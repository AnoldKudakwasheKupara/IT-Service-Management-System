# IT Service Management System

An enterprise ASP.NET Core MVC application that combines an IT service desk with a full
**Integrated Management System (IMS)** for ISO 9001:2015 and ISO/IEC 27001:2022, plus an
evidence-grounded **Compliance Intelligence Engine (ECIE)**.

## Tech stack

| Area | Choice |
|---|---|
| Framework | ASP.NET Core MVC, **.NET 10** |
| ORM / DB | Entity Framework Core 10, **SQL Server** |
| Auth | Custom session-based auth (PBKDF2, DB-backed session revocation, email/authenticator MFA) — *not* ASP.NET Identity |
| Frontend | Bootstrap 5, Font Awesome, jQuery, Toastr, DataTables, SignalR |
| Reporting | QuestPDF (PDF), ClosedXML (Excel) |
| Other | Serilog, Redis (optional cache + data-protection), MailKit/SendGrid email, Tesseract OCR (optional) |

## Modules

Dashboard · Helpdesk / Tickets · Assets · CMDB · Problems · Changes · SLA Policies ·
Users · Departments · Meeting Minutes · Exit Clearance · Employee Files (EFM) · Audit Logs ·
Security · **IMS / ISO** (Document Control, Internal Audits, CAPA, Non-Conformance, Risk,
Suppliers, Training, Management Review, Objectives, Compliance, Improvement, Evidence, Reports) ·
**ECIE** (Compliance Intelligence Engine, Compliance Health, Audit Mode).

## Prerequisites

- [.NET SDK 10.0.3xx](https://dotnet.microsoft.com/download) (pinned in `global.json`)
- SQL Server (LocalDB, Express, or full) reachable via the `DefaultConnection` string
- (Optional) Redis, an SMTP/SendGrid account, a `tessdata` folder for OCR

## Getting started

```bash
# 1. Configure the database connection (dev)
cd "IT Service Management System"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\\MSSQLLocalDB;Database=ITSM;Trusted_Connection=True;TrustServerCertificate=True"

# 2. Run — migrations are applied automatically on startup in Development
dotnet run
```

The app starts on the URLs in `Properties/launchSettings.json`. The default route is the
login page. In **Development**, demo accounts are seeded (see *Configuration* below) and the
MFA OTP is shown on the verification page.

## Build & test

```bash
dotnet build "IT Service Management System.slnx" -c Release
dotnet test  "IT Service Management System.slnx"
```

CI (`.github/workflows/ci.yml`) runs restore + build + test on every push/PR.

## Configuration (key flags)

Configuration comes from `appsettings.json`, environment-specific files, environment variables,
and user-secrets (dev). **Provide production secrets via environment variables or a secret store —
never commit them.**

| Key | Default | Notes |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | — | SQL Server connection |
| `ConnectionStrings:Redis` / `Redis:Configuration` | — | Enables Redis cache + data-protection when set |
| `Database:MigrateOnStartup` | `true` | Auto-apply migrations on boot. **Set `false` in production** and migrate as a deploy step. |
| `Demo:Seed` | `true` | Seeds demo login accounts. **Set `false` in production.** |
| `EFM:Ocr:Provider` | plaintext | Set to `tesseract` to OCR images/scanned PDFs (needs `tessdata`) |
| `Security:Av:Provider` | heuristic | Set to `clamav` (+ host/port) to scan uploads via ClamAV |

> ⚠️ **Production hardening (do before deploying):** disable `Database:MigrateOnStartup` and
> `Demo:Seed`, replace the bootstrap admin account, provide all secrets via env vars, set a real
> `AllowedHosts`, and run behind HTTPS with the reverse proxy forwarding `X-Forwarded-*`.

## Deployment

A multi-stage `Dockerfile` is provided:

```bash
docker build -t itsm .
docker run -p 8080:8080 -e ConnectionStrings__DefaultConnection="..." -e ASPNETCORE_ENVIRONMENT=Production itsm
```

Optional OCR / PDF-to-image features need extra native packages in the runtime image.

## Solution layout

```
IT Service Management System/           # the web app
  Controllers/  Models/  Services/  ViewModels/  Views/  Helpers/  Filters/  Middleware/
  DbContexts/   Migrations/  Hubs/  wwwroot/
IT Service Management System.Tests/     # xUnit unit tests
IT Service Management System.slnx       # solution
```
