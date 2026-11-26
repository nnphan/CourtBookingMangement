# Match room implementation checkpoints

## Phase 1 — Repository discovery

The API depends on Application and Infrastructure. Application depends on Domain and owns matching contracts, validation, and service interfaces. Infrastructure implements repositories with Dapper and Npgsql through `ISqlConnectionFactory`. Matching writes share the transaction opened by the application service. API controllers use the existing success/error envelopes and pagination metadata, and derive the actor from the authenticated current-user service.

Reuse `auth.users`, the role and permission repositories and branch authorization service, `customer.customers`, `core.branches`, `core.courts`, `booking.bookings`, `booking.booking_details`, `media.files`, and the notification repository. Matching tables already cover rooms, participants, join requests, and notifications. The checked-in November 18 SQL migration extends those tables and adds profiles, rating history, preferences, and preferred branches. Migrations are transactional SQL deployment scripts. The booking eligibility query requires a confirmed, active, undeleted booking owned by the authenticated customer's user and a confirmed detail covering the room's branch, court and interval. The join and review paths already lock the room before changing capacity. Booking hold expiration and court selection workers establish the hosted-service pattern.

Reuse the existing React Router destinations `/player-matching`, `/player-matching/:id`, `/my-matches`, and `/my-player-matching-profile`. The React/Vite app uses Axios, TanStack Query, React Hook Form, Zod, shared Tailwind and Radix controls, i18next, and dayjs. Discovery, booking/create dialogs, room detail, profile, hosted/participating workspace, and request dialogs already exist. Existing responsive layouts use Tailwind breakpoints and labeled form controls. Vitest and Playwright are dependencies.

Retain the `/api/player-matches` route family for discovery, create, details, draft/publish/cancel, hosted/participating lists, and join requests. Own-profile and preference operations use `/api/me/player-matching-profile` and `/api/me/match-preferences`. Applicant history uses `/api/me/match-join-requests`. Draft/publish flows, the existing rating model, and the current API names differ from the brief. Avoid inferring a new skill level from historical ratings without an explicit player or host choice.

## Phase 2 — Contract impact

The existing API contract remains the reference for legacy callers. The requested MVP requires explicit skill levels, start-time distance, 40/30/20/10 defaults, and a score breakdown showing server-calculated contributions. The existing numeric self-assessments, stored preferences, and legacy rooms need compatibility when the new scoring rules are introduced. Proposed implementation areas are the existing profile, room and scoring DTOs and services, their PostgreSQL repositories, API rate-limit policy and controllers, and the existing matching types, query hooks and screens. A forward SQL migration is needed for any new persisted data or schema defaults. The existing application has no down-migration convention. Verify the November 18 migration and audit legacy data before applying a schema extension.

Phase 1 status is completed. Phase 2 is partial pending implementation of the revised scoring contract and compatibility verification.

## Verification and remaining checkpoints

| Phase | Status | Result |
|---|---|---|
| 3 | Partial | Both matching migrations applied to a minimal PostgreSQL 18 fixture. Seven matching tables and all four weight defaults were verified with information_schema. Deployment data and narrow permission seeds remain unverified. |
| 4–5 | Partial | Existing own-profile and booking-backed room flows were retained. Explicit four-level profiles and booking-only create/update contracts remain pending. |
| 6–7 | Partial | Ranking uses start-time distance and primary/secondary branch scores. Contributions are server-calculated and discovery excludes inconsistent member counts. The existing rating-range skill policy, minimum-overlap filter, and score thresholds still apply. |
| 8 | Partial | Approval checks ownership before reading the request and maps final-slot contention to MATCH_ROOM_FULL for pending or expired requests. End-to-end concurrent approvals remain unverified. |
| 9–12 | Partial | Discovery shows server-calculated contributions and synchronized weight controls with reset and invalid-total disabling. Existing routes and dialogs remain in use. The new filter regression and five existing court-selection UI tests passed. Matching E2E cases remain pending. |
| 13 | Partial | Matching writes have an authenticated fixed-window policy. The worker locks up to 100 candidates per pass using SKIP LOCKED and expires pending requests in the same SQL statement as the room transition. PostgreSQL verified expiry, invalid-booking cancellation, and preservation of a future room. |
| 14 | Blocked | The backend solution build hit a pre-existing StaffInvitationState constructor error. Frontend production build and lint report existing failures outside the matching changes. The available court-selection Playwright scenario skipped. |
| 15 | Partial | The API contract and this impact map document the delivered changes. The full production definition of done remains open. |

For manual verification, use a database with both matching migrations applied. Sign in as a customer with a valid owned confirmed booking and publish an existing-style draft. Search as a second eligible customer, change a single weight and confirm search disables, then reset and inspect the contribution math. Submit two eligible requests to a room with one remaining slot and approve concurrently from host sessions. Verify one membership insert, a FULL room, an expired losing request and a conflict response to the losing approval. Cancel a linked booking and verify the room and pending requests transition during the next cleanup pass.
