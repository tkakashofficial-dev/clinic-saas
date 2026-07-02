# Klivia — Developer Guide

*How to work on this codebase without breaking its guarantees.
Read this before your first commit.*

---

## 1. Architecture in one picture

```
frontend/  Angular 21 (zoneless, signals, standalone components)
              │ HTTP + JWT (multi-role claims)
backend/
  Clinic.Api            controllers, middleware, workers   ← composition root
  Clinic.Application    interfaces, DTOs, validators       ← business contracts
  Clinic.Infrastructure EF Core, services, email, PDFs     ← implementations
  Clinic.Domain         entities, enums, constants         ← zero dependencies
```

**Dependency rule (strict):** Domain depends on nothing. Application depends on
Domain. Infrastructure implements Application. Api wires everything. Never
reverse an arrow.

## 2. Local setup (10 minutes)

```bash
# Backend
cd backend
dotnet tool restore
cd Clinic.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=clinic_db;Username=postgres;Password=<yours>"
dotnet user-secrets set "JwtSettings:Secret" "<random 64 chars>"
# optional email: Email:User + Email:Password (Gmail app password)
cd ..
dotnet ef database update --project Clinic.Infrastructure --startup-project Clinic.Api
dotnet run --project Clinic.Api        # http://localhost:5114 (Swagger at /)

# Frontend
cd frontend && npm install && npm start   # http://localhost:4200
```

## 3. The five invariants (never violate these)

1. **Tenant isolation is central, not per-query.** Every `IMustHaveTenant`
   entity gets a global query filter + write guard in `ClinicDbContext`.
   You never *need* `Where(x => x.TenantId == ...)` — but keep it where it
   exists (defense in depth). Opting out (`IgnoreQueryFilters([QueryFilters.Tenant])`)
   is allowed ONLY pre-auth (login/refresh) and in background workers.
2. **Secrets never touch git.** user-secrets locally, env vars in hosting.
   `appsettings.json` holds shape, not values.
3. **Roles are a closed set** (`RoleNames`). `[Authorize]` matches exact
   strings; free-text roles are a security hole. Users may hold several roles
   (JWT carries one claim per role).
4. **Plan limits live in `PlanLimits` only**, enforced via `Tenant.EffectivePlan`
   (handles trial / lapsed-trial / chosen plan). Violations throw
   `PlanLimitException` → HTTP 402 → the UI renders an upgrade prompt.
5. **Errors cross the API boundary in exactly one place** —
   `GlobalExceptionHandler` maps Application exceptions to RFC 7807:
   `NotFound→404, Conflict→409, BadRequest→400, PlanLimit→402, Unauthorized→401`.
   Controllers contain zero try/catch.

## 4. Adding a feature end-to-end (the checklist)

Backend: entity (`Domain/Entities`, private setters, ctor validation)
→ EF config (`Infrastructure/Persistence/Configurations`: lengths, indexes, FKs)
→ `DbSet` in `ClinicDbContext`
→ `dotnet ef migrations add <Name>` (see §6!)
→ DTOs + FluentValidation validator (`Application/Features/<Feature>`)
→ service interface (Application) + implementation (Infrastructure): tenant
comes from `ICurrentUserService`, project with `.Select()` (never return
entities), paginate lists with `ToPagedResultAsync`
→ register in `DependencyInjection`
→ thin controller with `[Authorize(Roles = ...)]`.

Frontend: mirror the DTO in `core/models/api.models.ts` (TypeScript catches
contract drift at compile time) → service in `core/api` → component (signals,
`parseApiError` for errors, drawer pattern for forms) → lazy route +
nav item in `shell.ts` with `roles`.

Tests: at minimum — the happy path, the tenant-isolation path
("clinic B cannot see/do this"), and every thrown exception.

## 5. Testing

- `Clinic.Tests` runs on **SQLite in-memory** (real FKs, unique indexes, query
  filters) — no Postgres needed, CI-friendly. `dotnet test` from `backend/`.
- `TestDb` seeds a full tenant graph; `FakeCurrentUserService.ActAs(...)`
  switches identity in one line; `NoOpEmailSender` records outbound mail.
- **Every new query path needs an isolation test.** This suite is the only
  thing standing between you and a patient-data leak.

## 6. Migrations — learned the hard way

- Never edit an applied migration; write a **new** one (see
  `BackfillTenantPlans` repairing `AddTenantPlans`).
- Adding a NOT NULL column to a table with rows? EF backfills with the type
  default (`""` for strings) — **decide the backfill explicitly** with
  `migrationBuilder.Sql(...)`, or existing rows get garbage semantics.
- Hosted deploys apply migrations automatically via `Database:MigrateOnStartup=true`;
  local dev stays explicit (`dotnet ef database update`).

## 7. Sharp edges worth knowing

- `Enum.TryParse` accepts numeric strings (`"999"` → `(PlanType)999`) —
  always pair with `Enum.IsDefined`.
- Check-then-insert limit checks race under concurrency — wrap in a
  serializable transaction (see `StaffService.AddStaffAsync`).
- `backdrop-filter` makes an element the containing block for
  `position: fixed` children — a full-screen overlay inside it silently
  shrinks to the element (we shipped this bug in the topbar).
- Angular is **zoneless**: state must live in signals; a mutated plain field
  won't re-render.
- Emails must never fail a business flow — `IEmailSender` implementations
  swallow and log.
- Background workers have no HTTP context → tenant filter yields empty; skip
  the Tenant filter explicitly and carry TenantId on each row you create.

## 8. Conventions

Commits: `feat:/fix:/chore:/docs:/test:` + body explaining WHY.
One logical change per commit. Push only after build + tests are green.
API: REST-ish, sub-resources for owned concepts (`/appointment/{id}/consultation`),
paged list envelope everywhere. Frontend: design tokens only — a hardcoded
hex is a review-blocker (see the designer guide).
