# Playgo Backend

Video streaming platform backend built with .NET 8, PostgreSQL, Redis, and Docker.

## Tech Stack
- **Runtime**: .NET 8 (ASP.NET Core Web API)
- **Database**: PostgreSQL 16 + Entity Framework Core 8
- **Cache**: Redis 7
- **Auth**: JWT Bearer (access 15min + refresh 7days)
- **Background jobs**: Hangfire
- **Logging**: Serilog
- **Docs**: Swagger / OpenAPI
- **Architecture**: Clean Architecture (Domain → Application → Infrastructure → API)

---

## Quick Start (Docker)

Requirements: Docker + Docker Compose

```bash
git clone <your-repo-url>
cd playgo-backend
cp .env.example .env
# Edit .env: set DB_PASSWORD and JWT_SECRET
docker-compose up -d
```

URLs:
- API: http://localhost:8080
- Swagger: http://localhost:8080/swagger
- Hangfire dashboard: http://localhost:8080/hangfire
- Health check: http://localhost:8080/health

---

## Local Development (without Docker)

Requirements: .NET 8 SDK, PostgreSQL, Redis running locally.

```bash
# Restore packages
dotnet restore

# Update connection strings in src/Playgo.API/appsettings.Development.json

# Run migrations
dotnet ef migrations add InitialCreate \
  --project src/Playgo.Infrastructure \
  --startup-project src/Playgo.API \
  --output-dir Persistence/Migrations

dotnet ef database update \
  --project src/Playgo.Infrastructure \
  --startup-project src/Playgo.API

# Run
cd src/Playgo.API
dotnet run
```

---

## API Endpoints

### Auth — /api/auth
| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| POST | /register | — | Ro'yxatdan o'tish |
| POST | /login | — | Kirish |
| POST | /refresh | — | Token yangilash |
| POST | /logout | ✓ | Chiqish |
| GET | /me | ✓ | Profil |
| PUT | /me | ✓ | Profilni yangilash |
| PUT | /me/password | ✓ | Parol o'zgartirish |

### Contents — /api/contents
| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| GET | / | — | Katalog (filter + pagination) |
| GET | /featured | — | Featured kontentlar |
| GET | /trending | — | Trending kontentlar |
| GET | /{id} | — | Kontent detali |
| GET | /slug/{slug} | — | Slug bo'yicha |
| GET | /{id}/similar | — | O'xshash kontentlar |
| POST | /{id}/view | — | Ko'rishni hisoblash |

### Admin — /api/admin/contents
| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| POST | / | Admin | Kontent yaratish |
| PUT | /{id} | Admin | Kontent yangilash |
| DELETE | /{id} | Admin | O'chirish |
| POST | /{id}/seasons | Admin | Mavsum qo'shish |
| POST | /seasons/{id}/episodes | Admin | Epizod qo'shish |

### Other endpoints
- GET /api/genres — Janrlar ro'yxati
- POST /api/genres [Admin] — Janr yaratish
- GET,POST,PUT,DELETE /api/reviews — Sharhlar
- GET,POST,DELETE /api/favorites [Auth] — Sevimlilar
- GET,PUT,DELETE /api/watch-history [Auth] — Ko'rish tarixi
- POST /api/upload/image [Admin] — Rasm yuklash
- POST /api/upload/video [Admin] — Video yuklash

### Watchlist — /api/watchlist [Auth]
"Save for later" — separate from Favorites. Soft-deleted entries are revived on re-add so duplicates can't accumulate.
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | / | Paged watchlist (ordered by `priority` asc then `createdAt` desc). |
| POST | /{contentId} | Add. Optional JSON body `{ priority, note }`. Re-add of a removed entry revives the original row. |
| DELETE | /{contentId} | Soft-remove. |
| GET | /{contentId}/check | `{ isInWatchlist: bool }`. |
| PUT | /{contentId}/priority | Body `{ priority, note }`. |

### Playlists — /api/playlists
User-created collections of content. Owner has full CRUD; public playlists are visible to everyone.
| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| GET | /mine | ✓ | Paged playlists of the caller. |
| GET | /public | — | Paged public playlists, optional `?search=`. |
| GET | /{id} | optional | Detail with ordered items. Private playlists return 400 to non-owners. |
| POST | / | ✓ | Create. Body `{ name, description?, isPublic, coverImageUrl? }`. |
| PUT | /{id} | ✓ owner | Rename / change visibility / cover. |
| DELETE | /{id} | ✓ owner | Soft-delete playlist AND its items. |
| POST | /{id}/items | ✓ owner | Add `{ contentId, orderIndex? }`. Defaults to append. Duplicates rejected. |
| DELETE | /{id}/items/{itemId} | ✓ owner | Remove item. |
| PUT | /{id}/reorder | ✓ owner | Body `{ itemIdsInOrder: [guid, …] }` — must list every current item exactly once; OrderIndex is reassigned by position. |

---

## Environment Variables

| Variable | Description | Example |
|----------|-------------|---------|
| DB_PASSWORD | PostgreSQL password | strong_pass_here |
| JWT_SECRET | JWT signing key (min 32 chars) | random_string_min_32_chars |
| API_BASE_URL | Public API URL | https://api.playgo.uz |
| FRONTEND_URL | Frontend origin for CORS | https://playgo.uz |

---

## Project Structure

```
src/
├── Playgo.Domain/          # Entities, Enums, BaseEntity (no dependencies)
├── Playgo.Application/     # Services, DTOs, Validators, Interfaces
├── Playgo.Infrastructure/  # EF Core, JWT, BCrypt, Redis, Hangfire
└── Playgo.API/             # Controllers, Middleware, Program.cs
tests/
└── Playgo.Tests/           # Unit tests (xUnit + Moq)
```

---

## Roadmap (keyingi bosqich)
- [ ] HLS transcoding — FFmpeg worker service
- [ ] CDN integration — Bunny.net signed URLs
- [ ] Full-text search — PostgreSQL tsvector
- [ ] Keyset pagination — OFFSET o'rniga cursor-based
- [ ] Redis caching — Featured/Trending/Genre endpointlar uchun
- [ ] Email verification — SMTP + confirmation link
- [ ] Admin analytics dashboard endpoint
