# Clinic SaaS 🏥

A multi-tenant clinic management system — one deployment serves many clinics with strict per-clinic data isolation. Built with .NET 10, EF Core, PostgreSQL, and Angular.

> **Status:** in active development. See the [roadmap](#roadmap) below.

## Why this project is built the way it is

| Decision | Reason |
|---|---|
| **Clean Architecture** (Domain → Application → Infrastructure → API) | Business rules are independent of frameworks and the database; each layer is testable in isolation. |
| **Multi-tenancy via global query filters** | Tenant isolation is enforced centrally in the `DbContext`, so a forgotten `WHERE TenantId = ...` cannot leak one clinic's patients to another. |
| **`SystemUser` / `TenantUser` split** | One login account can belong to multiple clinics with different roles — the standard SaaS identity model. |
| **JWT carries `tenant_id`** | The server derives the tenant from the verified token, never from client input. |
| **Secrets via user-secrets / environment variables** | No credentials in source control, ever. |

## Tech stack

- **Backend:** ASP.NET Core 10 Web API, EF Core 10, PostgreSQL (Npgsql)
- **Auth:** JWT bearer tokens, BCrypt password hashing, role-based authorization
- **Frontend:** Angular (standalone components)
- **Testing:** xUnit + SQLite in-memory (tenant isolation, auth, services, validators)
- **CI/CD:** GitHub Actions → Azure App Service + Azure Static Web Apps *(planned)*

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [PostgreSQL 16+](https://www.postgresql.org/download/)
- [Node.js 20+](https://nodejs.org/) (frontend)

### Backend setup

```bash
cd backend

# Restore local tools (dotnet-ef)
dotnet tool restore

# Configure secrets (never committed to git)
cd Clinic.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=clinic_db;Username=postgres;Password=<your-password>"
dotnet user-secrets set "JwtSettings:Secret" "<random-64-char-string>"
cd ..

# Create the database schema
dotnet ef database update --project Clinic.Infrastructure --startup-project Clinic.Api

# Run the API (Swagger UI at the root URL in Development)
dotnet run --project Clinic.Api
```

### Frontend setup

```bash
cd frontend
npm install
npm start   # http://localhost:4200
```

## Project structure

```
backend/
  Clinic.Domain/          # Entities, enums, domain rules — no dependencies
  Clinic.Application/     # Use-case interfaces + DTOs — depends only on Domain
  Clinic.Infrastructure/  # EF Core, services, JWT — implements Application interfaces
  Clinic.Api/             # Controllers, middleware, composition root
frontend/                 # Angular app
docs/
  design/                 # UI mockups + design system
  product-spec.md         # Product vision, workflows, decision log
```

## Roles

| Role | Can do |
|---|---|
| **Admin** | Everything: manage staff, patients, appointments |
| **Doctor** | View patients, view/update appointments |
| **Receptionist** | Register patients, create/manage appointments |

## Roadmap

- [x] Multi-tenant domain model (tenants, users, roles)
- [x] Auth: register clinic, login, JWT with tenant claims
- [x] Patients: register, list, search
- [x] Appointments: create, list/filter, status updates
- [x] Tenant isolation enforced by global query filters
- [x] Centralized error handling (RFC 7807 Problem Details)
- [x] Input validation (FluentValidation)
- [x] Pagination on all list endpoints
- [x] Unit tests (xUnit + SQLite in-memory)
- [ ] Refresh tokens + login rate limiting
- [ ] Angular frontend (login, patients, appointments, staff)
- [ ] CI/CD: GitHub Actions → Azure
- [ ] Audit log of patient-record access
