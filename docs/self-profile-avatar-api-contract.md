# Self-Service Profile and Avatar API Contract

**Status:** Implemented through Phase 7; profile UI and runtime integration remain  
**Scope:** Authenticated users editing their own display name and avatar  
**Storage provider:** Existing Cloudinary integration

The profile endpoints, database/upload foundations, frontend API contracts,
and query synchronization are implemented. The profile editing UI and runtime
integration remain in later phases.

## 1. Profile ownership and editable fields

`auth.users` is the current-user API's source for identity, display name, email,
and phone. Registration also copies name, email, and phone to the linked
`customer.customers` row. Profile writes must update `auth.users.full_name` and,
when a non-deleted linked customer exists, `customer.customers.full_name` in the
same database transaction. Do not create a customer record as a side effect of
profile access or editing.

The profile avatar belongs to the authenticated user and is stored as a file
reference on `auth.users`. Customer records do not get a duplicate avatar field.

| Field | Self-service behavior | Reason |
| --- | --- | --- |
| Display name | Editable; trimmed; 2-150 characters | Exists in both current records; 150 is the narrower database limit |
| Email | Read-only | No verified-email-change flow exists |
| Phone number | Read-only | It is a login identity; no phone verification/change flow exists |
| Avatar | Upload, replace, or remove | Uses the existing avatar media category |
| Date of birth, address, preferred language, gender | Not exposed | Not present on the current customer/user models |
| Roles, permissions, status, branch assignment, IDs, and audit fields | Never accepted from the client | Security- and administration-owned data |

No email or phone change flow is introduced by this feature.

## 2. Profile endpoints

All profile endpoints require an authenticated bearer token. The user ID is
resolved only from `ICurrentUserService`; request-supplied user or customer IDs
are not authoritative.

### Get own profile

```http
GET /api/me/profile
Authorization: Bearer <access-token>
```

Success uses the existing API envelope (`success`, `message`, `data`,
`metadata`, `traceId`, `timestamp`):

```json
{
  "success": true,
  "message": "Profile retrieved successfully.",
  "data": {
    "userId": "00000000-0000-0000-0000-000000000001",
    "customerId": "00000000-0000-0000-0000-000000000002",
    "displayName": "Nguyen Van A",
    "email": "user@example.com",
    "phoneNumber": "0901234567",
    "avatar": {
      "fileId": "00000000-0000-0000-0000-000000000003",
      "url": "https://res.cloudinary.com/example/image/upload/...",
      "fileName": "generated-name.webp"
    },
    "updatedAt": "2026-11-20T14:00:00Z"
  },
  "metadata": null,
  "traceId": "trace-id",
  "timestamp": "2026-11-20T14:00:00Z"
}
```

`customerId`, `displayName`, and `avatar` are nullable to preserve existing
records without a name or avatar. The response exposes no Cloudinary
credentials, physical paths, or mutable security fields. The avatar URL is
returned from authoritative file metadata, not trusted client input.

### Update own profile

```http
PUT /api/me/profile
Authorization: Bearer <access-token>
Content-Type: application/json
```

Request:

```json
{
  "displayName": "Nguyen Van An",
  "avatarFileId": "00000000-0000-0000-0000-000000000003",
  "removeAvatar": false
}
```

`avatarFileId` may be omitted to leave the existing avatar unchanged. To
replace it, supply the ID returned by the upload endpoint. To remove it, send
`removeAvatar: true` and omit `avatarFileId`. Setting both to a value and
`removeAvatar: true` is invalid. The database update must reload and return the
persisted profile; it must not echo the request as the result.

Successful updates use the existing response envelope. Backend audit values are
server-generated: `updated_at` and `updated_by = current user ID`.

### Failure behavior

- Validation errors return HTTP 400 in the existing `ApiErrorResponse` shape,
  including field validation errors.
- Unauthenticated requests return 401.
- A file that does not exist returns 404.
- A file owned by another user, or one not eligible to attach, returns 403 or
  the stable domain-equivalent error.
- Unique/concurrency conflicts return 409 where applicable.
- A rejected update leaves both the previous profile and previous avatar
  reference unchanged.

Stable profile/file error codes:

| Code | HTTP |
| --- | ---: |
| `PROFILE_NOT_FOUND` | 404 |
| `AVATAR_FILE_NOT_FOUND` | 404 |
| `AVATAR_FILE_NOT_OWNED` | 403 |
| `AVATAR_FILE_INVALID_TYPE` | 400 |
| `AVATAR_FILE_TOO_LARGE` | 400 or 413 at the HTTP boundary |
| `AVATAR_FILE_NOT_ATTACHABLE` | 409 |

Profile request validation uses the shared `COMMON_001` error code and includes
field-level validation errors.

The implementation must map these codes through the existing exception and
status-mapping conventions. Error messages must not disclose storage details.

## 3. Existing upload endpoint

### Contract before Phase 4

```http
POST /api/files/upload
Content-Type: multipart/form-data
```

Multipart fields are exactly:

| Field | Type | Current value |
| --- | --- | --- |
| `file` | Binary image | Required |
| `category` | Text | `avatar` for profile images |

Before Phase 4, the success envelope's `data` contained `fileName`, `url`,
`size`, `contentType`, and `category`, with no file ID or persisted uploader
metadata.

### Implemented compatible extension

The route, field names, and existing response fields are retained. The API now
adds an opaque `fileId` and persists uploader/file metadata:

```json
{
  "success": true,
  "message": "File uploaded successfully",
  "data": {
    "fileId": "00000000-0000-0000-0000-000000000003",
    "fileName": "generated-name.webp",
    "url": "https://res.cloudinary.com/example/image/upload/...",
    "size": 102400,
    "contentType": "image/webp",
    "category": "avatar"
  }
}
```

The extra response field is additive for existing clients. Uploads require an
authenticated user; the uploader ID is obtained from claims and is never
accepted as a multipart field. Existing categories remain supported. The
`avatar` category is stored in a user-scoped Cloudinary folder, and metadata
records are the authoritative owner/type/attachability check. Avatar uploads
start in `TEMPORARY`; legacy non-avatar categories are recorded as
`ATTACHED` so future temporary-avatar cleanup cannot delete existing images.

No new avatar-upload route is introduced. Do not accept arbitrary avatar URLs
in profile updates.

Uploads require the file extension to match the declared image MIME type and
the image's magic bytes to match JPEG, PNG, or WebP. The server generates the
storage filename. SVG is rejected. The maximum file size remains 5 MiB.

The reusable `IAvatarFilePolicy` checks metadata existence, uploader ownership,
soft-delete state, category, content type, size, and attachable state. The
database composite foreign key independently prevents attaching another user's
file. If Cloudinary succeeds but metadata persistence fails, the request fails
and logs the Cloudinary public ID for safe orphan investigation; automatic
provider cleanup for this cross-system failure is not implemented yet.

## 4. File metadata and avatar policy

The current repository has Cloudinary storage but no file metadata table or
file entity. The migration adds `media.files` as metadata for the existing
Cloudinary provider, not as a second storage service. It records:

- `id` (opaque UUID), `uploader_user_id`, and `category`
- `cloudinary_public_id` and durable `secure_url`
- Generated `file_name`, `content_type`, and `size_bytes`
- `status` (`TEMPORARY`, `ATTACHED`, or `REPLACED`)
- `created_at`, `updated_at`, `updated_by`, `deleted_at`, and `deleted_by`

`auth.users.avatar_file_id` references `media.files`. The composite foreign key
from `(avatar_file_id, auth.users.id)` to `(media.files.id,
media.files.uploader_user_id)` enforces uploader ownership at the database
boundary as well as preventing deletion of a referenced metadata record. A
file can be attached only when it exists, is not deleted, is an allowed image,
has category `avatar`, was uploaded by the current user, and is `TEMPORARY` or
already the current user's avatar. A file attached to another user's profile
is never attachable.

Allowed formats: JPEG, PNG, and WebP. Reject SVG. The effective original file
limit is 5 MiB, matching the existing service limit; retain the controller's
request-size guard with enough multipart overhead and do not increase the
service limit. Backend validation must verify signatures/magic bytes as well
as content type and allowed extension. Filenames and Cloudinary public IDs are
server-generated.

## 5. Replace, remove, and orphan policy

Upload and profile update remain separate HTTP operations:

1. Upload creates a `TEMPORARY` metadata record owned by the authenticated
   user.
2. Profile update validates ownership and eligibility, then changes the
   profile reference and file states in one PostgreSQL transaction.
3. Only after the new reference commits may the prior avatar become
   `REPLACED`.
4. Removal clears the reference transactionally; it never deletes a shared or
   default image.

If profile update fails after upload, the previous avatar remains active and
the uploaded file stays `TEMPORARY`. Expired temporary files are eligible for
cleanup after 24 hours. Replaced files are retained for 30 days before
best-effort Cloudinary cleanup. Cleanup failures are logged with safe
identifiers and do not roll back a committed profile change. These cleanup
intervals are the implementation baseline; cleanup must be implemented only
after a metadata-backed delete operation is available.

## 6. Database and compatibility

The migration adds nullable `auth.users.avatar_file_id` and `media.files`. It
does not add a duplicate avatar column to customers. The current schema has no
persisted avatar field or file table, so no historical database backfill is
required. The frontend's optional `avatarUrl` type/mocks do not represent
persisted production data.

`GET /api/auth/me`, login, registration, and refresh responses expose the
authoritative `avatarUrl` from the current profile repository. The frontend
models this field and its current-user hook synchronizes the `/auth/me` query
result into the auth store. Role, permission, and status values remain
unchanged. JWT claims remain immutable and are not modified client-side.

Existing management endpoints and their authorization behavior are out of
scope. The profile API must not call or weaken the administrative user-update
endpoint.

## 7. Audit and logging

Set `updated_at` and `updated_by` on profile records using server-side values.
Persist uploader ownership and file state changes in metadata. Logs may include
trace ID, current user ID, file ID, operation, and result. Do not log image
bytes, authorization headers, physical paths, credentials, or unnecessary
personal field values.

## 8. Frontend cache and avatar URL behavior

Use the existing Axios client and API response envelope. Multipart uploads use
the fields `file` and `category`; the category value is `avatar`. Keep the
existing TanStack Query client and shared `queryKeys.profile.me` query key.

After a successful update, cache the authoritative profile response, update
the matching auth user/store fields, and invalidate `queryKeys.auth.me` for
server reconciliation. The upload mutation reports transfer progress but does
not update the profile or global avatar until the profile update succeeds. Do
not clear the query cache or reload the page.

Cloudinary upload names are unique and immutable; a replacement therefore has
a new URL and naturally avoids stale-avatar caching. The existing global
header and account dropdown use the shared `CurrentUserAvatar` component and
initials fallback. No other persisted avatar consumers were found during
discovery.

## 9. Contract alignment checklist

- C# upload and profile DTOs expose exactly the fields documented here.
- Swagger describes the existing multipart field names and additive `fileId`.
- TypeScript upload/profile types match the serialized API response.
- Profile UI exposes only display name and avatar as editable fields.
- Error codes/status mappings match the backend `ApiErrorResponse` behavior.
- Profile and current-user query data stay synchronized after successful save.
