# SePay Court Booking Payment Contract

This contract describes the SePay booking-payment and webhook endpoints implemented so far. Reconciliation remains deferred.

## Create or reuse a payment

`POST /api/payments/sepay`

Request body:

```json
{
  "bookingId": "00000000-0000-0000-0000-000000000000"
}
```

For guest bookings, send the original booking hold token in `X-Booking-Hold-Token`. Authenticated customers are authorized using their customer profile. The booking must belong to the caller, have `PENDING_PAYMENT` status, use the `SEPAY` payment method, and have an unexpired hold.

The backend reads the amount, booking code, status, and expiration from PostgreSQL. Repeated requests for an active SePay attempt return that attempt rather than creating another. The QR URL and payment code are generated server-side.

The successful response uses the standard API envelope. Its `data` fields are:

| Field | Type | Meaning |
|---|---|---|
| `paymentId` | UUID | Payment-attempt identifier |
| `bookingId` | UUID | Booking aggregate identifier |
| `bookingCode` | string | Existing booking code |
| `paymentCode` | string | Unique bank-transfer content |
| `amount` | number | Exact integer VND amount from the booking |
| `status` | string | Initially `PENDING` |
| `qrUrl` | string | Server-generated QR image URL |
| `bankCode` | string | Configured receiving bank code |
| `bankAccountNumber` | string | Configured receiving account |
| `accountHolderName` | string | Configured account holder |
| `expiresAt` | timestamp | Database booking-hold expiry |
| `serverTime` | timestamp | Database time used for remaining-time calculation |
| `remainingSeconds` | integer | Non-negative seconds until expiry |

## Read payment status

`GET /api/payments/{paymentId}`

For guests, send `X-Booking-Hold-Token`; authenticated customers are checked against booking ownership. The response retains the existing payment fields and includes booking/payment state, payment code, QR URL, receiving-bank details, received amount, expiry, server time, and remaining seconds when available. It never treats a browser return or local countdown as proof of payment.

## Money, dates, and QR

- Amounts are represented as JSON numbers in VND; SePay QR creation rejects non-positive or fractional VND amounts.
- Timestamps are returned as UTC-compatible ISO 8601 values.
- QR parameters are constructed server-side from configured account/bank details, persisted booking amount, payment code, and template. The frontend must not construct authoritative payment QR data.
- The UI should require the exact amount and exact payment code for manual transfers.

## Current payment states

The schema retains the existing `PAYMENT_REVIEW` state for compatibility. It also permits `UNDERPAID` and `OVERPAID`. At this phase, create returns `PENDING`; webhook transitions and the final amount-mismatch policy are not implemented yet.

## Configuration

Configure the `SePay` section through environment variables or an approved secret store. The receiving bank code, account number, account holder, and selected webhook credential are required before payment creation is enabled. `ApiToken` is optional because the Phase 3 flow does not call SePay API v2.

Supported secret environment keys include:

```text
SePay__WebhookApiKey
SePay__WebhookHmacSecret
SePay__ApiToken
SePay__BankAccountNumber
```

## Errors

| Error code | HTTP status | Meaning |
|---|---:|---|
| `Payment.SePayNotConfigured` | 503 | Required SePay payment configuration is unavailable |
| `Booking.NotFound` | 404 | Booking is missing or not accessible to the caller |
| `Booking.StatusConflict` | 409 | Booking is not an active SePay payment hold |
| `Booking.HoldExpired` | 409 | Booking hold has expired |
| `Payment.InvalidAmount` | 400 | Booking amount is not a positive whole number of VND |
| `Payment.StatusConflict` | 409 | Payment attempt is under review or incomplete |

## SePay webhook

`POST /api/payments/sepay/webhook`

Configure SePay to send JSON. The endpoint is anonymous at the ASP.NET authorization layer and authenticates each request using the configured webhook mode:

- **API Key:** SePay sends `Authorization: Apikey <key>`. The configured key is compared without logging either value.
- **HMAC-SHA256:** Verify `X-SePay-Signature: sha256=<hex>` and `X-SePay-Timestamp` against `HMAC-SHA256(secret, timestamp + "." + exact raw request body)`. The timestamp must be within five minutes of server time. Do not parse and reserialize the body before verification.

The payload uses SePay's documented JSON fields: `id`, `gateway`, `transactionDate`, `accountNumber`, `subAccount`, `code`, `content`, `transferType`, `description`, `transferAmount`, `accumulated`, and `referenceCode`. `id` is the deduplication key. If `code` is `null`, the backend accepts exactly one payment code matching the configured prefix plus ten hexadecimal characters from `content`; a present but invalid `code` does not fall back to content parsing.

SePay's dashboard "Send test" request can use transaction ID `0`. In Sandbox only, an authenticated request with an explicit ID of `0` is acknowledged as a connectivity check without recording or processing a payment. Missing or negative IDs, and ID `0` outside Sandbox, are rejected. Use a simulated transaction with a positive ID to verify payment processing and deduplication.

For an authenticated request, the backend stores the JSON payload and transaction metadata in the existing callback audit table. Duplicate transaction IDs are acknowledged without applying payment changes. Unmatched codes, non-incoming transfers, invalid amounts, and receiving-account mismatches are audited and acknowledged without confirming a booking.

The success response is HTTP 200 with exactly:

```json
{"success": true}
```

For an exact transfer to a live, pending booking, the payment becomes `PAID`, the booking and all held details become `CONFIRMED`, and the booking expiry is cleared in one PostgreSQL transaction. Underpayments become `UNDERPAID`; overpayments become `OVERPAID`; neither confirms the booking. An authenticated payment arriving after expiry becomes `PAYMENT_REVIEW` and cannot resurrect the booking. Redis hold cleanup is attempted only after commit; a Redis failure does not roll back PostgreSQL state.

Invalid authentication returns 401; malformed payloads return 400. Transient database failures are allowed to fail the request so SePay can retry according to its configured policy.

## Migration compatibility and deferred contract

Historical MoMo payment rows remain readable and are not backfilled with SePay payment codes. New SePay attempts use the existing `payment.payment_transactions` table. Reconciliation is not implemented. Webhook payload and authentication formats follow the [SePay webhook integration guide](https://developer.sepay.vn/en/sepay-webhooks/tich-hop-webhook) and [authentication guide](https://developer.sepay.vn/en/sepay-webhooks/xac-thuc).
