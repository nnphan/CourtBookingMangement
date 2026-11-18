using System.Data;
using System.Globalization;
using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.Payments.Contracts;
using CourtBookingManagement.Application.Payments.Interfaces;
using CourtBookingManagement.Domain.Bookings;
using CourtBookingManagement.Domain.Payments;
using Dapper;

namespace CourtBookingManagement.Infrastructure.Repositories;

public sealed class PaymentRepository(ISqlConnectionFactory connectionFactory)
    : IPaymentRepository
{
    public async Task<SePayBookingPaymentContext?> GetSePayBookingForPaymentAsync(
        Guid bookingId,
        Guid? customerId,
        byte[]? holdTokenHash,
        CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<SePayBookingPaymentContext>(new CommandDefinition("""
            SELECT b.id AS BookingId,
                   b.booking_code AS BookingCode,
                   b.status AS BookingStatus,
                   UPPER(b.payment_method) AS PaymentMethod,
                   b.total_amount AS Amount,
                   b.expires_at AS ExpiresAt,
                   clock_timestamp() AS ServerTime
            FROM booking.bookings b
            WHERE b.id = @BookingId
              AND b.is_active = TRUE
              AND b.deleted_at IS NULL
              AND (b.customer_id = @CustomerId OR
                   (@HoldTokenHash IS NOT NULL AND b.hold_token_hash = @HoldTokenHash));
            """, new { BookingId = bookingId, CustomerId = customerId, HoldTokenHash = holdTokenHash },
            cancellationToken: cancellationToken));
    }

    public async Task<PaymentTransactionProjection?> CreateOrGetPendingSePayPaymentAsync(
        Guid bookingId,
        Guid? customerId,
        byte[]? holdTokenHash,
        Guid paymentId,
        string paymentCode,
        string orderId,
        string requestId,
        string qrUrl,
        decimal expectedAmount,
        DateTimeOffset expectedExpiresAt,
        string bankCode,
        string bankAccountNumber,
        CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);

        var booking = await connection.QuerySingleOrDefaultAsync<SePayBookingPaymentContext>(new CommandDefinition("""
            SELECT b.id AS BookingId,
                   b.booking_code AS BookingCode,
                   b.status AS BookingStatus,
                   UPPER(b.payment_method) AS PaymentMethod,
                   b.total_amount AS Amount,
                   b.expires_at AS ExpiresAt,
                   clock_timestamp() AS ServerTime
            FROM booking.bookings b
            WHERE b.id = @BookingId
              AND LOWER(b.status) = 'pending_payment'
              AND UPPER(b.payment_method) = 'SEPAY'
              AND b.expires_at > clock_timestamp()
              AND b.total_amount = @ExpectedAmount
              AND b.expires_at = @ExpectedExpiresAt
              AND b.is_active = TRUE
              AND b.deleted_at IS NULL
              AND (b.customer_id = @CustomerId OR
                   (@HoldTokenHash IS NOT NULL AND b.hold_token_hash = @HoldTokenHash))
            FOR UPDATE;
            """, new
        {
            BookingId = bookingId,
            CustomerId = customerId,
            HoldTokenHash = holdTokenHash,
            ExpectedAmount = expectedAmount,
            ExpectedExpiresAt = expectedExpiresAt
        }, transaction, cancellationToken: cancellationToken));

        if (booking is null)
        {
            transaction.Commit();
            return null;
        }

        var existingPaymentId = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition("""
            SELECT id
            FROM payment.payment_transactions
            WHERE booking_id = @BookingId
              AND provider = 'SEPAY'
              AND status IN ('PENDING', 'PROCESSING')
            ORDER BY created_at DESC
            LIMIT 1
            FOR UPDATE;
            """, new { BookingId = bookingId }, transaction, cancellationToken: cancellationToken));

        if (existingPaymentId is Guid activePaymentId)
        {
            var existingPayment = await GetPaymentProjectionAsync(
                connection,
                transaction,
                activePaymentId,
                cancellationToken);
            transaction.Commit();
            return existingPayment;
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO payment.payment_transactions
                (id, booking_id, provider, payment_method, order_id, request_id,
                 payment_code, amount, received_amount, currency, status,
                 qr_code_url, bank_code, bank_account_number, expires_at,
                 created_at, updated_at)
            VALUES
                (@PaymentId, @BookingId, 'SEPAY', 'VIETQR', @OrderId, @RequestId,
                 @PaymentCode, @Amount, 0, 'VND', 'PENDING',
                 @QrUrl, @BankCode, @BankAccountNumber, @ExpiresAt,
                 clock_timestamp(), clock_timestamp());
            """, new
        {
            PaymentId = paymentId,
            BookingId = bookingId,
            OrderId = orderId,
            RequestId = requestId,
            PaymentCode = paymentCode,
            booking.Amount,
            QrUrl = qrUrl,
            BankCode = bankCode,
            BankAccountNumber = bankAccountNumber,
            ExpiresAt = booking.ExpiresAt
        }, transaction, cancellationToken: cancellationToken));

        var createdPayment = await GetPaymentProjectionAsync(
            connection,
            transaction,
            paymentId,
            cancellationToken);
        transaction.Commit();
        return createdPayment;
    }

    public async Task<SePayWebhookProcessingOutcome> ProcessSePayWebhookAsync(
        SePayWebhookRequest request,
        string? paymentCode,
        DateTimeOffset? transactionDate,
        bool receivingAccountMatches,
        string rawPayload,
        CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);

        var callbackLogId = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition("""
            INSERT INTO payment.payment_callback_logs
                (id, provider, sepay_transaction_id, gateway, transaction_date,
                 account_number, sub_account, payment_code, transfer_type,
                 transfer_amount, reference_code, payload, processing_status, received_at)
            VALUES
                (uuid_generate_v7(), 'SEPAY', @TransactionId, @Gateway, @TransactionDate,
                 @AccountNumber, @SubAccount, @PaymentCode, @TransferType,
                 @TransferAmount, @ReferenceCode, CAST(@Payload AS jsonb), 'RECEIVED', clock_timestamp())
            ON CONFLICT (sepay_transaction_id)
                WHERE provider = 'SEPAY' AND sepay_transaction_id IS NOT NULL
            DO NOTHING
            RETURNING id;
            """, new
        {
            TransactionId = request.Id,
            request.Gateway,
            TransactionDate = transactionDate,
            request.AccountNumber,
            request.SubAccount,
            PaymentCode = paymentCode,
            request.TransferType,
            request.TransferAmount,
            request.ReferenceCode,
            Payload = rawPayload
        }, transaction, cancellationToken: cancellationToken));

        if (callbackLogId is null)
        {
            transaction.Commit();
            return new SePayWebhookProcessingOutcome(true, null, Array.Empty<BookingSlotResource>());
        }

        if (!string.Equals(request.TransferType, "in", StringComparison.OrdinalIgnoreCase))
        {
            await FinishCallbackAsync(
                connection, transaction, callbackLogId.Value, "REJECTED",
                "Only incoming transfers are processed.", cancellationToken);
            transaction.Commit();
            return new SePayWebhookProcessingOutcome(false, null, Array.Empty<BookingSlotResource>());
        }

        if (transactionDate is null
            || request.TransferAmount <= 0m
            || decimal.Truncate(request.TransferAmount) != request.TransferAmount)
        {
            await FinishCallbackAsync(
                connection, transaction, callbackLogId.Value, "REQUIRES_REVIEW",
                "The transaction date or transfer amount is invalid.", cancellationToken);
            transaction.Commit();
            return new SePayWebhookProcessingOutcome(false, null, Array.Empty<BookingSlotResource>());
        }

        if (!receivingAccountMatches)
        {
            await FinishCallbackAsync(
                connection, transaction, callbackLogId.Value, "REQUIRES_REVIEW",
                "The receiving account does not match the configured account.", cancellationToken);
            transaction.Commit();
            return new SePayWebhookProcessingOutcome(false, null, Array.Empty<BookingSlotResource>());
        }

        if (paymentCode is null)
        {
            await FinishCallbackAsync(
                connection, transaction, callbackLogId.Value, "REQUIRES_REVIEW",
                "No valid payment code was found.", cancellationToken);
            transaction.Commit();
            return new SePayWebhookProcessingOutcome(false, null, Array.Empty<BookingSlotResource>());
        }

        var paymentReference = await connection.QuerySingleOrDefaultAsync<SePayPaymentReference>(new CommandDefinition("""
            SELECT id AS PaymentId,
                   booking_id AS BookingId
            FROM payment.payment_transactions
            WHERE provider = 'SEPAY'
              AND payment_code = @PaymentCode;
            """, new { PaymentCode = paymentCode }, transaction, cancellationToken: cancellationToken));

        if (paymentReference is null)
        {
            await FinishCallbackAsync(
                connection, transaction, callbackLogId.Value, "REQUIRES_REVIEW",
                "No payment attempt matched the payment code.", cancellationToken);
            transaction.Commit();
            return new SePayWebhookProcessingOutcome(false, null, Array.Empty<BookingSlotResource>());
        }

        var lockedBookingId = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition("""
            SELECT id
            FROM booking.bookings
            WHERE id = @BookingId
            FOR UPDATE;
            """, new { paymentReference.BookingId }, transaction, cancellationToken: cancellationToken));

        if (lockedBookingId is null)
        {
            await FinishCallbackAsync(
                connection, transaction, callbackLogId.Value, "REQUIRES_REVIEW",
                "The booking for the payment code no longer exists.", cancellationToken);
            transaction.Commit();
            return new SePayWebhookProcessingOutcome(false, null, Array.Empty<BookingSlotResource>());
        }

        var payment = await connection.QuerySingleOrDefaultAsync<SePayWebhookPaymentProjection>(new CommandDefinition("""
            SELECT payment.id AS PaymentId,
                   payment.booking_id AS BookingId,
                   payment.amount AS Amount,
                   payment.status AS PaymentStatus,
                   payment.provider_transaction_id AS ProviderTransactionId,
                   booking.booking_code AS BookingCode,
                   booking.status AS BookingStatus,
                   booking.total_amount AS BookingAmount,
                   booking.expires_at AS BookingExpiresAt,
                   (booking.expires_at > clock_timestamp()) AS HoldUnexpired
            FROM payment.payment_transactions payment
            INNER JOIN booking.bookings booking ON booking.id = payment.booking_id
            WHERE payment.id = @PaymentId
              AND payment.provider = 'SEPAY'
              AND payment.payment_code = @PaymentCode
            FOR UPDATE OF payment;
            """, new { paymentReference.PaymentId, PaymentCode = paymentCode },
            transaction, cancellationToken: cancellationToken));

        if (payment is null)
        {
            await FinishCallbackAsync(
                connection, transaction, callbackLogId.Value, "REQUIRES_REVIEW",
                "No payment attempt matched the payment code.", cancellationToken);
            transaction.Commit();
            return new SePayWebhookProcessingOutcome(false, null, Array.Empty<BookingSlotResource>());
        }

        if (payment.PaymentStatus == PaymentStatuses.Paid)
        {
            await FinishCallbackAsync(
                connection, transaction, callbackLogId.Value, "REQUIRES_REVIEW",
                "An additional transfer arrived for an already paid payment.", cancellationToken);
            transaction.Commit();
            return new SePayWebhookProcessingOutcome(false, payment.BookingId, Array.Empty<BookingSlotResource>());
        }

        if (string.Equals(payment.BookingStatus, "PENDING_PAYMENT", StringComparison.OrdinalIgnoreCase)
            && !payment.HoldUnexpired)
        {
            var resources = await MarkLateSePayPaymentAsync(
                connection,
                transaction,
                payment,
                request,
                cancellationToken);
            await FinishCallbackAsync(
                connection, transaction, callbackLogId.Value, "REQUIRES_REVIEW",
                "Payment arrived after the booking hold ended.", cancellationToken);
            transaction.Commit();
            return new SePayWebhookProcessingOutcome(false, payment.BookingId, resources);
        }

        if (!string.Equals(payment.BookingStatus, "PENDING_PAYMENT", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(payment.PaymentStatus, PaymentStatuses.Pending, StringComparison.OrdinalIgnoreCase))
        {
            await UpdateSePayPaymentForReviewAsync(
                connection, transaction, payment.PaymentId, request, cancellationToken);
            await FinishCallbackAsync(
                connection, transaction, callbackLogId.Value, "REQUIRES_REVIEW",
                "The payment or booking state is not eligible for confirmation.", cancellationToken);
            transaction.Commit();
            return new SePayWebhookProcessingOutcome(false, payment.BookingId, Array.Empty<BookingSlotResource>());
        }

        if (payment.Amount != payment.BookingAmount)
        {
            await UpdateSePayPaymentForReviewAsync(
                connection, transaction, payment.PaymentId, request, cancellationToken);
            await FinishCallbackAsync(
                connection, transaction, callbackLogId.Value, "REQUIRES_REVIEW",
                "The payment amount differs from the persisted booking amount.", cancellationToken);
            transaction.Commit();
            return new SePayWebhookProcessingOutcome(false, payment.BookingId, Array.Empty<BookingSlotResource>());
        }

        if (request.TransferAmount != payment.Amount)
        {
            var paymentStatus = request.TransferAmount < payment.Amount
                ? PaymentStatuses.Underpaid
                : PaymentStatuses.Overpaid;
            var message = paymentStatus == PaymentStatuses.Underpaid
                ? "The incoming transfer is below the expected amount."
                : "The incoming transfer exceeds the expected amount.";
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE payment.payment_transactions
                SET status = @Status,
                    provider_transaction_id = @TransactionId,
                    bank_reference_code = NULLIF(@ReferenceCode, ''),
                    received_amount = @ReceivedAmount,
                    provider_message = @Message,
                    updated_at = clock_timestamp()
                WHERE id = @PaymentId
                  AND status = 'PENDING';
                """, new
            {
                Status = paymentStatus,
                TransactionId = request.Id.ToString(CultureInfo.InvariantCulture),
                request.ReferenceCode,
                ReceivedAmount = request.TransferAmount,
                Message = message,
                payment.PaymentId
            }, transaction, cancellationToken: cancellationToken));
            await FinishCallbackAsync(
                connection, transaction, callbackLogId.Value, "PROCESSED", message, cancellationToken);
            transaction.Commit();
            return new SePayWebhookProcessingOutcome(false, payment.BookingId, Array.Empty<BookingSlotResource>());
        }

        var confirmedRows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE booking.bookings
            SET status = 'confirmed',
                paid_amount = total_amount,
                expires_at = NULL,
                confirmed_at = clock_timestamp(),
                updated_at = clock_timestamp()
            WHERE id = @BookingId
              AND LOWER(status) = 'pending_payment'
              AND expires_at > clock_timestamp();
            """, new { payment.BookingId }, transaction, cancellationToken: cancellationToken));

        if (confirmedRows == 0)
        {
            var resources = await MarkLateSePayPaymentAsync(
                connection,
                transaction,
                payment,
                request,
                cancellationToken);
            await FinishCallbackAsync(
                connection, transaction, callbackLogId.Value, "REQUIRES_REVIEW",
                "Booking confirmation lost the expiration race.", cancellationToken);
            transaction.Commit();
            return new SePayWebhookProcessingOutcome(false, payment.BookingId, resources);
        }

        var confirmedDetails = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE booking.booking_details
            SET status = 'confirmed'
            WHERE booking_id = @BookingId
              AND LOWER(status) = 'held';
            """, new { payment.BookingId }, transaction, cancellationToken: cancellationToken));
        if (confirmedDetails == 0)
        {
            throw new InvalidOperationException(
                $"Confirmed booking {payment.BookingId} has no held booking details.");
        }

        var paidRows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE payment.payment_transactions
            SET status = 'PAID',
                provider_transaction_id = @TransactionId,
                bank_reference_code = NULLIF(@ReferenceCode, ''),
                received_amount = @ReceivedAmount,
                paid_at = clock_timestamp(),
                updated_at = clock_timestamp()
            WHERE id = @PaymentId
              AND status = 'PENDING';
            """, new
        {
            TransactionId = request.Id.ToString(CultureInfo.InvariantCulture),
            request.ReferenceCode,
            ReceivedAmount = request.TransferAmount,
            payment.PaymentId
        }, transaction, cancellationToken: cancellationToken));
        if (paidRows != 1)
        {
            throw new InvalidOperationException(
                $"Payment {payment.PaymentId} changed while its booking was being confirmed.");
        }

        var confirmedResources = await GetBookingResourcesAsync(
            connection,
            transaction,
            payment.BookingId,
            cancellationToken);
        await FinishCallbackAsync(
            connection, transaction, callbackLogId.Value, "PROCESSED",
            "Payment and booking confirmed.", cancellationToken);
        transaction.Commit();
        return new SePayWebhookProcessingOutcome(false, payment.BookingId, confirmedResources);
    }

    public async Task<PaymentTransactionProjection?> CreateOrGetPendingMomoPaymentAsync(
        Guid bookingId,
        Guid? customerId,
        byte[]? holdTokenHash,
        Guid paymentId,
        string orderId,
        string requestId,
        CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);

        var booking = await connection.QuerySingleOrDefaultAsync<PaymentBookingProjection>(new CommandDefinition("""
            SELECT b.id AS BookingId,
                   b.total_amount AS Amount
            FROM booking.bookings b
            WHERE b.id = @BookingId
              AND LOWER(b.status) = 'pending_payment'
              AND b.expires_at > NOW()
              AND UPPER(b.payment_method) = 'MOMO'
              AND b.is_active = TRUE
              AND b.deleted_at IS NULL
              AND (b.customer_id = @CustomerId OR
                   (@HoldTokenHash IS NOT NULL AND b.hold_token_hash = @HoldTokenHash))
            FOR UPDATE;
            """, new { BookingId = bookingId, CustomerId = customerId, HoldTokenHash = holdTokenHash },
            transaction, cancellationToken: cancellationToken));

        if (booking is null)
        {
            transaction.Commit();
            return null;
        }

        var existingPayment = await connection.QuerySingleOrDefaultAsync<PaymentTransactionProjection>(new CommandDefinition("""
            SELECT id AS PaymentId,
                   booking_id AS BookingId,
                   order_id AS OrderId,
                   request_id AS RequestId,
                   amount AS Amount,
                   status AS Status,
                   pay_url AS PayUrl,
                   deeplink AS Deeplink,
                   qr_code_url AS QrCodeUrl,
                   paid_at AS PaidAt,
                   (pay_url IS NULL AND status = 'PENDING') AS NeedsProviderRequest
            FROM payment.payment_transactions
            WHERE booking_id = @BookingId
              AND status IN ('PENDING', 'PROCESSING', 'PAYMENT_REVIEW')
            ORDER BY created_at DESC
            LIMIT 1
            FOR UPDATE;
            """, new { BookingId = bookingId }, transaction, cancellationToken: cancellationToken));

        if (existingPayment is not null)
        {
            transaction.Commit();
            return existingPayment;
        }

        var createdPayment = await connection.QuerySingleAsync<PaymentTransactionProjection>(new CommandDefinition("""
            INSERT INTO payment.payment_transactions
                (id, booking_id, provider, payment_method, order_id, request_id,
                 amount, currency, status, created_at, updated_at)
            VALUES
                (@PaymentId, @BookingId, 'MOMO', 'MOMO', @OrderId, @RequestId,
                 @Amount, 'VND', 'PENDING', NOW(), NOW())
            RETURNING id AS PaymentId,
                      booking_id AS BookingId,
                      order_id AS OrderId,
                      request_id AS RequestId,
                      amount AS Amount,
                      status AS Status,
                      pay_url AS PayUrl,
                      deeplink AS Deeplink,
                      qr_code_url AS QrCodeUrl,
                      paid_at AS PaidAt,
                      TRUE AS NeedsProviderRequest;
            """, new
        {
            PaymentId = paymentId,
            BookingId = bookingId,
            OrderId = orderId,
            RequestId = requestId,
            booking.Amount
        }, transaction, cancellationToken: cancellationToken));

        transaction.Commit();
        return createdPayment;
    }

    public async Task<PaymentTransactionProjection?> GetPaymentAsync(
        Guid paymentId,
        Guid? customerId,
        byte[]? holdTokenHash,
        CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<PaymentTransactionProjection>(new CommandDefinition("""
            SELECT payment.id AS PaymentId,
                   payment.booking_id AS BookingId,
                   payment.provider AS Provider,
                   booking.booking_code AS BookingCode,
                   booking.status AS BookingStatus,
                   payment.order_id AS OrderId,
                   payment.request_id AS RequestId,
                   payment.amount AS Amount,
                   payment.received_amount AS ReceivedAmount,
                   payment.status AS Status,
                   payment.payment_code AS PaymentCode,
                   payment.bank_code AS BankCode,
                   payment.bank_account_number AS BankAccountNumber,
                   payment.pay_url AS PayUrl,
                   payment.deeplink AS Deeplink,
                   payment.qr_code_url AS QrCodeUrl,
                   payment.paid_at AS PaidAt,
                   payment.expires_at AS ExpiresAt,
                   clock_timestamp() AS ServerTime,
                   CASE WHEN payment.expires_at IS NULL THEN NULL
                        ELSE GREATEST(0, CEIL(EXTRACT(EPOCH FROM (payment.expires_at - clock_timestamp()))))::integer
                   END AS RemainingSeconds,
                   FALSE AS NeedsProviderRequest
            FROM payment.payment_transactions payment
            INNER JOIN booking.bookings booking ON booking.id = payment.booking_id
            WHERE payment.id = @PaymentId
              AND (booking.customer_id = @CustomerId OR
                   (@HoldTokenHash IS NOT NULL AND booking.hold_token_hash = @HoldTokenHash));
            """, new { PaymentId = paymentId, CustomerId = customerId, HoldTokenHash = holdTokenHash },
            cancellationToken: cancellationToken));
    }

    public async Task<PaymentTransactionProjection?> GetLatestPaymentForBookingAsync(
        Guid bookingId,
        Guid? customerId,
        byte[]? holdTokenHash,
        CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<PaymentTransactionProjection>(new CommandDefinition("""
            SELECT payment.id AS PaymentId,
                   payment.booking_id AS BookingId,
                   payment.provider AS Provider,
                   booking.booking_code AS BookingCode,
                   booking.status AS BookingStatus,
                   payment.order_id AS OrderId,
                   payment.request_id AS RequestId,
                   payment.amount AS Amount,
                   payment.received_amount AS ReceivedAmount,
                   payment.status AS Status,
                   payment.payment_code AS PaymentCode,
                   payment.bank_code AS BankCode,
                   payment.bank_account_number AS BankAccountNumber,
                   payment.pay_url AS PayUrl,
                   payment.deeplink AS Deeplink,
                   payment.qr_code_url AS QrCodeUrl,
                   payment.paid_at AS PaidAt,
                   payment.expires_at AS ExpiresAt,
                   clock_timestamp() AS ServerTime,
                   CASE WHEN payment.expires_at IS NULL THEN NULL
                        ELSE GREATEST(0, CEIL(EXTRACT(EPOCH FROM (payment.expires_at - clock_timestamp()))))::integer
                   END AS RemainingSeconds,
                   FALSE AS NeedsProviderRequest
            FROM payment.payment_transactions payment
            INNER JOIN booking.bookings booking ON booking.id = payment.booking_id
            WHERE payment.booking_id = @BookingId
              AND (booking.customer_id = @CustomerId OR
                   (@HoldTokenHash IS NOT NULL AND booking.hold_token_hash = @HoldTokenHash))
            ORDER BY payment.created_at DESC
            LIMIT 1;
            """, new { BookingId = bookingId, CustomerId = customerId, HoldTokenHash = holdTokenHash },
            cancellationToken: cancellationToken));
    }

    public async Task SaveMomoResponseAsync(
        Guid paymentId,
        MomoProviderPaymentResponse response,
        bool signatureVerified,
        CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE payment.payment_transactions
            SET status = CASE
                    WHEN NOT @SignatureVerified THEN 'PAYMENT_REVIEW'
                    WHEN @ResultCode = 0 THEN 'PROCESSING'
                    ELSE 'FAILED'
                END,
                provider_transaction_id = NULLIF(@TransactionId, ''),
                result_code = @ResultCode::text,
                provider_message = LEFT(@Message, 500),
                pay_url = CASE WHEN @SignatureVerified THEN @PayUrl ELSE NULL END,
                deeplink = CASE WHEN @SignatureVerified THEN @Deeplink ELSE NULL END,
                qr_code_url = CASE WHEN @SignatureVerified THEN @QrCodeUrl ELSE NULL END,
                signature_verified = @SignatureVerified,
                updated_at = NOW()
            WHERE id = @PaymentId
              AND status = 'PENDING';
            """, new
        {
            PaymentId = paymentId,
            response.ResultCode,
            TransactionId = response.TransactionId == 0 ? null : response.TransactionId.ToString(),
            response.Message,
            response.PayUrl,
            response.Deeplink,
            response.QrCodeUrl,
            SignatureVerified = signatureVerified
        }, cancellationToken: cancellationToken));
    }

    public async Task<MomoIpnOutcome> ProcessMomoIpnAsync(
        MomoIpnRequest request,
        bool signatureVerified,
        string sanitizedPayload,
        CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);

        var callbackLogId = Guid.NewGuid();
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO payment.payment_callback_logs
                (id, provider, order_id, request_id, signature_verified,
                 payload, processing_status, received_at)
            VALUES
                (@Id, 'MOMO', NULLIF(@OrderId, ''), NULLIF(@RequestId, ''),
                 @SignatureVerified, CAST(@Payload AS jsonb), 'RECEIVED', NOW());
            """, new
        {
            Id = callbackLogId,
            request.OrderId,
            request.RequestId,
            SignatureVerified = signatureVerified,
            Payload = sanitizedPayload
        }, transaction, cancellationToken: cancellationToken));

        if (!signatureVerified)
        {
            await FinishCallbackAsync(connection, transaction, callbackLogId, "REJECTED", "Signature or partner validation failed.", cancellationToken);
            transaction.Commit();
            return new MomoIpnOutcome(
                new MomoIpnResponse(1, "Invalid payment notification."),
                null,
                Array.Empty<BookingSlotResource>());
        }

        var payment = await connection.QuerySingleOrDefaultAsync<IpnPaymentProjection>(new CommandDefinition("""
            SELECT payment.id AS PaymentId,
                   payment.booking_id AS BookingId,
                   payment.amount AS Amount,
                   payment.status AS PaymentStatus,
                   booking.status AS BookingStatus,
                   booking.expires_at AS ExpiresAt,
                   (booking.expires_at > NOW()) AS HoldUnexpired,
                   booking.total_amount AS BookingAmount
            FROM payment.payment_transactions payment
            INNER JOIN booking.bookings booking ON booking.id = payment.booking_id
            WHERE payment.provider = 'MOMO'
              AND payment.order_id = @OrderId
              AND payment.request_id = @RequestId
            FOR UPDATE OF payment, booking;
            """, new { request.OrderId, request.RequestId }, transaction, cancellationToken: cancellationToken));

        if (payment is null)
        {
            await FinishCallbackAsync(connection, transaction, callbackLogId, "REQUIRES_REVIEW", "No matching local payment was found.", cancellationToken);
            transaction.Commit();
            return new MomoIpnOutcome(
                new MomoIpnResponse(0, "Notification accepted for review."),
                null,
                Array.Empty<BookingSlotResource>());
        }

        var paymentAmount = payment.Amount > 0m && decimal.Truncate(payment.Amount) == payment.Amount
            ? decimal.ToInt64(payment.Amount)
            : -1;
        if (paymentAmount != request.Amount
            || payment.BookingAmount != payment.Amount)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE payment.payment_transactions
                SET status = 'PAYMENT_REVIEW',
                    result_code = @ResultCode::text,
                    provider_message = 'Provider amount or transaction data did not match the local payment.',
                    signature_verified = TRUE,
                    updated_at = NOW()
                WHERE id = @PaymentId;
                """, new { payment.PaymentId, request.ResultCode }, transaction, cancellationToken: cancellationToken));
            await FinishCallbackAsync(connection, transaction, callbackLogId, "REQUIRES_REVIEW", "Provider amount or transaction data did not match.", cancellationToken);
            transaction.Commit();
            return new MomoIpnOutcome(
                new MomoIpnResponse(0, "Notification accepted for review."),
                payment.BookingId,
                Array.Empty<BookingSlotResource>());
        }

        if (string.Equals(payment.PaymentStatus, PaymentStatuses.Paid, StringComparison.OrdinalIgnoreCase))
        {
            await FinishCallbackAsync(connection, transaction, callbackLogId, "PROCESSED", "Duplicate notification already applied.", cancellationToken);
            transaction.Commit();
            return new MomoIpnOutcome(
                new MomoIpnResponse(0, "Successful."),
                payment.BookingId,
                Array.Empty<BookingSlotResource>());
        }

        if (payment.PaymentStatus is PaymentStatuses.Failed or PaymentStatuses.Expired or PaymentStatuses.Refunded)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE payment.payment_transactions
                SET status = 'PAYMENT_REVIEW',
                    provider_transaction_id = NULLIF(@TransactionId, ''),
                    result_code = @ResultCode::text,
                    provider_message = 'Provider success conflicts with the local payment state.',
                    signature_verified = TRUE,
                    updated_at = NOW()
                WHERE id = @PaymentId;
                """, new
            {
                payment.PaymentId,
                TransactionId = request.TransactionId == 0 ? null : request.TransactionId.ToString(),
                request.ResultCode
            }, transaction, cancellationToken: cancellationToken));
            await FinishCallbackAsync(connection, transaction, callbackLogId, "REQUIRES_REVIEW", "Provider success conflicts with local payment status.", cancellationToken);
            transaction.Commit();
            return new MomoIpnOutcome(
                new MomoIpnResponse(0, "Notification accepted for review."),
                payment.BookingId,
                Array.Empty<BookingSlotResource>());
        }

        if (request.ResultCode != 0)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE payment.payment_transactions
                SET status = 'FAILED',
                    provider_transaction_id = NULLIF(@TransactionId, ''),
                    result_code = @ResultCode::text,
                    provider_message = LEFT(@Message, 500),
                    signature_verified = TRUE,
                    updated_at = NOW()
                WHERE id = @PaymentId
                  AND status IN ('PENDING', 'PROCESSING', 'PAYMENT_REVIEW');
                """, new
            {
                payment.PaymentId,
                TransactionId = request.TransactionId == 0 ? null : request.TransactionId.ToString(),
                request.ResultCode,
                request.Message
            }, transaction, cancellationToken: cancellationToken));
            await FinishCallbackAsync(connection, transaction, callbackLogId, "PROCESSED", "Payment failure recorded.", cancellationToken);
            transaction.Commit();
            return new MomoIpnOutcome(
                new MomoIpnResponse(0, "Successful."),
                payment.BookingId,
                Array.Empty<BookingSlotResource>());
        }

        var confirmedRows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE booking.bookings
            SET status = 'confirmed',
                paid_amount = total_amount,
                expires_at = NULL,
                confirmed_at = clock_timestamp(),
                updated_at = clock_timestamp()
            WHERE id = @BookingId
              AND LOWER(status) = 'pending_payment'
              AND expires_at > clock_timestamp();
            """, new { payment.BookingId }, transaction, cancellationToken: cancellationToken));

        if (confirmedRows == 0)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE payment.payment_transactions
                SET status = 'PAYMENT_REVIEW',
                    provider_transaction_id = NULLIF(@TransactionId, ''),
                    result_code = @ResultCode::text,
                    provider_message = 'Payment arrived after the booking hold ended.',
                    signature_verified = TRUE,
                    updated_at = NOW()
                WHERE id = @PaymentId;

                UPDATE booking.bookings
                SET status = CASE
                        WHEN LOWER(status) = 'pending_payment' AND expires_at <= clock_timestamp() THEN 'expired'
                        ELSE status
                    END,
                    updated_at = clock_timestamp()
                WHERE id = @BookingId;

                UPDATE booking.booking_details
                SET status = 'expired'
                WHERE booking_id = @BookingId
                  AND LOWER(status) = 'held'
                  AND EXISTS (
                      SELECT 1 FROM booking.bookings
                      WHERE id = @BookingId AND LOWER(status) = 'expired'
                  );
                """, new
            {
                payment.PaymentId,
                payment.BookingId,
                TransactionId = request.TransactionId == 0 ? null : request.TransactionId.ToString(),
                request.ResultCode
            }, transaction, cancellationToken: cancellationToken));
            var expiredResources = await GetBookingResourcesAsync(connection, transaction, payment.BookingId, cancellationToken);
            await FinishCallbackAsync(connection, transaction, callbackLogId, "REQUIRES_REVIEW", "Payment arrived after the booking hold ended.", cancellationToken);
            transaction.Commit();
            return new MomoIpnOutcome(
                new MomoIpnResponse(0, "Notification accepted for review."),
                payment.BookingId,
                expiredResources);
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE payment.payment_transactions
            SET status = 'PAID',
                provider_transaction_id = NULLIF(@TransactionId, ''),
                result_code = @ResultCode::text,
                provider_message = LEFT(@Message, 500),
                signature_verified = TRUE,
                paid_at = clock_timestamp(),
                updated_at = clock_timestamp()
            WHERE id = @PaymentId
              AND status IN ('PENDING', 'PROCESSING', 'PAYMENT_REVIEW');

            UPDATE booking.booking_details
            SET status = 'confirmed'
            WHERE booking_id = @BookingId
              AND LOWER(status) = 'held'
              AND EXISTS (
                  SELECT 1 FROM booking.bookings
                  WHERE id = @BookingId AND LOWER(status) = 'confirmed'
              );
            """, new
        {
            payment.PaymentId,
            payment.BookingId,
            TransactionId = request.TransactionId == 0 ? null : request.TransactionId.ToString(),
            request.ResultCode,
            request.Message
        }, transaction, cancellationToken: cancellationToken));

        var resources = await GetBookingResourcesAsync(connection, transaction, payment.BookingId, cancellationToken);
        await FinishCallbackAsync(connection, transaction, callbackLogId, "PROCESSED", "Payment and booking confirmed.", cancellationToken);
        transaction.Commit();
        return new MomoIpnOutcome(
            new MomoIpnResponse(0, "Successful."),
            payment.BookingId,
            resources);
    }

    private static Task<PaymentTransactionProjection?> GetPaymentProjectionAsync(
        System.Data.IDbConnection connection,
        IDbTransaction transaction,
        Guid paymentId,
        CancellationToken cancellationToken) =>
        connection.QuerySingleOrDefaultAsync<PaymentTransactionProjection>(new CommandDefinition("""
            SELECT payment.id AS PaymentId,
                   payment.booking_id AS BookingId,
                   payment.provider AS Provider,
                   booking.booking_code AS BookingCode,
                   booking.status AS BookingStatus,
                   payment.order_id AS OrderId,
                   payment.request_id AS RequestId,
                   payment.amount AS Amount,
                   payment.received_amount AS ReceivedAmount,
                   payment.status AS Status,
                   payment.payment_code AS PaymentCode,
                   payment.bank_code AS BankCode,
                   payment.bank_account_number AS BankAccountNumber,
                   payment.pay_url AS PayUrl,
                   payment.deeplink AS Deeplink,
                   payment.qr_code_url AS QrCodeUrl,
                   payment.paid_at AS PaidAt,
                   payment.expires_at AS ExpiresAt,
                   clock_timestamp() AS ServerTime,
                   CASE WHEN payment.expires_at IS NULL THEN NULL
                        ELSE GREATEST(
                            0,
                            CEIL(EXTRACT(EPOCH FROM (payment.expires_at - clock_timestamp())))
                        )::integer
                   END AS RemainingSeconds,
                   FALSE AS NeedsProviderRequest
            FROM payment.payment_transactions payment
            INNER JOIN booking.bookings booking ON booking.id = payment.booking_id
            WHERE payment.id = @PaymentId;
            """, new { PaymentId = paymentId }, transaction, cancellationToken: cancellationToken));

    private static async Task<IReadOnlyList<BookingSlotResource>> MarkLateSePayPaymentAsync(
        System.Data.IDbConnection connection,
        IDbTransaction transaction,
        SePayWebhookPaymentProjection payment,
        SePayWebhookRequest request,
        CancellationToken cancellationToken)
    {
        await UpdateSePayPaymentForReviewAsync(
            connection,
            transaction,
            payment.PaymentId,
            request,
            cancellationToken);

        var expiredRows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE booking.bookings
            SET status = 'expired',
                updated_at = clock_timestamp()
            WHERE id = @BookingId
              AND LOWER(status) = 'pending_payment'
              AND expires_at <= clock_timestamp();
            """, new { payment.BookingId }, transaction, cancellationToken: cancellationToken));

        if (expiredRows == 0)
        {
            return Array.Empty<BookingSlotResource>();
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE booking.booking_details
            SET status = 'expired'
            WHERE booking_id = @BookingId
              AND LOWER(status) = 'held';
            """, new { payment.BookingId }, transaction, cancellationToken: cancellationToken));

        return await GetBookingResourcesAsync(
            connection,
            transaction,
            payment.BookingId,
            cancellationToken);
    }

    private static Task<int> UpdateSePayPaymentForReviewAsync(
        System.Data.IDbConnection connection,
        IDbTransaction transaction,
        Guid paymentId,
        SePayWebhookRequest request,
        CancellationToken cancellationToken) =>
        connection.ExecuteAsync(new CommandDefinition("""
            UPDATE payment.payment_transactions
            SET status = 'PAYMENT_REVIEW',
                provider_transaction_id = COALESCE(
                    provider_transaction_id,
                    @TransactionId),
                bank_reference_code = COALESCE(
                    bank_reference_code,
                    NULLIF(@ReferenceCode, '')),
                received_amount = GREATEST(received_amount, @ReceivedAmount),
                provider_message = 'Authenticated SePay transaction requires manual review.',
                updated_at = clock_timestamp()
            WHERE id = @PaymentId
              AND status IN ('PENDING', 'PROCESSING', 'EXPIRED');
            """, new
        {
            TransactionId = request.Id.ToString(CultureInfo.InvariantCulture),
            request.ReferenceCode,
            ReceivedAmount = request.TransferAmount,
            PaymentId = paymentId
        }, transaction, cancellationToken: cancellationToken));

    private static Task FinishCallbackAsync(
        System.Data.IDbConnection connection,
        IDbTransaction transaction,
        Guid callbackLogId,
        string status,
        string message,
        CancellationToken cancellationToken) =>
        connection.ExecuteAsync(new CommandDefinition("""
            UPDATE payment.payment_callback_logs
            SET processing_status = @Status,
                processing_message = LEFT(@Message, 500),
                processed_at = NOW()
            WHERE id = @CallbackLogId;
            """, new { CallbackLogId = callbackLogId, Status = status, Message = message },
            transaction, cancellationToken: cancellationToken));

    private static async Task<IReadOnlyList<BookingSlotResource>> GetBookingResourcesAsync(
        System.Data.IDbConnection connection,
        IDbTransaction transaction,
        Guid bookingId,
        CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync<BookingHoldSlot>(new CommandDefinition("""
            SELECT court_id AS CourtId,
                   booking_date AS BookingDate,
                   start_time AS StartTime,
                   end_time AS EndTime
            FROM booking.booking_details
            WHERE booking_id = @BookingId;
            """, new { BookingId = bookingId }, transaction, cancellationToken: cancellationToken));
        return rows.Select(row => new BookingSlotResource(
            row.CourtId,
            row.BookingDate,
            row.StartTime,
            row.EndTime)).ToArray();
    }

    private sealed class PaymentBookingProjection
    {
        public Guid BookingId { get; init; }
        public decimal Amount { get; init; }
    }

    private sealed class IpnPaymentProjection
    {
        public Guid PaymentId { get; init; }
        public Guid BookingId { get; init; }
        public decimal Amount { get; init; }
        public string PaymentStatus { get; init; } = string.Empty;
        public string BookingStatus { get; init; } = string.Empty;
        public DateTimeOffset? ExpiresAt { get; init; }
        public bool HoldUnexpired { get; init; }
        public decimal BookingAmount { get; init; }
    }

    private sealed class SePayWebhookPaymentProjection
    {
        public Guid PaymentId { get; init; }
        public Guid BookingId { get; init; }
        public decimal Amount { get; init; }
        public string PaymentStatus { get; init; } = string.Empty;
        public string? ProviderTransactionId { get; init; }
        public string BookingCode { get; init; } = string.Empty;
        public string BookingStatus { get; init; } = string.Empty;
        public decimal BookingAmount { get; init; }
        public DateTimeOffset? BookingExpiresAt { get; init; }
        public bool HoldUnexpired { get; init; }
    }

    private sealed class SePayPaymentReference
    {
        public Guid PaymentId { get; init; }
        public Guid BookingId { get; init; }
    }

    private sealed class BookingHoldSlot
    {
        public Guid CourtId { get; init; }
        public DateOnly BookingDate { get; init; }
        public TimeOnly StartTime { get; init; }
        public TimeOnly EndTime { get; init; }
    }
}
