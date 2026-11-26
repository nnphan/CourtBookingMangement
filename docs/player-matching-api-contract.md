# Player Matching API Contract

## Status

**Phase 2 implementation is in progress.** Authenticated profile and preference
read/update, join-request creation with an optional message, applicant history,
host rejection reasons, request cancellation, and pending-request expiry when a
room fills are implemented in the backend. Approval now revalidates the
confirmed host booking, applicant profile/rating/gender, branch-local start time,
and active-member count while locking the room and request. Draft creation and
editing, owner-only draft visibility, confirmed-booking publication, and host
cancellation with pending-request expiry are also implemented. Personalized
search and scoring are implemented. Join requests and approvals now revalidate
bookings under the room lock; an invalid booking cancels the room and expires
its pending requests. Search excludes rooms with invalid bookings. Scheduled reconciliation now cancels rooms with invalid linked bookings and expires open or full rooms at their branch-local start. Host start, completion and member leave remain pending. Backend DTOs,
Swagger, this document, and frontend types must be synchronized as each slice
is completed.

## Decisions and existing model

- Evolve `matching.player_matches`, `matching.match_participants`, and
  `matching.match_join_requests`; do not create parallel room, member, or
  join-request tables.
- Matching identities use authenticated user IDs, consistent with the existing
  matching tables. A booking-linked host must also have the customer relationship
  required by the existing booking ownership query. Identity and review author
  are taken from the authenticated user, never from client-supplied IDs.
- New profile, rating-history, preferences, and preferred-branch records are
  needed if a live-schema audit confirms they do not already exist. Preserve
  legacy records and clients; do not silently convert textual skill bands into
  numerical ranges.
- Each room has a unique server-generated `roomCode`; migration must assign
  stable unique codes to existing rooms without changing their IDs. The
  migration derives the code from the UUID as `MR` followed by 32 hexadecimal
  characters.
- Existing text-only rooms remain in legacy discovery, but are excluded from
  personalized search until the host supplies a numerical rating range and game
  type. The host can set these once while the room is `OPEN` or `FULL` and has
  not started. Existing members are not removed; pending applicants are
  revalidated at approval. Other fields remain unchanged.
- Publication requires an active, non-deleted, **confirmed** booking owned by
  the host, with a matching branch and active court/booking-detail schedule.
  The booking detail must cover the whole match interval. Drafts are not
  searchable. Planning rooms without a booking are deferred.
- If a linked booking becomes invalid, a published, uncompleted room must
  become unavailable and be cancelled, and its pending requests expire.
  Search, join, and approval must recheck booking validity, including during
  any delay before cancellation is reflected in room state. Notifications are
  best effort after commit and cannot undo a successful transaction.
- Manual approval only. Automatic approval and moderation permissions are
  deferred. Existing customer permissions and branch authorization remain in
  effect.

## Profile and privacy

The player's self-assessed integer `currentRating` is between 0 and 1000.
Initial self-assessment uses `ratingSource: SELF_ASSESSMENT` and
`ratingConfidence: 0.40`. The initial rating and each subsequent change are
audited. A player cannot set their own confidence, assessment source, matches
played, or assessment evidence. Administrative or coach assessment workflows
are deferred.

The profile rating remains the input for minimum and maximum room
eligibility. Scoring uses categorical level distance. Rating confidence is
retained as profile metadata.

| Rating | Band |
|---|---|
| 0-199 | BEGINNER |
| 200-399 | BASIC |
| 400-599 | INTERMEDIATE |
| 600-749 | GOOD |
| 750-899 | ADVANCED |
| 900-1000 | COMPETITIVE |

MVP gender policies are `ANY` and `SPECIFIC`; `SAME_AS_HOST` and
`MIXED_REQUIRED` are deferred. A player may explicitly provide a private gender
value `MALE`, `FEMALE`, or `OTHER`, or leave it undisclosed with `null`.
Gender is never inferred. The profile form states that an optionally supplied
value is used for room eligibility; saving it constitutes the player's explicit
choice, and the player can clear it. A `SPECIFIC` room selects one of the same
three values. An undisclosed or nonmatching value fails eligibility; no exclusion
reason is exposed in public search. The room's own requirement is visible to
prospective applicants, but members' private values are never displayed or
logged.

Discovery and public room detail must not return email, phone, payment data,
private gender values, rating evidence, or block relationships. Hosts may view
only the public applicant summary and permitted rating/confidence information
for their own rooms. A profile marked private must not expose its private fields.

## Lifecycle and capacity

Room statuses are `DRAFT`, `OPEN`, `FULL`, `STARTED`, `COMPLETED`, `CANCELLED`,
and `EXPIRED`. New rooms begin as `DRAFT`; a host publishes a valid draft to
`OPEN`. A draft may be edited by its host. Creation and publication are distinct
operations; the legacy creation endpoint remains compatible during migration.
Draft creation/editing, publication, and host cancellation are implemented;
host start, completion and member leave remain pending.

Intended transitions:

| From | To | Condition |
|---|---|---|
| DRAFT | OPEN | Host publishes; booking and required fields are valid |
| DRAFT | CANCELLED | Host cancels |
| OPEN | FULL | Last available slot approved |
| OPEN | CANCELLED | Host cancels, or linked booking becomes invalid |
| FULL | CANCELLED | Host cancels, or linked booking becomes invalid |
| OPEN, FULL | EXPIRED | Match start passes without starting |
| FULL | OPEN | An active non-host member leaves and the room is still eligible |
| FULL, OPEN | STARTED | Host starts during the match interval with at least two active members |
| STARTED | CANCELLED | Linked booking becomes invalid before completion |
| STARTED | COMPLETED | Host completes the room |

The branch timezone determines the actual start and end instants. Search,
new requests, and approvals stop at start, even if a worker has not changed the
status. An unstarted open or full room becomes `EXPIRED` at or after start; pending requests
expire when the room starts, expires, or is cancelled. Only the host can start
or complete. The host may complete a started room before or after its end.
A non-host active member may leave an `OPEN` or `FULL` room before start;
a full room reopens only if its booking remains valid and it has capacity.
Invalid transitions return a conflict and do not mutate state.

The host is an active participant. Active membership is authoritative for
capacity. If `current_players` remains persisted for compatibility, it must be
reconciled and updated transactionally with membership. The schema audit must
check legacy counts before imposing new constraints. Preserve mismatched legacy
counts and members; personalized discovery and new join/approval workflows
must exclude any room whose stored count differs from its active-member count
until an operator reconciles it. Do not silently change a room's capacity.

Join requests have `PENDING`, `APPROVED`, `REJECTED`, `CANCELLED`, or `EXPIRED`
status. Only one `PENDING` request per room/user is permitted. A request may be
cancelled only by its owner while pending; only the host may approve or reject
it. Approved requests create an active member atomically. When an approval fills
the room, all other `PENDING` requests become `EXPIRED` in the same transaction.
If a slot later reopens, an eligible player may submit a new request.
Reapplication after a terminal request requires a migration from the current
whole-pair uniqueness constraint to a suitable partial unique constraint after
auditing live data.

Approval uses one PostgreSQL transaction: lock room, lock request, validate
host, booking, room state, time, eligibility, membership, and active capacity;
insert the participant; update the request and count; set `FULL` if needed;
then commit. Concurrent approvals for the final slot must not overfill the room.

## Search and scoring

Personalized search requires authentication. A customer without a matching
profile can search with request weights or system defaults. Saving preferences
requires a profile. An existing profile that has opted out cannot search.

SQL first applies participation rules such as room state, slots, future play
time, owned booking validity, date, selected branches, rating eligibility,
mandatory gender, and existing membership or pending requests. Search skill
and the time window affect ranking. A low component score does not invalidate
an eligible result.

- Skill uses the requested level, or the profile rating mapped to the same
  four levels used by room creation: below 400 beginner, below 600 intermediate,
  below 900 advanced, otherwise professional. The room uses its recorded level.
  Distances 0, 1, 2, and 3 or more score 100, 70, 40, and 10. Missing levels score 0.
- Time uses branch-local time-of-day intervals expressed as `HH:mm:ss`. The
  request supplies both `startTime` and `endTime`, or omits both. The legacy
  `preferredStartTime` and `preferredEndTime` pair is also accepted. The time
  score is intersection duration divided by union duration, multiplied by 100.
  Missing, disjoint, or touching intervals score 0. Exact intervals score 100.
- Branch scores 100 when the room ID matches the first selected branch ID,
  otherwise 0. Additional IDs still qualify for the branch filter. An empty
  list applies no branch filter and gives 0 branch points.
- Gender scores 100 for rooms open to everyone or a matching room requirement,
  otherwise 0. The profile gender takes priority over the request selection.
  A request selection does not override mandatory eligibility checks.

Each weight is an exact decimal percentage in `0..100`; totals other than 100
are rejected. The final score is the sum of each component multiplied by its
weight divided by 100. Results sort on the unrounded score and display one
decimal. A displayed time score below a perfect match is capped at 99.9.
Each result returns the applied `weights`, the individual component scores,
and their weighted contributions. The search envelope also retains
`appliedWeights` for existing clients. Personalized scores are not persisted.

### Candidate selection and pagination

Apply SQL hard filters and batch-load all inputs without N+1 queries. Score
before pagination; order by score descending, confirmed court first, available
slots ascending, start time ascending, creation time descending, then ID.
Limit page size to `1..50`. SQL selects at most 301 eligible candidates ordered
by start time ascending then room ID ascending. Score only the first 300;
`candidateLimitReached` is true if the 301st exists. In that case scores,
ranking and `metadata.totalCount` cover only the bounded candidate set, not
all matching rooms; the UI must disclose that results are incomplete. Without
truncation, the total is the count of eligible scored candidates. Results
are live rather than a snapshot: pagination may change as rooms or profiles
change.

## Proposed API surface

These are planned endpoints, not a claim that they already exist. Retain the
existing `/api/player-matches` routes and response envelope for legacy clients.
All new or changed personalized/mutating operations require authentication.
Use existing `ApiResponse` / `ApiErrorResponse`, `traceId`, `timestamp`, and
paging metadata conventions. Backend DTOs and Swagger must be finalized before
frontend types.

| Method | Route | Access and intent |
|---|---|---|
| GET, PUT | `/api/me/player-matching-profile` | Read/update own profile |
| GET, PUT | `/api/me/match-preferences` | Read/update own default weights and branches |
| POST | `/api/player-matches/drafts` | Create own draft |
| GET, PUT | `/api/player-matches/{id}` | View permitted detail; edit own draft |
| POST | `/api/player-matches/{id}/publish` | Publish own valid draft |
| POST | `/api/player-matches/{id}/cancel` | Cancel own eligible room |
| PUT | `/api/player-matches/{id}/rating-range` | Host supplies range and game type for a legacy room |
| POST | `/api/player-matches/search` | Personalized hard-filtered and scored search |
| POST | `/api/player-matches/{id}/score` | Score one eligible room using the search criteria |
| POST | `/api/player-matches/{id}/join-requests` | Submit own request with optional message |
| DELETE | `/api/player-matches/{id}/join-requests/me` | Cancel own pending request |
| GET | `/api/player-matches/{id}/join-requests` | Host lists applicants for own room |
| POST | `/api/player-matches/{id}/join-requests/{requestId}/approve` | Host approves |
| POST | `/api/player-matches/{id}/join-requests/{requestId}/reject` | Host rejects |
| GET | `/api/player-matches/hosted` | Existing host activity route |
| GET | `/api/player-matches/participating` | Existing participant activity route |
| GET | `/api/me/match-join-requests` | Own request history |
| POST | `/api/player-matches/{id}/leave` | Active non-host member leaves before start |
| POST | `/api/player-matches/{id}/start` | Host starts an eligible room |
| POST | `/api/player-matches/{id}/complete` | Host completes a started room |

Room IDs, request IDs, branch IDs, and user IDs are UUIDs. Money is a
non-negative VND decimal consistent with existing booking conventions; date
is `YYYY-MM-DD`. The legacy `GET /api/player-matches` and its existing
creation/join routes remain available; the new search and score operations
never rely on legacy public results for eligibility.

### Write contracts

- `PUT /api/me/player-matching-profile` accepts `currentRating` (integer),
  `gender` (`MALE`, `FEMALE`, `OTHER`, or `null`), `isProfilePublic`, and
  `isMatchingEnabled`. Its response also includes `ratingBand`,
  `ratingConfidence`, `ratingSource`, and `matchesPlayed`. Users cannot submit
  those server-managed fields. Returning a private profile is limited to its owner.
  The profile and preference `GET` routes return `404` until the player creates
  a profile. If the profile exists but preferences have not been saved, the
  preferences `GET` returns the default `40/30/20/10` weights and an empty
  `branchIds` list.
- `PUT /api/me/match-preferences` accepts `preferences` with the four weights,
  ordered `branchIds`, optional `preferredStartTime` and `preferredEndTime`,
  `minimumTimeOverlapMinutes`, and optional `maximumParticipationFee`. When
  either preferred time is set, both are required and start must precede end.
  The request shape is:

  ```json
  {
    "preferences": {
      "skillWeight": 40,
      "timeWeight": 30,
      "branchWeight": 20,
      "genderWeight": 10
    },
    "branchIds": ["00000000-0000-0000-0000-000000000001"],
    "preferredStartTime": "18:00:00",
    "preferredEndTime": "21:00:00",
    "minimumTimeOverlapMinutes": 60,
    "maximumParticipationFee": 100000
  }
  ```
- A draft write accepts `bookingId`, `branchId`, `courtId`, `matchDate`,
  `startTime`, `endTime`, `title`, optional `description`, `gameType`,
  `minimumRating`, `maximumRating`, `requiredPlayerCount`, `genderPolicy`,
  optional `requiredGender`, and `participationFee`. Publication requires all
  fields except description and `requiredGender` for `ANY`; partial drafts may
  omit booking and schedule. An `ANY` policy requires `requiredGender: null`.
  Rating limits are inclusive `0..1000`, minimum cannot exceed maximum,
  capacity is `2..20`, and fee is non-negative. Server validates booking,
  branch, court, schedule and ownership rather than trusting supplied IDs.
  Drafts require branch, title, game type, rating range, player count, gender
  policy, and participation fee; booking, court, and the complete schedule may
  be omitted until publication. If any schedule value is supplied, all three
  date/time values must be supplied.
- `PUT /api/player-matches/{id}/rating-range` accepts `minimumRating`,
  `maximumRating`, and `gameType`. It is limited to the host's legacy room as
  described above.
- Join-request creation accepts optional `message` up to 500 characters.
  Rejection accepts an optional `reason` up to 500 characters, visible only
  to the host and applicant. Approval does not accept a player identity. The
  authenticated applicant can cancel only their own pending request and list
  their paged request history; the host's applicant list remains host-only.

### Search example

```json
{
  "branchIds": [
    "00000000-0000-0000-0000-000000000001",
    "00000000-0000-0000-0000-000000000002"
  ],
  "matchDate": "2026-11-21",
  "preferredStartTime": "18:00:00",
  "preferredEndTime": "21:00:00",
  "minimumTimeOverlapMinutes": 60,
  "gameTypes": ["DOUBLES"],
  "maximumParticipationFee": 100000,
  "preferences": {
    "skillWeight": 40,
    "timeWeight": 30,
    "branchWeight": 20,
    "genderWeight": 10
  },
  "saveAsDefault": false,
  "page": 1,
  "pageSize": 20
}
```

Validate start before end, positive minimum overlap not exceeding the preferred
interval, non-negative fee, valid distinct branches and game types, pagination,
and weights independently on the server. Search saves the submitted weights,
ordered branches, preferred interval, overlap, and maximum fee only when
`saveAsDefault` is true and the search request succeeds. A normal search never
changes saved preferences.

The success response uses the existing envelope; `data.items` is the current
page and `metadata.totalCount` follows the bounded semantics above:

```json
{
  "success": true,
  "message": "Matching rooms retrieved successfully.",
  "data": {
    "items": [{
      "roomId": "00000000-0000-0000-0000-000000000003",
      "roomCode": "MR261121001",
      "title": "Doubles practice",
      "branchId": "00000000-0000-0000-0000-000000000002",
      "branchName": "Example branch",
      "courtName": "Court 1",
      "courtReservationStatus": "COURT_RESERVED",
      "matchDate": "2026-11-21",
      "startTime": "19:00:00",
      "endTime": "21:00:00",
      "gameType": "DOUBLES",
      "minimumRating": 400,
      "maximumRating": 700,
      "currentPlayerCount": 2,
      "requiredPlayerCount": 4,
      "availableSlots": 2,
      "participationFee": 70000,
      "matchScore": 70.0,
      "matchLevel": "GOOD",
      "weights": { "skillWeight": 40, "timeWeight": 30, "branchWeight": 20, "genderWeight": 10 },
      "scoreBreakdown": {
        "skillScore": 100.0,
        "timeScore": 66.7,
        "branchScore": 0.0,
        "genderScore": 100.0,
        "skillContribution": 40.0,
        "timeContribution": 20.0,
        "branchContribution": 0.0,
        "genderContribution": 10.0
      },
      "reasons": ["SKILL_MATCH"],
      "warnings": ["PARTIAL_TIME_OVERLAP"],
      "joinRequestStatus": null
    }],
    "appliedWeights": {
      "skillWeight": 40,
      "timeWeight": 30,
      "branchWeight": 20,
      "genderWeight": 10
    },
    "candidateLimitReached": false
  },
  "metadata": {
    "pageNumber": 1,
    "pageSize": 20,
    "totalCount": 1,
    "totalPages": 1,
    "hasPreviousPage": false,
    "hasNextPage": false
  },
  "traceId": "example-trace",
  "timestamp": "2026-11-18T00:00:00Z"
}
```

The score endpoint accepts the same criteria as search except `page`,
`pageSize`, and `saveAsDefault`; it returns the same score/breakdown fields for
the requested room. It applies the same participation rules and scoring formula; if the
room is not eligible, it returns a generic `PLAYER_NOT_ELIGIBLE` without a
sensitive reason. It does not save preferences.

For example, `21:00?22:00` compared with `21:00?23:00` gets a time score
of 50. At a time weight of 30%, that criterion adds 15 points.

## Errors and authorization

Use the existing error envelope and explicit HTTP mappings: invalid input
`400`, unauthenticated `401`, denied ownership/permission `403`, unavailable
resource `404`, and state/capacity conflicts `409`. Planned domain codes
include `PLAYER_MATCHING_PROFILE_REQUIRED`, `PLAYER_MATCHING_DISABLED`,
`PLAYER_RATING_OUT_OF_RANGE`, `PREFERENCE_WEIGHT_TOTAL_INVALID`,
`MATCH_ROOM_NOT_FOUND`, `MATCH_ROOM_NOT_OWNED`,
`MATCH_ROOM_INVALID_STATUS_TRANSITION`, `MATCH_ROOM_FULL`,
`MATCH_ROOM_BOOKING_INVALID`, `MATCH_ROOM_ALREADY_MEMBER`,
`JOIN_REQUEST_NOT_FOUND`, `JOIN_REQUEST_ALREADY_EXISTS`,
`JOIN_REQUEST_INVALID_STATUS`, `PLAYER_NOT_ELIGIBLE`,
`MATCH_ROOM_DRAFT_INCOMPLETE`, `MATCH_ROOM_SCHEDULE_CONFLICT`,
`SEARCH_CRITERIA_INVALID`. Do not expose
blocking or sensitive-policy details.

## Frontend integration

Reuse the existing player-matching pages, dialogs, translations, shared
components, and TanStack Query. Search weights have a live total and cannot
submit unless the total is exactly 100. Show score, label, breakdown, reasons,
warnings, capacity, and confirmed-court status without relying on color alone.

Invalidate relevant search, room detail, hosted, participating, applicant, and
profile/preference query keys after mutations; never clear the entire query
cache or reload the page to force consistency. No server-authoritative room
list or capacity is stored in Zustand. Include loading, empty, retry, keyboard,
mobile, and accessible validation states.

## Rollout and validation

Before migration, audit live schema, legacy room statuses, booking links,
membership/count consistency, and existing uniqueness constraints. Preserve
legacy clients and data; deploy additive schema and backend DTOs before
frontend adoption. Verify the one-slot concurrent approval race, booking
invalidation, late requests, privacy, score examples, and candidate truncation
without using real player data. Schema and backend API work has begun; frontend
adoption and end-to-end rollout validation remain outstanding.

## Deferred

Distance, fee, game type, play style, reliability, host reputation, and court
trust **scoring**; auto approval; planning rooms; inferred gender; external
messaging; complex rating automation; and new Redis search caching are outside
the MVP. Game type and maximum fee remain hard filters.


### Weight-default migration

Apply `20261126_UpdateMatchingWeightDefaults.sql` after the November 18 matching migration. It changes the column defaults to skill 40 and time 30. Stored user preferences retain their saved values. To revert the change, restore column defaults 50 and 20 in a forward SQL patch and deploy the corresponding previous application defaults. No preference-row rewrite is required.
