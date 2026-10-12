# StarterKit: .NET 10 Web API template

![CI](https://github.com/Fmaes2/dotnet-minimal-api-starter/actions/workflows/ci.yml/badge.svg)

A ready-to-use Web API template with Minimal APIs, JWT authentication, PostgreSQL, tests and Docker. Clone it, set your key and start adding your own entities.

## What's included

- **Minimal APIs** organized by feature, using `MapGroup`
- **Authentication** with ASP.NET Core Identity and JWT tokens
- **EF Core + PostgreSQL**, with migrations and pagination
- **Validation** with FluentValidation through a reusable endpoint filter
- **Global error handling** in ProblemDetails format
- **Logging** with Serilog (console and file)
- **Tests**: unit and integration (xUnit, Moq, in-memory SQLite)
- **Docker** and docker-compose
- **CI** with GitHub Actions

## Requirements

- .NET SDK 10
- Docker Desktop (for the database, or for the whole stack)
- Visual Studio 2026, VS Code or Rider
- EF Core CLI tool (one time only): `dotnet tool install --global dotnet-ef`

## Quick start with Docker (5 minutes)

```bash
cp .env.example .env        # on Windows: copy .env.example .env
```
Edit `.env` and set a JWT key of at least 32 characters. Then:

```bash
docker compose up --build
```

The API runs at `http://localhost:8080`. Try `GET /health`, then use `StarterKit.Api/StarterKit.Api.http` to register a user, log in and call the endpoints.

## Local development (API from your IDE, database in Docker)

1. Start only Postgres:
```bash
   docker compose up db -d
```
2. Store the JWT key in user-secrets (it is never committed):
```bash
   dotnet user-secrets set "Jwt:Key" "<a-key-of-at-least-32-characters>" --project StarterKit.Api
```
3. Apply the migrations:
```bash
   dotnet ef database update --project StarterKit.Api
```
4. Run the API from your IDE, or with `dotnet run --project StarterKit.Api`.

In this mode the API listens on the port shown in the console (it comes from `launchSettings.json`), not on 8080. Update the `@host` variable in the `.http` file to match.

## Project structure

```
StarterKit/
├── StarterKit.slnx
├── StarterKit.Api/
│   ├── Endpoints/      Routes grouped by feature
│   ├── Domain/         Entities
│   ├── Data/           DbContext and migrations
│   ├── Auth/           JWT options and token generation
│   ├── Validation/     Validators and validation filter
│   └── Middleware/     Global exception handler
└── StarterKit.Tests/   Unit and integration tests
```

## Adding a new entity

1. Create the class in `Domain/` and add its `DbSet` to `AppDbContext`.
2. Create the request/response contracts and the validator.
3. Copy `ProductEndpoints.cs`, adapt it, and register it in `Program.cs` with `app.MapXxxEndpoints()`.
4. Create and apply the migration:
```bash
   dotnet ef migrations add AddXxx --project StarterKit.Api
   dotnet ef database update --project StarterKit.Api
```

## Tests

```bash
dotnet test
```
Integration tests use in-memory SQLite, so they need neither Docker nor Postgres. Keep in mind that SQLite and Postgres can differ in small details (for example, text sorting).

## Troubleshooting

- **"Jwt:Key is missing or too short"**: set the key with user-secrets (local development) or with the `JWT_KEY` variable in `.env` (Docker).
- **"Set JWT_KEY in the .env file"** when running `docker compose up`: you skipped the step that creates `.env`. Copy `.env.example` to `.env` and set your key.
- **HTTPS certificate warning** when running with `dotnet run`: run `dotnet dev-certs https --trust`.
- **Port already in use**: change the left-hand side of the port mapping in `docker-compose.yml` (for example `"8081:8080"` for the API, or `"5434:5432"` for the database). If you change the database port, update `appsettings.Development.json` too.
- **`dotnet ef` not found**: install it with `dotnet tool install --global dotnet-ef`.

## Security: what to change before deploying

- **Generate your own JWT key** and pass it as an environment variable (`Jwt__Key`). Never commit it.
- Change the Postgres password in the compose file; the current one is just an example.
- Set `ASPNETCORE_ENVIRONMENT=Production` and put the API behind HTTPS.
- This v1 does not include refresh tokens, roles or rate limiting.
- Automatic migrations on startup (`Database__MigrateOnStartup`) are convenient to get started. In production it is usually better to apply migrations as a separate deployment step.

## Dependency licenses

| Package | License |
|---|---|
| Microsoft packages (ASP.NET Core, EF Core) | MIT |
| Npgsql | PostgreSQL License |
| Serilog | Apache 2.0 |
| FluentValidation | Apache 2.0 |
| xUnit | Apache 2.0 |
| Moq | BSD-3-Clause |

## License

Released under the [MIT License](LICENSE).

## Feedback

Missing something? Open a [Discussion](../../discussions) and tell me what you struggle with when building APIs (refresh tokens, roles, multi-tenancy...). It helps me decide what to build next.