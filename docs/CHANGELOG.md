# Changelog

All notable changes to the Playgo backend will be documented in this file.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Phase 2: Watchlist & Playlists (Prompt 5)
- New `Watchlist` entity (separate from `Favorites`) for save-for-later — carries optional `priority` and `note`. Soft-deleted entries are revived on re-add so duplicates can't accumulate.
- New `Playlist` and `PlaylistItem` entities for user-created collections, with ordered items and an `isPublic` flag.
- Public/private playlist visibility: public list endpoint, private playlists rejected for non-owners on detail.
- Item reordering via `PUT /api/playlists/{id}/reorder` — requires every existing item id exactly once; `OrderIndex` is reassigned by position.
- Partial unique indexes (`WHERE "IsDeleted" = false`) on `watchlists(UserId, ContentId)` and `playlist_items(PlaylistId, ContentId)` allow soft-deleted history to coexist with a single active row per pair.
- Cascade only on `playlist_items → playlists` (DB level); other relationships are `NoAction` to avoid cascade-cycle errors. Application-level soft-delete cascades items when a playlist is deleted.
- FluentValidation validators (`CreatePlaylistRequestValidator`, `UpdatePlaylistRequestValidator`, `AddItemToPlaylistRequestValidator`, `AddToWatchlistRequestValidator`).
- New controllers `WatchlistController` and `PlaylistsController`; all endpoints behind `[Authorize]` except public playlist read.
- Migration `AddWatchlistAndPlaylists` creates `watchlists`, `playlists`, `playlist_items` tables with the partial unique indexes above.
- 11 unit tests added (`WatchlistServiceTests` x5, `PlaylistServiceTests` x6) covering happy paths, ownership enforcement, dup prevention, pagination, reorder, public visibility, and soft-delete cascade.
