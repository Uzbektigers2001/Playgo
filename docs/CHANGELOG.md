# Changelog

All notable changes to the Playgo backend will be documented in this file.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Phase 2: Review Votes & Moderation (Prompt 6)
- New `ReviewVote` entity with `Like` / `Dislike` toggle semantics — same-type vote cancels, opposite-type swaps and adjusts both counters in a single `SaveChangesAsync`. Soft-deleted votes are revived on re-vote so the partial unique index `(ReviewId, UserId) WHERE "IsDeleted" = false` keeps history clean.
- `Review.DislikesCount` and `Review.RejectionReason` fields added; existing `LikesCount` stays. Counters are mutated alongside the votes navigation, never via separate write paths.
- `ReviewDto` exposes `dislikesCount` and `myVote` (`"like"`/`"dislike"`/`null`). `myVote` is resolved per-request from `ICurrentUserService`, batched across the page of reviews.
- New `POST /api/reviews/{id}/vote` endpoint (authenticated) — body `{ "voteType": 1|2 }`, returns `{ likesCount, dislikesCount, myVote }`.
- New admin moderation pipeline:
  - `IAdminReviewService` + `AdminReviewService`.
  - `GET /api/admin/reviews?page=&pageSize=&contentId=&isApproved=&userId=&search=`.
  - `PUT /api/admin/reviews/{id}/approve` — folds the rating back into the content average if it was pending.
  - `PUT /api/admin/reviews/{id}/reject` — stores `RejectionReason`, pulls a previously-approved rating out of the average.
  - `DELETE /api/admin/reviews/{id}` — soft-delete with average rollback.
- Configurable `Reviews:RequireModeration` flag in `appsettings.json` (default `false`). When `true`, `POST /api/reviews` creates reviews with `IsApproved=false` and skips the average aggregation until an admin approves.
- `ICurrentUserService` and `IConfiguration` injected into `ReviewService`. `Microsoft.Extensions.Configuration.Abstractions` added to the Application project.
- 9 unit tests (`ReviewVoteTests` x6: like, toggle off, switch, not-found, two-user consistency, `myVote` per-caller; `AdminReviewTests` x3: approve, reject with reason, soft-delete).
- Migrations:
  - `AddReviewVotes` — creates `review_votes` with cascade FK to `reviews` and partial unique index on `(ReviewId, UserId)`.
  - `AddReviewModerationFields` — adds `DislikesCount` (NOT NULL DEFAULT 0) and `RejectionReason` (nullable text) columns to `reviews`.
