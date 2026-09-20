# GS Analytics — Sales Intelligence for Small Businesses

A small, genuinely useful sales intelligence app for small businesses: track customers, products and
sales, import a CSV export from wherever you currently keep records, and see deterministic,
explainable analytics — which customers have gone quiet, which products are trending, a simple sales
forecast — without any of it being dressed up as "AI." Every number on every page is either a stored
fact or a documented arithmetic rule you could reproduce by hand.

**Frontend:** hand-built HTML/CSS/vanilla JS (Bootstrap 5, Font Awesome 6, Chart.js — all from cdnjs,
no vendored copies, no build step).
**Backend:** ASP.NET Core Web API (.NET 10) + Entity Framework Core + PostgreSQL, with JWT
authentication and strict per-business data isolation.

It is **not intended to compete with Power BI** and does not present simple arithmetic as artificial intelligence. Its goal is narrower: make everyday sales data useful, understandable and actionable.

```
GSAnalytics/
├── index.html, main.html, stats.html, salesBy*.html,      # frontend pages
│   salesForecasting.html, data.html, insights.html,
│   importData.html, add*.html
├── JS/
│   ├── api.js       shared fetch wrapper (auth header, 401-retry-with-refresh)
│   ├── auth.js       login/register/refresh — access token kept in memory only
│   ├── data.js       all API calls (customers/products/sales/imports/insights) + client-side aggregation
│   ├── nav.js         shared sidebar
│   ├── ui.js           toasts, CSV export
│   └── palette.js     colorblind-safe chart palette
├── CSS/, Images/
└── backend/
    ├── src/
    │   ├── GSAnalytics.Domain            entities only, no framework dependencies
    │   ├── GSAnalytics.Application       security, auth, imports, insights — interfaces + logic
    │   ├── GSAnalytics.Infrastructure    EF Core DbContext, migrations, demo data seeder, implementations
    │   └── GSAnalytics.Api               controllers, Program.cs, appsettings
    ├── tests/GSAnalytics.Tests            xUnit — model tests + full API integration tests
    ├── Dockerfile, docker-compose.yml, .env.example
    └── GSAnalytics.slnx
```

## Architecture notes

- **No repository-wrapping-EF, no MediatR/CQRS.** Controllers talk to `GSAnalyticsDbContext` directly.
  Abstractions exist only where they earn their keep: CSV importing, insights, JWT/current-user
  context.
- **BusinessId is never trusted from the client.** Every business-scoped endpoint reads it from the
  authenticated user's JWT claims (`ICurrentUserService`), never from a route/query/body value. See
  `backend/tests/GSAnalytics.Tests/BusinessIsolationTests.cs` for the tests that prove this.
- **Auth:** short-lived JWT access token (15 min) held only in an in-memory JS variable — never in
  `localStorage`/`sessionStorage` — plus an HttpOnly/Secure/SameSite=Lax refresh cookie backed by a
  `RefreshToken` table with rotation and reuse detection (presenting an already-rotated token revokes
  the whole chain). Because this is a multi-page app, not an SPA, every page silently re-derives a
  fresh access token from the refresh cookie on load.
- **Money is always `decimal`.** `SaleItem.LineTotal` (`Quantity * UnitPrice`) is computed, never
  persisted.
- **CSV import** matches customers/products by name (creating them if unseen) and never blocks on a
  duplicate file — it warns (via a SHA-256 file hash comparison) and imports anyway. Partial success is
  normal: each row succeeds or fails independently, with reasons reported back.
- **Insights are simple, documented rules, not statistics or ML:** a customer "needs attention" when
  they've gone > 1.5× their own average order-to-order gap without a new order; a product is
  "Growing"/"Declining" when its trailing-30-day sales differ from the prior 30 days by more than 10%.

## Running locally

### Backend

Requires the .NET 10 SDK and a local PostgreSQL instance.

```bash
cd backend
dotnet user-secrets set "ConnectionStrings:GSAnalytics" "Host=localhost;Database=gsanalytics;Username=<you>;Password=<yours>" --project src/GSAnalytics.Api
dotnet user-secrets set "Jwt:Key" "$(openssl rand -base64 48)" --project src/GSAnalytics.Api
dotnet run --project src/GSAnalytics.Api
```

In Development, migrations apply and demo data seeds automatically on startup — no separate step
needed. Swagger UI is at `https://localhost:<port>/swagger`.

**Demo account:** `demo@harborpointwholesale.test` / `Demo123!` — a fictional wholesale supplier with
~12 months of intentionally-patterned history (repeat customers, one declining, one overdue, a growing
product, a declining product, seasonal variation) so every analytics page has something real to show.

### Frontend

Any static file server, from the repo root (not `backend/`):

### Customer, product and sales management

- Full CRUD operations for customers and products.
- Sales represented as orders with one or more line items.
- Searchable and filterable business data.
- Customer, product and city drill-down views.

### CSV import

- Import historical sales from CSV.
- Map source columns before processing.
- Automatically create customers and products that do not yet exist.
- Detect previously imported files and warn the user without blocking them.
- Use the included sample CSV to explore the complete import flow.

### Dashboard and analytics

- Summary KPIs.
- Monthly sales trend.
- Category contribution breakdown.
- Recent orders.
- Customer, product and city charts.
- Product trend detection over the most recent 30 days.
- Customer inactivity detection relative to each customer's own ordering rhythm.
- An illustrative linear sales projection with explicit limitations.

### Demonstration data

The application includes a seeded fictional wholesale supplier with one year of realistic sales history. This allows the dashboard, insights and drill-down screens to be evaluated immediately without manually creating data.

## Why this project exists

Many small businesses already have useful sales data but keep it in spreadsheets or disconnected tools. They do not necessarily need an enterprise reporting platform. They need direct answers to practical questions:

- Are monthly sales improving or declining?
- Which customers are becoming inactive?
- Which products are gaining or losing momentum?
- Which categories and cities contribute most to revenue?
- What might sales look like if the present trajectory continues?

GS Analytics addresses those questions with focused workflows and transparent calculations.

## Technology stack

### Backend

- ASP.NET Core
- .NET 10
- C#
- Entity Framework Core
- PostgreSQL
- JWT authentication

### Frontend

- HTML5
- CSS3
- Vanilla JavaScript
- Browser Fetch API

### Delivery

- Docker
- PostgreSQL-backed integration tests

## Architecture

The backend follows a pragmatic layered structure. It deliberately avoids repository and CQRS abstractions that would duplicate Entity Framework Core without adding meaningful value at the current scale.

```text
Browser interface
       │
       │ HTTPS / JSON
       ▼
ASP.NET Core controllers
       │
       │ Business rules + tenant scoping
       ▼
Entity Framework Core
       │
       ▼
PostgreSQL
```

Open the served `index.html`. `JS/api.js` points at `https://localhost:5443/api` for `localhost`/
`127.0.0.1` origins — update it if your backend runs on a different port.

### Docker (backend + Postgres only)

```bash
cd backend
cp .env.example .env   # fill in POSTGRES_PASSWORD and JWT_KEY (openssl rand -base64 48)
docker compose up --build
```

This serves the API over **plain HTTP** on `:8080` (no dev HTTPS cert inside the container), so the
`Secure` refresh cookie won't be set by the browser — fine for exercising the API via Swagger/curl with
a bearer token, but the full frontend login flow needs the HTTPS-served `dotnet run` path above.

### Tests

```bash
cd backend
dotnet test
```

Includes fast EF model-metadata tests and full HTTP integration tests (auth, CRUD, business isolation,
CSV import, insights) via `WebApplicationFactory`. The integration tests need a reachable local
Postgres (same as `dotnet run`, since they reuse the Development auto-migrate/seed path) — they are not
yet hermetic/CI-portable via an ephemeral test database, which is a known, deliberate scope cut for V1.

## Not yet built

- Server-side forecasting (the sales forecast is still a client-side linear regression, same
  deterministic math, just not yet ported to an endpoint).
- Azure/production deployment (explicitly deferred; this is a local-first V1).

## Screenshots

1. Dashboard overview: ![Dashboard](screenshots/dashboard.png)
2. Customer inactivity insights: ![Insights](screenshots/insights.png)
3. Product trend analysis. ![Products](screenshots/insights.png)
4. Customer or product drill-down: ![Sales Forecast](screenshots/sales-forecast.png)
