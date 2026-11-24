# Staff invitation and SMTP contract

Status: **Phases 2–8 backend work and Phases 9–10 frontend activation work
implemented**. Do not deploy or enable delivery of version-2 invitations
until the migration and complete frontend activation flow are deployed and
verified.

The shared parent `BE_FE/docs` directory is not in a Git repository, so this
contract is kept in the existing backend documentation directory.

## Decisions

- Development provider: the existing configurable SMTP relay, using STARTTLS
  on port 587. The relay host, sender and credentials are deployment inputs;
  none are assumed or stored here. Production must use an approved provider.
- Reuse `auth.users`, `auth.branch_user_roles`, `auth.staff_invitations`,
  existing branch authorization, BCrypt, response envelopes and the existing
  invitation delivery infrastructure. Creation and resend use the shared
  `IEmailSender`; legacy activation retains `IStaffInvitationDelivery`. Do not introduce a second account,
  authentication, invitation or notification system.
- Send directly after commit for the MVP. There is no existing email outbox.
  An outbox is a later decision, and must not persist a raw token without an
  approved encryption, retention and key-management design.
- Invitation lifetime defaults to 24 hours, configurable from 1 to 168 hours.
  Resends have a five-minute cooldown and a maximum of three resends per
  rolling 15-minute window; all limits are configurable and enforced in the
  database as well as any applicable HTTP rate limiter.
- The new public frontend route is `/staff/activate`. New emails use
  `{FrontendActivationUrl}?token={encodedToken}&email={encodedEmail}`.
  Production URLs require HTTPS; HTTP is allowed only for a loopback
  development URL. Build the query with a URL builder.
- Existing issued fragment links are supported temporarily as described in
  [Compatibility and rollout](#compatibility-and-rollout). They must not be
  confused with the new email-bound contract.
- Password policy: the current Staff activation rule becomes the shared rule
  for new registration and activation, and for password reset when implemented:
  at least 12 characters, at most 72 UTF-8 bytes, and at least three of
  uppercase, lowercase, digits and symbols. Use the existing BCrypt mechanism.
  Existing passwords and login remain valid; no password is emailed or
  returned. Registration becomes stricter for newly registered users.

## Configuration and secrets

Continue using the existing `StaffInvitations` configuration namespace rather
than creating a competing SMTP configuration. Its options are:

| Key | Meaning |
|---|---|
| `StaffInvitations:ActivationUrl` | Absolute frontend activation URL, without query or fragment |
| `StaffInvitations:ExpirationHours` | Invitation lifetime; default 24 |
| `StaffInvitations:ResendCooldownMinutes` | Default 5 |
| `StaffInvitations:MaxResendsPerWindow` | Default 3 |
| `StaffInvitations:ResendWindowMinutes` | Default 15 |
| `StaffInvitations:Smtp:Enabled` | Enables delivery and startup validation |
| `StaffInvitations:Smtp:Host` | Approved relay host |
| `StaffInvitations:Smtp:Port` | 587 for the selected development relay |
| `StaffInvitations:Smtp:Security` | `StartTls` for the selected development relay |
| `StaffInvitations:Smtp:Sender` | Valid sender address |
| `StaffInvitations:Smtp:FromName` | Display name |
| `StaffInvitations:Smtp:Username` | SMTP authentication username |
| `StaffInvitations:Smtp:Password` | SMTP authentication secret |
| `StaffInvitations:Smtp:TimeoutSeconds` | Default 15; allowed 5–60 |

When enabled, startup validates host, port, supported transport mode, sender,
both authentication values, timeout, activation URL and invitation limits.
The sender uses MailKit with STARTTLS by default, a per-send SMTP client and a
bounded timeout. New create invitations use the email-bound query link and
HTML/plain-text template. Missing or disabled delivery is reported before a
create or resend transaction starts.

Use .NET User Secrets locally and environment variables or the approved secret
store in production. The API project has a `UserSecretsId`; configure only
local development secrets from the backend root, for example:

```powershell
dotnet user-secrets set "StaffInvitations:Smtp:Enabled" "true" --project src\CourtBookingManagement.Api\CourtBookingManagement.Api.csproj
dotnet user-secrets set "StaffInvitations:Smtp:Host" "<relay-host>" --project src\CourtBookingManagement.Api\CourtBookingManagement.Api.csproj
dotnet user-secrets set "StaffInvitations:Smtp:Sender" "<sender-address>" --project src\CourtBookingManagement.Api\CourtBookingManagement.Api.csproj
dotnet user-secrets set "StaffInvitations:Smtp:Username" "<relay-username>" --project src\CourtBookingManagement.Api\CourtBookingManagement.Api.csproj
dotnet user-secrets set "StaffInvitations:Smtp:Password" "<relay-password>" --project src\CourtBookingManagement.Api\CourtBookingManagement.Api.csproj
dotnet user-secrets set "StaffInvitations:ActivationUrl" "http://localhost:5173/staff/activate" --project src\CourtBookingManagement.Api\CourtBookingManagement.Api.csproj
```

Port 587, `StartTls`, a 15-second timeout and the invitation limits in the
table above have safe defaults. Do not put credentials in configuration files,
logs, examples, responses or source control. For Gmail, an App Password is
required; Microsoft 365 may require mailbox/tenant SMTP AUTH configuration.
Do not weaken organization security settings. A development-only smoke endpoint
is available at `POST /api/dev/smtp-smoke-test`; it requires an `ADMIN` token,
is rate-limited, does not appear in Swagger, and returns 404 outside Development.
Use it only with an explicitly approved recipient and configured relay; the
example request is in `src/CourtBookingManagement.Api/smtp-smoke-test.http`.

## Invitation persistence and tokens

The existing `auth.staff_invitations` table is reused. Its unique token-hash
index and outstanding-user/purpose index, `used_at` and `revoked_at` fields,
expiry and delivery-state columns remain the persistence foundation. Migration
`Database/Migrations/20261124_StaffInvitationTokenInfrastructure.sql` adds:

- `link_version`: existing rows default to `1` (legacy hexadecimal fragment
  tokens); new create invitations use `2` for email-bound Base64URL links.
- `issuance_type`: `CREATE` or `RESEND` when known; existing rows remain `NULL`
  and must be treated conservatively as resends for future rolling limits.
- A partial index supporting resend-window queries and constraints for the
  supported link-version and issuance-type values.

The registered `IStaffInvitationTokenService` generates 32 cryptographically
random bytes as unpadded Base64URL, validates the canonical token encoding,
and hashes the exact token text with SHA-256 as 64 uppercase hexadecimal
characters. Only that hash is suitable for persistence. The
`IStaffInvitationRepository` reads invitation state using one PostgreSQL
`clock_timestamp()` value per query; it does not return the hash or raw token.
The existing legacy activation transaction now reads database time after
locking the user and invitation rows and uses that same value for expiry and
state updates.

New create writes are explicitly marked `link_version = 2` and
`issuance_type = CREATE`; resends use version 2 and `RESEND`. The current
legacy activation endpoint accepts only the legacy token format. The migration
has not been applied to a database; apply it before deploying code that reads
or writes the new columns.

## Email composition

`StaffInvitationActivationUrlBuilder` creates the new email-bound link as
`{ActivationUrl}?token={encodedToken}&email={encodedEmail}`. The URL must not
already contain a query or fragment. `StaffInvitationEmailComposer` returns
both HTML and plain-text bodies, escapes display and branch names, includes
the Vietnam-local expiry, one-time-use guidance and unexpected-email guidance,
and does not add tracking parameters. It does not log the URL or token.

The create and resend flows use `StaffInvitationEmailComposer` and the shared
`IEmailSender`; each supplies the normalized recipient, generated token,
display name, assigned owned-branch names and the same configured expiry that
is persisted with the invitation. A real email-client rendering or SMTP
delivery check has not been run.

## Account, invitation and transaction semantics

- Resolve the authenticated owner through `core.owners.user_id`, verify active
  owned branches and the relevant `staff.create` permission, and assign only
  the existing `STAFF` branch role. Existing user email/phone conflicts remain
  generic and must not attach an unrelated user.
- A new account is inactive and unverified. `PENDING_ACTIVATION`, `EXPIRED`,
  `ACTIVE` and `INACTIVE` are derived from account and invitation state, not a
  new user-status table. Login continues to require `is_active`.
- Generate 32 cryptographically random bytes; encode new tokens as unpadded
  Base64URL. Store only SHA-256 of the exact token in the existing unique
  `token_hash` column. Never log the token, full URL, password, password hash,
  email body or SMTP credentials.
- Create user, assignments and invitation atomically. Send email only after
  commit with a bounded timeout. An SMTP delivery failure leaves the account
  and invitation intact and returns the existing successful creation envelope
  with `invitationDeliveryStatus: "FAILED"` and a safe warning.
- Preserve `CreateStaffResponse` (`userId`, `invitationDeliveryStatus`,
  `warningCode`) and the existing owner create/resend routes. If post-commit
  delivery-state persistence fails, report the committed outcome and
  `INVITATION_STATUS_PERSISTENCE_FAILED` rather than claiming rollback. If the
  process or connection fails after commit, the client must treat the outcome
  as unknown and check the staff list before retrying creation.
- Activation and resend must lock the user and relevant invitation rows in a
  consistent order. After acquiring locks, use a single database time reading for
  expiry and resend decisions. Activation verifies purpose, token hash,
  normalized email for new links, pending account state, an active `STAFF`
  assignment and absence of protected roles. Set the BCrypt password hash,
  activate/verify the user, consume the invitation and revoke any other active
  invitations in the same transaction. Concurrent redemption must allow exactly
  one success.
- A resend verifies owner scope and target eligibility, checks limits under
  the same user lock, revokes prior unused invitations and inserts the new
  hash before committing. Delivery occurs after commit; a failed delivery can
  therefore leave old links revoked. The cooldown applies after the latest
  issuance, including initial creation; the rolling maximum counts resends.
  Persist enough issuance metadata to distinguish creates from resends.
  Legacy rows whose origin is unknown count conservatively as resends until
  they leave the rolling window. Rejected attempts do not create or revoke
  invitations. The database-backed cooldown/window errors include
  `Retry-After`; an additional per-IP ASP.NET rate limit protects the resend
  endpoint.
- Per-IP activation throttling remains necessary, with trusted proxy
  configuration and a distributed/edge limit for multiple instances.
  Database-backed resend limits must not depend solely on in-process limits.

## API contract

All paths below are under `/api` and use the existing `ApiResponse<T>` and
`ApiErrorResponse` envelopes. The email-bound validation and activation
endpoints are implemented; the owner resend route remains on its existing
compatibility flow.

### Validate

`POST /auth/staff-invitations/validate` is anonymous and rate-limited. It
accepts only canonical version-2 tokens and returns details only when the
normalized email matches the invitation:

```json
{ "token": "one-time-token", "email": "staff@example.com" }
```

For a valid invitation, `data` contains `valid`, `displayName`, `branchNames`
and UTC `expiresAt`; it never includes IDs, hashes or credentials. Validation
does not consume or extend the invitation. It is advisory: activation repeats
all checks under lock.

Unknown tokens and email mismatches return the same generic invalid response.
For a matching high-entropy token and normalized email only, the server may
return the stable expired, used or revoked state codes needed for localized UI
guidance, without returning account details. All such failures use HTTP 400 and
a generic public message; do not reveal whether an arbitrary email has an
account. The UI offers contact-owner guidance rather than disclosing secrets.

### Activate

`POST /auth/staff-invitations/activate` is anonymous and rate-limited. It
accepts only version-2 tokens, requires a matching email and password
confirmation, and repeats eligibility, email, expiry and one-time-use checks
inside the activation transaction. The legacy `POST /auth/staff-activation`
route remains available for version-1 tokens only.

```json
{
  "token": "one-time-token",
  "email": "staff@example.com",
  "password": "user-supplied-password",
  "confirmPassword": "user-supplied-password"
}
```

The server validates password confirmation and the shared policy, then performs
the atomic transition above. Return success without credentials or automatic
login. Password errors are safe validation errors and never echo input.

### Owner resend

`POST /branch-owner/staff/{userId}/send-activation` remains authenticated and
owner-scoped. It returns the existing delivery result shape after issuance.
Only eligible inactive, unverified Staff accounts may be resent; active Staff
receive `STAFF_ALREADY_ACTIVE`. Preserve generic `STAFF_NOT_FOUND` for
unmanageable targets. The current implementation also requires the caller to
have created the account, excludes active foreign-owner assignments, and checks
`staff.create` on every active owned branch; preserve these restrictions unless
an explicitly reviewed replacement enforces an equivalent or stricter scope.
Return 403 for denied scope, 409 for an authorized already-active state, 429
with `Retry-After` for cooldown/window limits and 503 for unavailable
configuration before issuance. SMTP failure after commit is a 200 response
with `FAILED`, not a rollback-shaped error.

Implemented activation errors include `STAFF_INVITATION_INVALID`,
`STAFF_INVITATION_EXPIRED`, `STAFF_INVITATION_ALREADY_USED`,
`STAFF_INVITATION_REVOKED`, `PASSWORD_POLICY_FAILED` and
`PASSWORD_CONFIRMATION_MISMATCH`. Resend errors include `STAFF_ALREADY_ACTIVE`,
`INVITATION_RESEND_TOO_SOON` and `INVITATION_RATE_LIMITED`. Error messages must not include
input tokens, passwords, private SMTP details or account data.

## Compatibility and rollout

The legacy sender emitted 64-character hexadecimal tokens in a URL fragment,
and the existing `POST /auth/staff-activation` accepts `token` and
`newPassword`, without email. Preserve already-issued links temporarily; do
not silently reinterpret them as the new email-bound format.

- Persist a link/issuance version with each invitation; migrate existing rows
  as legacy. The legacy endpoint accepts only legacy invitations; the new
  endpoints accept only new invitations and require a matching email. This
  prevents new email-bound invitations from bypassing the email check through
  the legacy endpoint. Legacy hash normalization remains compatible with
  current uppercase hexadecimal tokens; new Base64URL tokens are case-sensitive.
- The frontend recognizes old fragment links at the previously configured
  activation URL. Because those links contain no email, the temporary legacy
  path uses the existing token-only activation contract and does not invent an
  email or display an unverified invitation summary. If the old URL differs
  from `/staff/activate`, deployment must route that old URL to the compatible
  frontend during the grace period.
- Roll out database and legacy/new endpoint compatibility to every API instance
  before issuing new-format invitations. Stop old binaries from issuing legacy
  links, then retain legacy redemption until the last legacy invitation has
  expired or been revoked. Only then disable the legacy endpoint and URL
  support. Do not assume there are no outstanding invitations.

## URL, frontend and email safety

Query tokens reach the frontend host, reverse proxy and CDN on the initial
request, before React can clean the URL. Deployment is blocked until every
such layer redacts query strings for the activation route and avoids storing
full request URLs. Configure a restrictive `Referrer-Policy` and
`Cache-Control: no-store`; prevent analytics, error reporting and third-party
requests from recording or receiving the URL. Do not rely on referrer policy
or client-side cleanup to protect initial server access logs.

Extract token and email into page-local memory, immediately replace the
browser history entry with the clean route, and never write tokens to browser
storage, persistent state, analytics or long-lived query caches. Activation
mutation variables containing credentials must be removed immediately after
settlement and not persisted by query tooling. The public page provides
validation/loading, password requirements and confirmation, safe invalid or
expired guidance, success and navigation to the existing login route. Do not
automatically sign in.

The email must include escaped display and owned-branch names, a primary link,
plain-text fallback, Vietnam-local expiry, one-time-use and unexpected-email
guidance. Supply HTML and plain-text alternatives; never include a password,
internal IDs or token-bearing tracking parameters.

## Verification and delivery limitations

Before completion, use an explicitly isolated PostgreSQL database to verify
migration, owner scoping, conflict rollback, concurrent activation, database
resend limits, revoked/expired/used links and legacy/new separation. Use a
configured development relay and approved recipient to verify delivery and a
safe failure simulation. Verify keyboard/mobile behavior and secret-free
logging.

The Phase 8 API build and all 61 API test-project tests passed locally, but the
migration has not been applied and PostgreSQL locking/concurrency behavior has
not been verified against a database. SMTP has not been sent. The frontend
activation flow is implemented, but browser testing with a real backend and
the production build remain unverified. Do not deploy the version-2 create or
resend paths until the migration and frontend activation flow are deployed
and verified with these endpoints.

Direct SMTP after commit cannot guarantee eventual delivery if the process
stops, and an in-flight older email may arrive after a later resend has revoked
its link. Expose delivery outcomes, instruct the recipient to use the newest
valid invitation, and monitor safe failure metadata. Assess a properly secured
outbox later, including idempotency, concurrency, retries, token protection and
retention; it is not part of this phase.
