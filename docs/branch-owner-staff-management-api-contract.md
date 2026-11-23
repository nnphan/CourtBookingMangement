# Branch Owner Staff Management API Contract

This is the initial Phase 2 contract for branch-scoped Staff management. Backend DTOs and Swagger become authoritative when endpoints are implemented; update this document and the frontend API types together.

## Status and decisions

- Activation mode: secure email invitation through the existing SMTP relay. Delivery uses a provider-neutral application interface; configuration and the frontend activation page are deployment prerequisites. Do not report a message as sent when no sender is configured.
- A Staff account is an existing `auth.users` account with the `STAFF` role assigned through `auth.branch_user_roles`. Do not create a second Staff user or role-assignment table.
- A Staff account may be assigned to multiple branches owned by the creating Branch Owner. Do not attach an existing user by email or phone.
- `email` is immutable through Staff update because no verified email-change flow exists.
- Staff-code and employment metadata are branch-assignment data. For this API version, one submitted set of assignment metadata is applied consistently to each branch in the request.
- Removing the final active branch assignment is rejected. Deactivate the Staff account explicitly instead.
- Deactivating a Staff account sets `auth.users.is_active = false`, revokes active refresh tokens, and closes active sessions. It does not delete the user, assignments, or business history.
- Owners can reactivate an account only after it has completed activation. An owner cannot bypass pending activation by activating the account directly.
- Staff activation sets a password through the activation flow. Owners cannot set or view Staff passwords.
- No payroll, shifts, attendance, arbitrary role assignment, hard deletion, or cross-owner user assignment is included.

## Authorization and tenant scope

Every Staff request must be authenticated and must satisfy both a Staff-management permission and Branch Owner scope on the server:

1. Resolve the caller from the authenticated user ID.
2. Resolve one active, non-deleted `core.owners` record whose `user_id` is the caller.
3. Restrict the operation to active, non-deleted branches whose `owner_id` is that Owner record.
4. Require the action-specific `staff.*` permission for the relevant branch scope.
5. For a Staff target, prove that the target has a `STAFF` assignment in the caller's branch scope and does not have a protected role.

Use the existing branch authorization mechanism rather than a frontend-only role check. `BranchAuthorizationService` currently resolves an owner to effective `BRANCH_ADMIN` permissions for owned branches. The implementation must check that permission for every requested branch; it must not rely on `[Permission]` claim checks alone, because the current permission service only places global-role permissions in JWT claims.

The checked-in sources have a role-code mismatch: branch authorization and the POS migration use `BRANCH_ADMIN`, while a branch lookup query also references `BRANCH_MANAGER`. The Phase 3 migration targets `BRANCH_ADMIN`, matching the current owner authorization path, and requires active `BRANCH_ADMIN` and `STAFF` roles. Confirm the role codes against deployed data before applying the migration; reconcile the `BRANCH_MANAGER` lookup discrepancy separately. Do not introduce a new authorization system or assume a global `BRANCH_OWNER` role is authoritative.

The frontend Staff route and navigation share an access query that requests the first Staff list page. The route and dashboard recheck on mount; the sidebar checks when opened. A forbidden response denies access and other failures are surfaced separately. These checks do not replace backend authorization. The current list is read-only; account creation and management actions remain for later phases.

The migration extends existing branch-role rows in place and leaves their assignments and account states unchanged. It does not infer owners or backfill legacy Staff accounts. Reads include eligible existing Staff assigned to owned branches regardless of who created their accounts.

Writes may only manage Staff accounts created for that Owner. New accounts are created only when the submitted email and phone are not already used. Existing-user assignment is not supported. Never manage a user holding `ADMIN`, `SUPER_ADMIN`, `BRANCH_ADMIN`, `BRANCH_OWNER`, `BRANCH_MANAGER`, or `CUSTOMER` as a Staff target; never accept role or permission fields from a client.

## Permissions

| Permission | Operations |
|---|---|
| `staff.view` | List and read Staff in owner scope |
| `staff.create` | Create a Staff account and initial branch assignments |
| `staff.update` | Edit allowed personal and assignment fields |
| `staff.activate` | Reactivate a previously activated, inactive Staff account |
| `staff.deactivate` | Deactivate Staff and revoke refresh tokens/sessions |
| `staff.assign_branch` | Add or replace owner-scoped branch assignments |
| `staff.remove_branch` | Remove a Staff assignment from an owned branch |
| `staff.reset_password` | Send a password-reset email to an activated Staff account |

These are branch-scoped permissions. Do not grant them to the global `STAFF` role. The Phase 3 migration grants them only to the effective `BRANCH_ADMIN` role; the authorization service must still verify that the caller owns the target branch. The Phase 4 read routes verify active Owner scope and call branch authorization with `staff.view` for each included branch.

## Account and assignment lifecycle

### Account states

- `PENDING_ACTIVATION`: account is inactive and has an outstanding, unexpired activation invitation.
- `ACTIVE`: account is active and has completed activation.
- `INACTIVE`: account was activated previously and was subsequently deactivated.
- `EXPIRED`: activation invitation expired before activation; the account remains inactive until a new invitation is sent.

`auth.users.is_active` remains authoritative for whether login is permitted. Activation status is derived from account state and invitation records; it is not a replacement account-status table.

Allowed transitions:

```text
new account -> PENDING_ACTIVATION
PENDING_ACTIVATION -> ACTIVE     (Staff redeems activation invitation)
PENDING_ACTIVATION -> EXPIRED    (invitation expires; resend can issue a new invitation)
ACTIVE -> INACTIVE               (authorized Branch Owner deactivates)
INACTIVE -> ACTIVE               (authorized Branch Owner reactivates)
```

Only the existing security/admin flow may lock an account. Branch assignment removal never hard-deletes a User or business history.

### Branch assignments

The existing `auth.branch_user_roles` row represents one user's role in one branch. `STAFF` is assigned there, not through a client-controlled global role list. The existing `(branch_id, user_id)` uniqueness means the same Staff user can have one assignment in each of multiple branches.

Assignments are soft-removed by marking the row inactive and recording removal audit fields. Re-adding a Staff member to the same branch reactivates the existing row. A request may not leave the Staff account with zero active assignments; attempts to remove the final active assignment return `LAST_ACTIVE_BRANCH_REMOVAL_NOT_ALLOWED`.

## Activation and notification

- Generate a cryptographically random, single-use token with at least 256 bits of entropy.
- Store only a one-way hash of the token. Never return it in an API response, log it, or put it in query cache.
- Activation invitations expire after 24 hours. Password-reset invitations expire after 1 hour.
- A newly created User is inactive and unverified. It receives only `STAFF` branch assignments. The service stores a randomly generated, undisclosed password hash until the Staff member redeems the activation link. Activation applies the password policy described below.
- Reissuing an invitation revokes prior unused tokens for the same purpose. Redemption is atomic and consumes the token once.
- Commit User, assignment, and invitation records before sending email. Email delivery must happen outside the database transaction.
- Persist a delivery state (`PENDING`, `SENT`, or `FAILED`) and a safe failure code. If sending fails, the account remains created but inactive; the response explicitly reports delivery failure, and the owner can retry by requesting a new invitation. Do not include an activation token in the response. A process interruption after commit may leave `PENDING`; resend revokes the outstanding token and issues a new one.
- Implement a provider-neutral sender adapter and make provider/configuration a deployment prerequisite. No email provider is currently configured in the checked-in application.
- Activation is public but rate-limited and returns a generic invalid/expired-token error. It accepts a token and a new password; it does not accept a user ID, role, or branch list.

## Routes

All routes below are under `/api`. Successful responses use the existing `ApiResponse<T>` envelope. List responses put the array in `data` and paging information in `metadata`, consistent with the existing user-list endpoint.

| Method | Route | Permission | Behavior |
|---|---|---|---|
| GET | `/branch-owner/staff` | `staff.view` | List Staff with owner-scoped search, filters, sorting, and paging |
| GET | `/branch-owner/staff/branches` | `staff.view` | List active owned branches available for the Staff filter |
| GET | `/branch-owner/staff/{userId}` | `staff.view` | Read one manageable Staff account and only its assignments in owner scope |
| POST | `/branch-owner/staff` | `staff.create` | Atomically create an inactive User, STAFF branch assignments, and activation invitation |
| PUT | `/branch-owner/staff/{userId}` | `staff.update` and `staff.assign_branch` | Update allowed fields and replace the caller's branch assignment set transactionally |
| POST | `/branch-owner/staff/{userId}/activate` | `staff.activate` | Reactivate a previously activated, inactive Staff account |
| POST | `/branch-owner/staff/{userId}/deactivate` | `staff.deactivate` | Deactivate account, revoke refresh tokens, close sessions |
| POST | `/branch-owner/staff/{userId}/send-activation` | `staff.create` | Reissue an activation invitation for a pending/expired account |
| POST | `/branch-owner/staff/{userId}/send-password-reset` | `staff.reset_password` | Send a password-reset invitation; does not activate an inactive account |
| DELETE | `/branch-owner/staff/{userId}/branches/{branchId}` | `staff.remove_branch` | Soft-remove an assignment; reject removing the final active assignment |
| POST | `/auth/staff-activation` | Anonymous | Redeem one-time invitation and set the password |

The update endpoint's two permissions are both required. Every submitted branch ID must be owned by the caller. Updating the assignment set affects only the caller's assignments; it must not modify another owner's rows. Current implementation must also enforce the contract's no-cross-owner account rule.

The list, branch lookup, detail, create, resend-activation, and staff-activation routes are implemented. The other routes remain contract-only. Read requests with no active owned branches return 403 because no branch permission can be established.

The branch lookup returns `data` as an array of `{ "branchId": "uuid", "branchName": "name", "canCreateStaff": true }`, including active owned branches even when they have no Staff assignments. It requires `staff.view` for each returned branch; `canCreateStaff` reflects the branch's `staff.create` permission and is rechecked when creating an account.

## List query and response

Query parameters:

| Parameter | Rules |
|---|---|
| `branchId` | Optional; if supplied, must be an active branch owned by the caller |
| `search` | Optional; match display name, email, phone, or staff code |
| `status` | Optional: `ACTIVE` or `INACTIVE` |
| `activationStatus` | Optional: `PENDING_ACTIVATION`, `ACTIVE`, `INACTIVE`, or `EXPIRED` |
| `sortBy` | Whitelist: `displayName`, `email`, `staffCode`, `status`, `createdAt`, `lastLoginAt` |
| `sortDirection` | `ASC` or `DESC`; default `DESC` |
| `page` | Default `1`; must be positive |
| `pageSize` | Default `20`; allowed range `10`–`100` |

Sort expressions must be selected from a server-side whitelist. Search values are parameterized. Branch data is fetched without per-user N+1 queries. The default list includes Staff with at least one assignment in the caller's scope, including inactive accounts; removed assignments are not treated as active membership.

`data` items:

```json
{
  "userId": "uuid",
  "displayName": "Nguyen Van A",
  "email": "staff@example.com",
  "phoneNumber": "0901234567",
  "staffCode": "NV001",
  "status": "ACTIVE",
  "activationStatus": "ACTIVE",
  "branches": [
    {
      "branchId": "uuid",
      "branchName": "Binh Loi",
      "assignmentStatus": "ACTIVE",
      "staffCode": "NV001",
      "jobTitle": "Nhan vien quay",
      "employmentStartDate": "2026-11-21",
      "notes": "Ca sang"
    }
  ],
  "lastLoginAt": "2026-11-21T08:30:00Z",
  "createdAt": "2026-10-01T07:00:00Z"
}
```

Responses must not include password hashes, invitation/reset tokens, refresh tokens, session IDs, security stamps, or other authentication metadata.

## Create request and result

Request:

```json
{
  "displayName": "Nguyen Van A",
  "email": "staff@example.com",
  "phoneNumber": "0901234567",
  "staffCode": "NV001",
  "jobTitle": "Nhan vien quay",
  "branchIds": ["owned-branch-uuid"],
  "employmentStartDate": "2026-11-21",
  "notes": "Ca sang"
}
```

Rules:

- `displayName` is trimmed and required, 2–100 characters.
- `email` is validated and normalized; it is read-only after account creation.
- `phoneNumber` follows the application's supported phone format and uniqueness rules.
- `branchIds` is required, distinct, non-empty, and every branch must be owned by the caller.
- `staffCode` is optional, trimmed, and unique among active Staff assignments in each branch.
- `jobTitle` and `notes` are optional; `notes` is limited to 1,000 characters.
- `employmentStartDate` is optional and must be a valid date.
- The service assigns only `STAFF`; extra `role`, `roleIds`, or `permissions` values are rejected or ignored and never persisted.
- If email or phone is already registered, return a generic `STAFF_ALREADY_EXISTS` conflict regardless of the existing account's owner/scope. Do not attach the existing User or reveal another tenant's identity.

Success uses `201 Created`. The response `data` contains:

```json
{
  "userId": "staff-user-uuid",
  "invitationDeliveryStatus": "SENT",
  "warningCode": null
}
```

If delivery fails after the transaction commits, the API still reports the account creation outcome explicitly and sets `invitationDeliveryStatus` to `FAILED` with a safe `warningCode` of `INVITATION_SEND_FAILED`. The account remains inactive and is recoverable through `send-activation`. The response never contains the token.

## Update, assignment, and status requests

Update request:

```json
{
  "displayName": "Nguyen Van An",
  "phoneNumber": "0901234567",
  "staffCode": "NV002",
  "jobTitle": "Nhan vien van hanh",
  "branchIds": ["owned-branch-uuid"],
  "employmentStartDate": "2026-11-21",
  "notes": "Ca chieu"
}
```

Email, password, account status, role, permission, owner ID, audit fields, and authentication metadata are not update fields. `branchIds` replaces only this owner’s assignment set, must contain at least one valid owned branch, and is applied in the same transaction as the allowed profile/assignment fields.

Deactivate request:

```json
{
  "reason": "Nhan vien da nghi viec"
}
```

`reason` is optional, trimmed, and limited to 500 characters. Audit actor, target, branch scope, timestamp, and reason where supported. Do not accept client-supplied audit columns.

Activation request (anonymous):

```json
{
  "token": "one-time-token",
  "newPassword": "user-supplied-password"
}
```

The existing registration policy does not impose a strength requirement. Staff activation requires at least 12 characters, at most 72 UTF-8 bytes, and at least three categories among uppercase, lowercase, digits, and symbols. Do not return credentials after activation. The endpoint permits 10 requests per IP per minute per application instance; deployments must account for proxy configuration and distributed rate limiting.

### SMTP deployment configuration

Configure `StaffInvitations:ActivationUrl` as the HTTPS URL of a frontend page that reads the `token` value from the URL fragment and submits it to the activation API. That page does not yet exist. Configure `StaffInvitations:Smtp:Host`, `Port`, and `Sender`; `Username` and `Password` are optional but must be configured together. Use secret storage or environment variables for credentials. The SMTP relay must support STARTTLS. Missing or invalid configuration returns `STAFF_DELIVERY_NOT_CONFIGURED` before any account is created. The activation token is sent in the URL fragment, not in an HTTP query string.

## Transactions, history, and access revocation

- Create User, STAFF branch assignments, invitation metadata, and audit values in one database transaction.
- Validate every requested branch against the current owner before writing. Database unique constraints remain authoritative under concurrency.
- Send email only after commit. Delivery failure must be observable and retryable; it must not roll back a valid account.
- Update fields and assignment differences transactionally. Use stable row-lock order where locking is needed.
- Deactivation and refresh-token/session revocation commit together where supported.
- Soft-remove assignments and preserve Users and operational/audit references. Never delete bookings, POS, inventory, payment, invoice, or activity history.
- JWTs are stateless and can remain usable until their configured expiration. Staff-management and sensitive branch operations must recheck current account and active assignment state where the existing authorization architecture supports it. Refresh-token revocation alone does not immediately invalidate an already-issued access token.

## Errors and HTTP behavior

Errors use the existing `ApiErrorResponse` shape.

| Code | HTTP | Meaning |
|---|---:|---|
| `STAFF_NOT_FOUND` | 404 | No manageable Staff target in owner scope |
| `STAFF_NOT_MANAGEABLE` | 403 | Target is not Staff in the caller's scope or has a protected role |
| `STAFF_ALREADY_EXISTS` | 409 | Email or phone conflicts with an existing User; no identity details disclosed |
| `STAFF_ALREADY_ACTIVE` | 409 | Account is already active |
| `STAFF_ALREADY_INACTIVE` | 409 | Account is already inactive |
| `STAFF_HAS_PROTECTED_ROLE` | 403 | Target holds a protected role |
| `STAFF_BRANCH_REQUIRED` | 400 | No branch assignment was supplied or the final assignment would be removed |
| `STAFF_BRANCH_ACCESS_DENIED` | 403 | A requested branch is not owned/authorized by the caller |
| `STAFF_ASSIGNMENT_NOT_FOUND` | 404 | Assignment does not exist in owner scope |
| `STAFF_CODE_ALREADY_USED` | 409 | Staff code conflicts in an assigned branch |
| `LAST_ACTIVE_BRANCH_REMOVAL_NOT_ALLOWED` | 409 | Operation would leave Staff with no active assignment |
| `INVITATION_EXPIRED` | 400 | Invitation is expired, revoked, consumed, or invalid |
| `INVITATION_SEND_FAILED` | 201 / 200 | Delivery failed after creation or resend; returned as `warningCode` |
| `STAFF_DELIVERY_NOT_CONFIGURED` | 503 | SMTP or activation URL configuration is missing or invalid |
| `STAFF_INVITATION_INVALID` | 400 | Invitation is invalid, expired, revoked, or consumed |
| `STAFF_PASSWORD_INVALID` | 400 | Activation password does not meet the stated policy |
| `PASSWORD_RESET_SEND_FAILED` | 503 | Password-reset delivery failed; account state remains unchanged |
| `400` validation error | 400 | Invalid request fields, pagination, or sort |
| authentication failure | 401 | Caller is unauthenticated |
| permission/scope failure | 403 | Caller lacks permission or owner scope |

Use a generic activation response for invalid, expired, revoked, and consumed tokens. Do not reveal whether an unrelated email or account exists.

## Schema work anticipated for Phase 3

Reuse `auth.branch_user_roles` and add only missing assignment data:

- `staff_code`, `job_title`, `employment_start_date`, `notes`
- `removed_at`, `removed_by` to distinguish soft removal and preserve auditability
- a partial unique index for active, non-removed `(branch_id, staff_code)` when a code is present

Add a minimal invitation table under `auth` for purpose (`STAFF_ACTIVATION` or `STAFF_PASSWORD_RESET`), token hash, expiry, consumed/revoked timestamps, creator/audit data, delivery state, and safe delivery error code. Enforce one outstanding token per user/purpose and index lookup/expiry paths. Do not store raw tokens.

The Phase 5 migration adds case-insensitive email uniqueness for non-deleted accounts and fails if duplicates already exist. Verify and resolve duplicates before applying it. The existing active-phone index remains in use. No new User, Role, Permission, UserRole, or Branch-assignment table is planned.

## Implementation prerequisites and contract synchronization

- Configure and validate the existing SMTP relay and deploy the frontend activation page before using account creation.
- Resolve the `BRANCH_ADMIN` / `BRANCH_MANAGER` role-code discrepancy against the actual deployed role rows before Phase 3 permission seeding.
- Keep Swagger DTOs, frontend TypeScript contracts, this document, and validation rules synchronized.
- The frontend Staff card/menu is visible to Branch Owners or users with the relevant Staff-view capability, but frontend visibility is not authorization. Backend owner and branch checks are mandatory on every route.
