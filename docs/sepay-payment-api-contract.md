# SePay Court Booking Payment Contract

This contract covers SePay QR creation, payment status, authenticated webhook processing, and server-side reconciliation with SePay API v2.

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

`GET /api/bookings/{bookingId}/payment-status` or `GET /api/payments/{paymentId}`

For guests, send `X-Booking-Hold-Token`; authenticated customers are checked against booking ownership. The response retains the existing payment fields and includes booking/payment state, payment code, QR URL, receiving-bank details, received amount, expiry, server time, and remaining seconds when available. It never treats a browser return or local countdown as proof of payment.

## Money, dates, and QR

- Amounts are represented as JSON numbers in VND; SePay QR creation rejects non-positive or fractional VND amounts.
- Timestamps are returned as UTC-compatible ISO 8601 values.
- QR parameters are constructed server-side from configured account/bank details, persisted booking amount, payment code, and template. The frontend must not construct authoritative payment QR data.
- The UI should require the exact amount and exact payment code for manual transfers.

## Current payment states

New attempts return `PENDING`. Exact transfers confirm an active booking. Amount mismatches are shown as `UNDERPAID` or `OVERPAID`. Expired or otherwise ineligible bookings may require `PAYMENT_REVIEW`. Customers can reopen payment status from the QR dialog or My Bookings. Guest status links use the original session's booking hold token.

## Configuration

Configure the `SePay` section through environment variables or a secret store. Set the receiving bank code, account number, account holder, and selected webhook credential. In SePay, point the incoming-transfer webhook at the public HTTPS URL ending in `/api/payments/sepay/webhook` and select JSON. If using SePay's payment-code filter, configure the same prefix and a ten-character alphanumeric suffix.

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

The payload uses SePay's documented JSON fields: `id`, `gateway`, `transactionDate`, `accountNumber`, `subAccount`, `code`, `content`, `transferType`, `description`, `transferAmount`, `accumulated`, and `referenceCode`. `id` is the deduplication key. A payment code must match the configured prefix followed by ten hexadecimal characters. When SePay returns a blank, truncated, or invalid `code`, the backend accepts one distinct full code from `content`. Conflicting full codes require review.

SePay's dashboard "Send test" request can use transaction ID `0`. In Sandbox only, an authenticated request with an explicit ID of `0` is acknowledged as a connectivity check without recording or processing a payment. Missing or negative IDs, and ID `0` outside Sandbox, are rejected. Use a simulated transaction with a positive ID to verify payment processing and deduplication.

For an authenticated request, the backend stores the JSON payload and transaction metadata in the existing callback audit table. Duplicate transaction IDs are acknowledged without applying payment changes. An authenticated replay with the original payload can be processed again if the first delivery stopped before finding any valid payment code and the replay can now resolve it. Unmatched codes, non-incoming transfers, invalid amounts, and receiving-account mismatches are audited and acknowledged without confirming a booking.

The success response is HTTP 200 with exactly:

```json
{"success": true}
```

For an exact transfer to a live, pending booking, the payment becomes `PAID`, the booking and all held details become `CONFIRMED`, and the booking expiry is cleared in one PostgreSQL transaction. Underpayments become `UNDERPAID`; overpayments become `OVERPAID`; neither confirms the booking. An authenticated payment arriving after expiry becomes `PAYMENT_REVIEW` and cannot resurrect the booking. Redis hold cleanup is attempted only after commit; a Redis failure does not roll back PostgreSQL state.

Invalid authentication returns 401; malformed payloads return 400. Transient database failures are allowed to fail the request so SePay can retry according to its configured policy.

## Reconciliation

Apply `20261127_AddSePayReconciliation.sql` before deploying the updated payment backend. Configure `SePay__ApiToken` and `SePay__ApiBankAccountId` (the SePay bank-account UUID) to enable the worker. Set `SePay__ApiBaseUrl` to `https://userapi.sepay.vn/v2` for Production or `https://userapi-sandbox.sepay.vn/v2` for Sandbox with the matching token. The worker checks incoming transfers every 30 seconds over the last 24 hours by default. The interval and lookback are configurable. Each sweep reviews at most 20 pages of 100 transactions and logs when it reaches that limit.

The worker validates the configured bank account, exact payment code, provider transaction UUID, transaction time, and amount. Both delivery paths use the same database transaction and booking lock. A transfer observed via both paths is matched using bank reference, account, amount, timestamp, and payment code. Transfers for expired holds remain available for manual review. This flow neither reopens expired holds nor initiates bank refunds.

Use [SePay sandbox transactions](https://developer.sepay.vn/vi/tien-ich-khac/test-mode/mo-phong-giao-dich) to verify payment confirmation, amount mismatch, and webhook replay. See the [SePay transaction API](https://developer.sepay.vn/vi/sepay-api/v2/giao-dich/danh-sach) for API configuration.
