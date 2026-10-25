# StarterKit: .NET 10 Web API template

![CI](https://github.com/Fmaes2/dotnet-minimal-api-starter/actions/workflows/ci.yml/badge.svg)

A ready-to-use Web API template with Minimal APIs, JWT authentication, PostgreSQL, tests and Docker. Clone it, set your key and start adding your own entities.

## What's included

- **Minimal APIs** organized by feature, using `MapGroup`
- **Authentication** with ASP.NET Core Identity, short-lived JWT access tokens and rotating refresh tokens
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

The API runs at `http://localhost:8080`. Try `GET /health/live` and `GET /health/ready`, then use `StarterKit.Api/StarterKit.Api.http` to register a user, log in and call the endpoints.

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
   Run this command again whenever you pull changes that include new migrations.
4. Run the API from your IDE, or with `dotnet run --project StarterKit.Api`.

In this mode the API listens on the port shown in the console (it comes from `launchSettings.json`), not on 8080. Update the `@host` variable in the `.http` file to match.

## API endpoints

| Method | Route | Auth | Description |
|---|---|---|---|
| GET | `/health/live` | No | Liveness check: the process is up (does not check the database) |
| GET | `/health/ready` | No | Readiness check: also verifies the database connection (returns 503 if it fails) |
| POST | `/api/auth/register` | No | Create a user |
| POST | `/api/auth/login` | No | Returns an access token and a refresh token |
| POST | `/api/auth/refresh` | No | Exchanges a refresh token for a new token pair |
| POST | `/api/auth/logout` | No | Revokes a refresh token (and its whole session) |
| POST | `/api/auth/logout-all` | Yes | Revokes every refresh token of the current user |
| GET | `/api/auth/me` | Yes | Id and email of the authenticated user |
| GET | `/api/products` | Yes | List products (paginated with `page` and `pageSize`) |
| GET | `/api/products/{id}` | Yes | Get a product |
| POST | `/api/products` | Yes | Create a product (you become its owner) |
| PUT | `/api/products/{id}` | Yes, owner only | Update a product |
| DELETE | `/api/products/{id}` | Yes, owner only | Delete a product |

- `POST /api/auth/register`, `POST /api/auth/login` and `POST /api/auth/refresh` return `429 Too Many Requests` with a `Retry-After` header when the limit is exceeded.
- Use `/health/live` for liveness probes (restart the container if it fails) and `/health/ready` for readiness probes (stop sending traffic if it fails).

Protected endpoints expect an `Authorization: Bearer <access token>` header, with the access token returned by the login or refresh endpoints. In the Development environment, the OpenAPI document is served at `/openapi/v1.json`.

## Refresh tokens

Login returns two tokens:

```json
{
  "accessToken": "eyJhbGciOi...",
  "accessTokenExpiresAt": "2026-10-24T15:45:00Z",
  "refreshToken": "q3Zx...",
  "refreshTokenExpiresAt": "2026-10-31T15:30:00Z"
}
```

- The **access token** is a JWT that lasts 15 minutes. Send it in the `Authorization` header.
- The **refresh token** is an opaque random value that lasts 7 days. When the access token expires, send the refresh token to `POST /api/auth/refresh` (`{ "refreshToken": "..." }`) to get a new pair.

How it works:

- **Storage**: only a SHA-256 hash of each refresh token is stored in the database, never the token itself.
- **Rotation**: every refresh token can be used once. Using it returns a new refresh token and invalidates the old one.
- **Reuse detection**: if an already used refresh token is presented again, the API assumes it was stolen and revokes the whole session (every token issued since that login). The client has to log in again.
- **Reuse leeway**: a rotated token reused within a few seconds of its rotation (10 s by default) gets `401` but does **not** revoke the session. This covers a second tab or a slow retry that sent the old token before seeing the new one: the client can pick up the new token and carry on. Outside the leeway, reuse detection applies as usual.
- **Logout**: `POST /api/auth/logout` revokes the session of the given refresh token. It always returns `204`, so it does not reveal whether a token exists. `POST /api/auth/logout-all` revokes every session of the authenticated user.

Configuration (section `Jwt`):

| Setting | Default | Description |
|---|---|---|
| `ExpiresMinutes` | 15 | Access token lifetime |
| `RefreshTokenDays` | 7 | Refresh token lifetime |
| `RefreshTokenReuseLeewaySeconds` | 10 | Seconds after a rotation during which reusing the old token does not revoke the session. `0` disables the leeway |

> **Breaking change**: the login response used to be `{ "token", "expiresAt" }`. It is now `{ "accessToken", "accessTokenExpiresAt", "refreshToken", "refreshTokenExpiresAt" }`, and the default access token lifetime went from 60 to 15 minutes.

## Project structure

```
StarterKit/
├── StarterKit.slnx
├── StarterKit.Api/
│   ├── Endpoints/      Routes grouped by feature
│   ├── Domain/         Entities
│   ├── Data/           DbContext and migrations
│   ├── Auth/           JWT options, access tokens and refresh tokens
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
- **`500` on login after pulling new changes**, with `relation "RefreshTokens" does not exist` (or another missing table) in the logs: the database is missing a migration. Run `dotnet ef database update --project StarterKit.Api`.

## Security: what to change before deploying

- **Generate your own JWT key** and pass it as an environment variable (`Jwt__Key`). Never commit it.
- Change the Postgres password in the compose file; the current one is just an example.
- Set `ASPNETCORE_ENVIRONMENT=Production` and put the API behind HTTPS.
- Refresh tokens are returned in the response body. For browser-based clients (SPAs), consider storing them in an `HttpOnly`, `Secure`, `SameSite=Strict` cookie instead; this template does not do that yet.
- The rate limiter keys on the client IP. Behind a reverse proxy or load balancer, configure forwarded headers so it sees the real client IP and not the proxy's. The counters are kept in memory, so they are not shared between several instances.
- Automatic migrations on startup (`Database__MigrateOnStartup`) are convenient to get started. In production it is usually better to apply migrations as a separate deployment step.

## Who is this for?

Developers learning ASP.NET Core, or anyone who wants a small working API to start from instead of an empty project. It's a starting point, not a production-ready base.

## Known limitations

- Products are visible to every authenticated user. Only the owner can update or delete a product, but there are no roles yet (for example, no admin who can edit any product).
- Registration is open. Login, registration and refresh are rate limited per IP (5 requests per minute by default, see `RateLimiting:Auth`), but there is no per-account lockout.
- Access tokens cannot be revoked: after a logout they remain valid until they expire (15 minutes at most).
- The reuse leeway trades some detection for usability: if an attacker rotates a stolen token first and the real client reuses it within the leeway, the theft is not detected. The attacker still gets nothing from the old token, but keeps the new one. The default of 10 seconds is a reasoned guess, not a measured value: every reuse is logged with the elapsed seconds (`ElapsedSeconds`), so you can check the real distribution in your deployment and adjust `Jwt:RefreshTokenReuseLeewaySeconds`.
- Every rotation extends the refresh token lifetime by 7 days, so there is no absolute session limit yet.
- Expired and revoked refresh tokens are not cleaned up from the database automatically.

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

Missing something? Open a [Discussion](../../discussions) and tell me what you struggle with when building APIs (roles, multi-tenancy, observability...). It helps me decide what to build next.