# Changelog

All notable changes to the Playgo backend will be documented in this file.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Phase 1: DTO Compatibility (Prompt 2)
- Global camelCase JSON serialization (`Program.cs` — `JsonNamingPolicy.CamelCase`, case-insensitive deserialization, omit-null on write, enums as camelCase strings).
- `POST /api/auth/login` accepts both `email` and `emailOrUsername` (legacy callers unaffected; service uses `GetIdentifier()`).
- `POST /api/auth/register` accepts `name` and auto-generates `username` (letters/digits, spaces → `_`, 20-char cap, collision-retry up to 5 times) and splits `name` into `firstName`/`lastName`.
- `AuthResponse` exposes a `token` alias of `accessToken` while keeping `accessToken`, `refreshToken`, `accessTokenExpiry`, and `user`.
- `UserDto` now includes `firstName`, `lastName`, `avatar` (alias of `avatarUrl`), `updatedAt`, and the role is emitted in lowercase (`"user"`/`"admin"`).
- New alias endpoint `GET /api/auth/profile` (delegates to `/api/auth/me`).
- New alias controller `MoviesAliasController` mapping `/api/movies`, `/api/movies/featured`, `/api/movies/trending`, `/api/movies/{id}`, `/api/movies/{id}/related`, `POST /api/movies/{id}/view` to the existing content services. Response envelope is the FE-shaped `{data, total, page, totalPages}`.
- New `MovieDto` with frontend-friendly field names (`poster`, `backdrop`, `trailer`, `streamUrl`, `duration`, `rating`, `genres: string[]`, `cast: string[]`, `quality`, `views`, `likes`, `dislikes`, i18n placeholders for `titleUz`/`titleRu`/`descriptionUz`/`descriptionRu`).
- New `Content.Quality` field (`VideoQuality` enum, default `HD=2`; existing `VideoQuality` enum extended with `UHD4K=4`).
- Migration `AddContentQualityField` adds `Quality` integer column to `contents` with default `2`.

### Backward compatibility
- Existing endpoints `/api/contents/*`, `/api/auth/me`, `/api/auth/login` with `emailOrUsername`, and the original `PagedResult<T>` envelope (`items`/`totalCount`/`pageSize`) all continue to work.
