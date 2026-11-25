# Realtime court selection

Both Court Status pages now claim a range as soon as a user selects it. One selection session can own several ranges on the same branch and business date. The first claim starts the lease. Adding or removing a range preserves the original expiry.

## Database and checkout

Apply [20261125_AddCourtSelectionHolds.sql](../Database/Migrations/20261125_AddCourtSelectionHolds.sql) after the existing booking/payment migrations using the repository's SQL migration procedure. PostgreSQL 14 or later and the existing `btree_gist` extension are required. The migration creates selection holds, their ranges, and idempotency receipts. The range index uses GiST. Partial indexes cover the active session, board and expiration worker. Existing booking rows and timestamp columns require no backfill.

The repository locks the subject and court/business-date in PostgreSQL before checking overlap and inserting a selection range. The booking-detail trigger obtains the same court/date lock and checks active selections, including for callers that bypass the main booking repository. The existing booking-detail exclusion constraint still covers overlap between bookings. Times are stored as UTC instants using half-open ranges. Branch-local input is interpreted using the branch timezone, with `Asia/Ho_Chi_Minh` as the existing fallback.

Checkout supplies the selection hold ID and browser session ID. It locks the hold, verifies the owner and database clock, and compares the union of held ranges with the union of the requested ranges per court. Checkout closes the selection, links it to the booking, and writes the booking details in one transaction. Cash checkout writes confirmed booking details. Online checkout hands the ranges to the existing pending-payment booking and its separate expiry and confirmation workflow. Release and expiry close the selection so its ranges immediately stop blocking claims. An expired selection is ignored by the database even before the worker processes it.

Verify the installation on a staging copy of the booking database.

```sql
SELECT tablename, indexname FROM pg_indexes
WHERE schemaname = 'booking' AND tablename LIKE 'court_selection_%';

SELECT tgname FROM pg_trigger
WHERE tgrelid = 'booking.booking_details'::regclass
  AND tgname = 'check_court_selection_overlap';
```

For rollback, first route traffic to the previous app release and let active five-minute leases finish. Run the following in a transaction after archiving selection history if needed. Existing bookings remain in the original tables.

```sql
BEGIN;
DROP TRIGGER check_court_selection_overlap ON booking.booking_details;
DROP FUNCTION booking.check_court_selection_overlap();
DROP TABLE booking.court_selection_slots, booking.court_selection_operations, booking.court_selection_holds;
COMMIT;
```

## API and ownership

| Endpoint | Behavior |
| --- | --- |
| `POST /api/court-selections/guest-session?ownerSessionId=…` | Issues a time-limited protected guest credential bound to the tab session. |
| `GET /api/court-selections/{branchId}/{businessDate}?ownerSessionId=…` | Returns current holds, server time, branch timezone and operating windows. The original `GET /api/court-status` supplies courts and the booking schedule. |
| `POST /api/court-selections/slots` | Claims the supplied branch, date, court and local start/end time for `ownerSessionId`. An explicit `holdId` is optional. Returns the acquired range, hold ID and expiry. |
| `DELETE /api/court-selections/{holdId}/slots/{slotId}` | Releases one range. |
| `DELETE /api/court-selections/{holdId}` | Releases the entire selection. |
| `POST /api/bookings` | Accepts optional `selectionHoldId` and `ownerSessionId` for checkout. Existing callers can still book an unheld range through this endpoint. |

Both release routes take `branchId`, `businessDate` and `ownerSessionId` in the query string. Claim and release require a UUID in `Idempotency-Key`. A retry with the same subject, key and payload replays the receipt. A different payload for that key returns `IDEMPOTENCY_KEY_REUSED`. Use the same key after a transport failure. Guests send the credential in `X-Court-Selection-Guest` for snapshot, claim, release and checkout. A signed-in call binds ownership to the authenticated account and tab UUID. The public snapshot omits the hold ID for other sessions. The public booking schedule masks booking IDs and customer fields. The staff schedule requests customer fields with `includeCustomerData=true`, which requires branch-scoped booking view permission.

Selection errors include `SLOT_NOT_AVAILABLE`, `SLOT_IN_PAST`, `SLOT_BLOCKED`, `SLOT_ALREADY_BOOKED`, `HOLD_EXPIRED`, `HOLD_SLOT_LIMIT_EXCEEDED`, `HOLD_LIMIT_EXCEEDED` and `IDEMPOTENCY_KEY_REUSED` as HTTP 409. Missing ranges or holds return 404. Ownership mismatch returns 403, invalid request fields return 400, and the mutation rate limiter returns 429. A stale or invalid hold at checkout uses the existing booking conflict mapping.

## Deployment

| `BookingHolds` setting | Default |
| --- | --- |
| `SelectionHoldMinutes` | 5 |
| `CleanupIntervalSeconds` | 10 |
| `MaximumSlotsPerHold` | 8 |
| `MaximumActiveHoldsPerUser` | 1 per authenticated or protected guest subject |
| `IdempotencyRetentionMinutes` | 10 |

Use HTTPS for browser APIs. Configure `Cors:AllowedOrigins` for a separate frontend origin. Route `/hubs/court-status` including WebSocket upgrades to the API. Persist the ASP.NET Data Protection key ring using `DataProtection:KeyDirectory` or the host's persistent key store. Replicas need the same protected key ring to validate guest credentials.

SignalR group names are `court-status:{branchId:N}:{businessDate:yyyy-MM-dd}`. `SlotHeld`, `SlotReleased`, `SlotsBooked` and `CourtStatusInvalidated` carry board coordinates and prompt snapshot reconciliation. The API sends them directly after commit and the worker polls expired rows with `FOR UPDATE SKIP LOCKED` in batches of 100. The frontend cancels stale reads and fetches current state after joining or rejoining. Live messages are invalidations so reordered delivery cannot restore an old slot value. Polling provides recovery after a missed notification. This implementation uses in-process SignalR. A multi-replica deployment needs managed SignalR scale-out or a backplane for timely cross-replica delivery.

Subscribe to the `CourtBookingManagement.CourtSelection` meter for mutation outcomes, expired hold counts, publication failures and cleanup duration. Logs include correlation ID, outcome, and available hold and slot IDs.

## Verification

Run the backend integration tests against a disposable PostgreSQL server. The test creates and removes its own isolated database.

```powershell
$env:COURT_SELECTION_TEST_PG = 'Host=localhost;Database=postgres;Username=postgres'
dotnet test tests/CourtBookingManagement.CourtSelection.Tests
```

From the frontend repository, run `npm run test:court-selection`. For Playwright, start both applications, then set `COURT_SELECTION_E2E_URL`, `COURT_SELECTION_E2E_BRANCH` and `COURT_SELECTION_E2E_DATE` to an active disposable branch with a future free slot. The optional `COURT_SELECTION_E2E_COURT` and `COURT_SELECTION_E2E_TIME` default to `Sân E2E` and `07:00`. Run `npm run test:court-selection:e2e`. Supplying `COURT_SELECTION_E2E_FIXTURE_COURT_ID` lets the test use fixture data for branch discovery and the legacy schedule read while exercising the live selection API, PostgreSQL and SignalR.

For a manual check, open the same branch and future date in two independent browser contexts. Press Enter on an available slot in A. Expect the selected label in A and a disabled gray cell in B. Select an adjacent slot and check that the countdown retains its original expiry. Release a slot in A and check that B can select it. Disconnect B, change the board in A and reconnect B to confirm reconciliation. Repeat after expiry and after checkout. Use a disposable court/date so the test booking does not affect customer inventory.

## Changed files

Paths below are relative to their respective repositories.

| Backend paths | Purpose |
| --- | --- |
| `Database/Migrations/20261125_AddCourtSelectionHolds.sql` | Postgres schema, indexes and shared booking overlap trigger. |
| `src/CourtBookingManagement.Api/Controllers/CourtSelectionsController.cs` | Snapshot, guest credential, claim and release endpoints. |
| `src/CourtBookingManagement.Api/Controllers/BookingsController.cs` | Verified checkout ownership and post-commit board notification. |
| `src/CourtBookingManagement.Api/Controllers/CourtStatusController.cs`, `src/CourtBookingManagement.Api/Common/Constants/Permissions.cs` | Public schedule redaction and branch-scoped staff access. |
| `src/CourtBookingManagement.Api/Realtime/CourtSelectionIdentity.cs`, `src/CourtBookingManagement.Api/Realtime/CourtSelectionRealtime.cs` | Browser ownership, hub groups, publication, expiry worker and metrics. |
| `src/CourtBookingManagement.Api/Program.cs`, `src/CourtBookingManagement.Api/appsettings.json` | Registration, throttling and configuration. |
| `src/CourtBookingManagement.Application/Bookings/DTOs/CourtSelection.cs`, `src/CourtBookingManagement.Application/Bookings/DTOs/CreateBookingRequest.cs` | Selection and checkout contracts. |
| `src/CourtBookingManagement.Application/Bookings/Interfaces/IBookingRepository.cs`, `src/CourtBookingManagement.Application/Bookings/Interfaces/IBookingService.cs`, `src/CourtBookingManagement.Application/Bookings/Services/BookingService.cs` | Validated ownership through checkout and existing booking idempotency. |
| `src/CourtBookingManagement.Infrastructure/Repositories/CourtSelectionRepository.cs`, `src/CourtBookingManagement.Infrastructure/Repositories/BookingRepository.cs`, `src/CourtBookingManagement.Infrastructure/Repositories/CourtStatusRepository.cs` | Dapper transactions and board refreshes. |
| `tests/CourtBookingManagement.CourtSelection.Tests/CourtBookingManagement.CourtSelection.Tests.csproj`, `CourtSelectionTests.cs`, `CourtSelectionIdentityTests.cs` in that directory | PostgreSQL, identity and group tests. |
| `docs/realtime-court-selection.md` | Migration, rollout and verification notes. |

| Frontend paths | Purpose |
| --- | --- |
| `src/features/court-status/hooks/useRealtimeCourtSelection.ts`, `src/lib/court-selection-session.ts` | Snapshot and realtime reconciliation, tab session and guest credentials. |
| `src/features/court-status/components/CourtScheduleBoard.tsx`, `src/features/court-status/components/CourtSelectionStatus.tsx`, `src/features/court-status/utils/time-selection.utils.ts` | Accessible slot states, expiry indicator and range overlap display. |
| `src/features/customer-court-status/pages/CustomerCourtStatusPage.tsx`, `src/features/court-status-board/pages/CourtStatusPage.tsx` | Hold-on-click and binding of the booking dialog to its selection. |
| `src/features/court-status-board/hooks/useCourtStatus.ts`, `src/services/court-status.service.ts` | Scoped staff schedule retrieval and query cache. |
| `src/features/bookings/api/booking.api.ts`, `src/features/bookings/types/booking.types.ts`, `src/features/bookings/utils/booking.mapper.ts` | Pass selection ownership through the existing checkout. |
| `src/features/court-status/hooks/useRealtimeCourtSelection.test.tsx`, `tests/court-selection.spec.ts` | React and two-browser coverage. |
| `package.json`, `package-lock.json`, `vite.config.ts` | SignalR/test dependencies, test scripts and development WebSocket proxy. |
