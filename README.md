# Flash Cards Multiple API

Flash Cards Multiple is an application for building flash cards that hold **multiple cards under a single topic**. Instead of one question and one answer, each topic (a "series") carries up to three related cards that you study together. That makes it useful for:

- learning several languages at the same time (one topic, one card per language);
- studying multiple terms for the same concept;
- multiple-choice style items, where one prompt has several possible responses;
- anything where several associated combinations are easier to learn side by side.

> **Status:** this project is under active development and is not a finished product. Features and behaviour may change.

This repository is the ASP.NET Core Web API (.NET 10, EF Core with SQLite) behind the Angular front end in `flash-card-multiple`. It provides user registration and login (JWT) and the endpoints for creating, reading, updating and deleting each user's card series.

- How to use the app: see [USER-GUIDE.md](USER-GUIDE.md).
- Developer commands, architecture and configuration: see [CLAUDE.md](CLAUDE.md).

## Quick start

```powershell
# Set the JWT signing key once (at least 32 characters)
dotnet user-secrets set "Jwt:Key" <value> --project flash-cards-multiple-api

# Create or update the database
dotnet ef database update --project flash-cards-multiple-api

# Run the API on https://localhost:7100
dotnet run --project flash-cards-multiple-api --launch-profile https

# Run the tests
dotnet test
```
