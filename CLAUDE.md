# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

ASP.NET Core Web API (.NET 10) for a flash-cards application. Contains the EF Core data layer (`Data/`), the card-series and auth endpoints, and an integration test project (`flash-cards-multiple-api.Tests/`).

**Solution file:** `flash-cards-multiple-api.slnx`  
**Project:** `flash-cards-multiple-api/flash-cards-multiple-api.csproj`  
**Namespace root:** `flash_cards_multiple_api`

## Commands

```powershell
# Run the API (Development) on https://localhost:7100 and http://localhost:5228.
# `https` is the first launch profile, so plain `dotnet run --project flash-cards-multiple-api` is the same.
# The Angular dev proxy targets :7100, so use this one when working with the front end.
dotnet run --project flash-cards-multiple-api --launch-profile https

# HTTP only (http://localhost:5228); the Angular proxy cannot reach this (502)
dotnet run --project flash-cards-multiple-api --launch-profile http

# Build
dotnet build

# Run all tests
dotnet test

# Run a single test
dotnet test --filter "FullyQualifiedName~<TestClassName>.<MethodName>"

# Docker
docker build -f flash-cards-multiple-api/Dockerfile -t flash-cards-multiple-api flash-cards-multiple-api/
```

**Dev URLs:** `http://localhost:5228` (HTTP) / `https://localhost:7100` (HTTPS)  
**OpenAPI spec** (Development only): `GET /openapi/v1.json`

## Architecture

Minimal Web API setup in `Program.cs` — services registered there, controllers auto-discovered via `MapControllers()`. OpenAPI is enabled in Development only via `MapOpenApi()`.

Controllers live in `flash-cards-multiple-api/Controllers/` and use `[ApiController]`. Routes carry an explicit `api/` prefix (e.g. `[Route("api/card-series")]`) because the Angular dev proxy forwards `/api/*` unchanged and its `apiBaseUrl` is `/api`.

`CardSeriesController` implements the Angular `SPEC.md` §6.2 card-series contract: `GET` (summaries, or full with `?include=cards`), `GET {id}`, `POST`, `PUT {id}`, `DELETE {id}`. DTOs are in `Models/CardSeriesModels.cs` (camelCase JSON matching `card.models.ts`). Validation is manual (inputs trimmed; topic 1–200, front 1–500, back 1–1000, 1–3 cards at positions 0..n-1) and returns RFC 9457 `ValidationProblem` with keys like `cards[0].front`. `PUT` returns `409` when `concurrencyStamp` is stale.

**Auth** (`AuthController`, `api/auth`): `register` (201 / 400 / 409), `login` (200 `LoginResponse` / 401 / 429), `logout` (204, stateless), `me`. Passwords use ASP.NET `PasswordHasher<User>`; emails are trimmed and lower-cased. Tokens are HS256 JWTs (`Services/TokenService.cs`, `sub` claim = user id, `MapInboundClaims = false`) validated by `AddJwtBearer` in `Program.cs`. Config: `Jwt:Issuer/Audience/ExpiresMinutes` in `appsettings.json`; **`Jwt:Key` (≥32 chars) lives only in User Secrets** (`dotnet user-secrets set "Jwt:Key" <value> --project flash-cards-multiple-api`) and the app refuses to start without it. Register/login share a per-IP rate limit (`RateLimit:AuthPermitLimit`, default 10/min, then 429). `CardSeriesController` is `[Authorize]` and every query goes through `Mine()`, so another user's series returns `404`, never `403`. `CardSeries.UserId` is nullable only because the seeded Dog/Cat series predate users; they are owned by nobody and invisible through the API. They act as templates: `Register` copies every unowned series (with new ids and stamps) to the new user in the same `SaveChanges` as the user, so each user starts with their own Dog and Cat. Existing users are not back-filled.

## Database

SQLite via EF Core (`Microsoft.EntityFrameworkCore.Sqlite`). `Data/AppDbContext.cs` is registered in `Program.cs` with `UseSqlite`, reading `ConnectionStrings:DefaultConnection` (`Data Source=flashcards.db`, created in the working directory on first connect). Schema mirrors the Angular app's models (`flash-card-multiple/src/app/core/cards/card.models.ts`): `CardSeries` (topic, optional image URL, timestamps, `ConcurrencyStamp` concurrency token) has 1–3 `Card`s (`Position` 0–2, unique per series, enforced by index and check constraint; front/back length limits match the editor validators). Entities are in `Data/Entities/`, configuration and seed data in `AppDbContext`. The Angular app's two placeholder series (Dog, Cat) are seeded with fixed GUIDs via `HasData`. `User` owns series (see Auth above).

Schema changes use EF migrations (`dotnet-ef` global tool): `dotnet ef migrations add <Name> --project flash-cards-multiple-api` then `dotnet ef database update --project flash-cards-multiple-api` (run from the repo root; drop `--no-build`, since a stale build silently misses new migrations). Migrations are not applied automatically at startup. In Docker the `.db` file is lost with the container unless a volume is mounted and the connection string points at it.

User Secrets are configured (`UserSecretsId` in the .csproj) — use them for any API keys or connection strings in development rather than `appsettings.json`.

Docker support is included (Linux target, ports 8080/8081).

## Tests

`flash-cards-multiple-api.Tests/` (xUnit) holds integration tests that boot the real app through `WebApplicationFactory<Program>` (`Program` is exposed with `public partial class Program;`). `ApiFactory` swaps in a private in-memory SQLite database built by running the real migrations (so the sample series are present), supplies a test `Jwt:Key` via `UseSetting` (no user-secrets needed), and sets a very high auth rate limit so the suite isn't throttled; `CreateUserClientAsync()` registers a fresh user and returns a client with a bearer token. Tests never touch `flashcards.db`. A new migration is exercised automatically because the factory migrates on startup.

One EF gotcha the tests caught: add new `Card`s with `db.Cards.Add(...)`, not through the tracked `series.Cards` collection. A preset-key entity attached that way is tracked as Modified, and the failed UPDATE surfaces as a false 409.
