# Changelog

All notable changes to the Playgo backend will be documented in this file.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Phase 2: User Preferences (Prompt 3)
- New `UserPreferences` entity with `Language` (PreferredLanguage: Uz/Ru/En), `Quality` (PreferredQuality: Auto/SD480/HD720/FHD1080/UHD4K), `Autoplay`, `EmailNotifications`, `PushNotifications`.
- `User.Preferences` 1:1 navigation; unique index on `user_preferences.UserId` with cascade delete.
- New endpoints `GET /api/auth/me/preferences` and `PUT /api/auth/me/preferences` (PATCH semantics — only supplied fields are updated; invalid enum values return 400).
- Default preferences (Language=En, Quality=Auto, Autoplay=true, EmailNotifications=true, PushNotifications=false) auto-created on user registration; also lazily created on first GET for legacy users.
- Migration `AddUserPreferences` adds `user_preferences` table with FK to `users.Id` ON DELETE CASCADE.
- 3 unit tests added in `AuthServicePreferencesTests` covering default-on-get, PATCH semantics, invalid-enum failure.

### Phase 2: i18n Support (Prompt 4)
- Multi-language Content via `ContentTranslation` entity (`LanguageCode`, `Title`, `OriginalTitle`, `Description`, `ShortDescription`, `Director`, `Cast`) with unique index on `(ContentId, LanguageCode)`.
- Multi-language Genre via `GenreTranslation` entity (`LanguageCode`, `Name`, `Description`) with unique index on `(GenreId, LanguageCode)`.
- `ILocalizationContext` resolves the active request language from `?lang=` query string or `Accept-Language` header, falling back to `en`. Bound to `IHttpContextAccessor` and registered as scoped.
- `ContentService` localizes `GetById`, `GetBySlug`, `GetContents`, featured, trending, similar — top-level `title`/`description`/`director`/`cast` are swapped for the active language when a translation exists, with graceful fallback to the original columns.
- `ContentDetailDto` now carries the full `translations` array on every detail response.
- `CreateContentRequest` and `UpdateContentRequest` accept an optional `translations: [...]` array; update replaces the previous set atomically.
- Admin endpoints for translation management:
  - `GET / POST / DELETE /api/admin/contents/{contentId}/translations[/{lang}]`
  - `POST / DELETE /api/genres/{genreId}/translations[/{lang}]`
- Public endpoints:
  - `GET /api/contents/{id}/translations`
  - `GET /api/genres/{genreId}/translations`
- Migrations `AddContentTranslations` and `AddGenreTranslations` create the new tables, FK ON DELETE CASCADE, unique composite indexes, and idempotently backfill an `en` row from the existing primary columns for every non-deleted content/genre.
- 4 unit tests added in `ContentTranslationsTests` (create with translations, update replaces set, `?lang=` returns localized title with fallback, upsert idempotency for `(contentId, languageCode)`).
