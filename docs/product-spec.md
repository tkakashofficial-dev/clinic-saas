# Clinic SaaS — Product Specification

> This document is the single source of truth for WHAT we are building and WHY.
> Code follows this spec; when the spec changes, we change it here first.

## Vision

A **dental clinic management system**, delivered first to a single real clinic,
built from day one as a multi-tenant SaaS so additional clinics can onboard
without rework. Long-term: multi-branch clinics and organization hierarchies.

## Personas & what each needs from the UI

| Persona | Daily reality | UI must optimize for |
|---|---|---|
| **Admin** (owner, often also a doctor) | Runs the business, checks how the clinic is doing, manages staff | Calm overview dashboard, staff management, at-a-glance numbers |
| **Receptionist** | Front desk under time pressure: phone ringing, patient waiting | **Speed**: minimal clicks, big touch targets, keyboard-friendly forms, instant search |
| **Doctor** | Between patients, needs context fast | **Information density**: patient history, conditions and appointments visible without hunting |
| **Patient** (future portal) | Books/views appointments | Simplicity, reassurance |

A user can hold multiple roles (owner who is also a doctor).

## Core workflow (the product IS this flow)

1. Clinic owner visits site → **registers** → system creates Clinic (tenant) + owner user with Admin role + seeds Doctor/Receptionist roles ✅ *built*
2. Owner **adds staff** (doctors, receptionists) ✅ *built*
3. Staff log in; everyone sees only their clinic's data ✅ *built & verified*
4. Receptionist **registers patient** + medical history (master conditions, many-to-many) ✅ *built*
5. Receptionist **creates appointment**, assigns doctor ✅ *built*
6. Doctor sees their appointments for the day ✅ *built (filter by doctor+date)*
7. Doctor consults → records **diagnosis** 🔜 Phase 2
8. Doctor adds **prescription** (medicines) → patient gets **PDF** 🔜 Phase 2
9. **WhatsApp/SMS reminders** 🔜 Phase 3
10. **Subscription & billing** for clinics (the SaaS business model) 🔜 Phase 3
11. Multi-branch, org hierarchy, advanced reports 🔜 Phase 4

## Roadmap

### Phase 0 — Foundation hardening ✅ DONE
Secrets out of git, enforced tenant isolation (global query filters + write guards),
closed role set, RFC 7807 error handling, CORS, missing Appointments migration.

### Phase 1 — API contract freeze (before Angular consumes it)
- Pagination on all list endpoints (`page`, `pageSize` → `PagedResult<T>`)
- Input validation (FluentValidation) — friendly 400s the UI can display per-field
- First xUnit tests (auth, tenant isolation, patients)

### Phase 2 — Angular MVP (demoable product)
- Design system from `docs/design/design-system.md`
- Login / clinic registration
- Receptionist: patient register/search, appointment book/day view
- Doctor: my-day appointment list, patient detail with history
- Admin: staff management, simple dashboard

### Phase 3 — Clinical depth (the sellable core for dentists)
- Consultation: diagnosis notes per appointment
- Prescription module + PDF generation
- Refresh tokens, login rate limiting, audit log of patient-record access

### Phase 4 — Ship it
- CI/CD: GitHub Actions → Azure (App Service + Static Web Apps free tiers)
- Audit log of patient-record access (who viewed what, when)
- Error monitoring + structured logging

## Competitive feature map — the road to "replaces modern systems"

Based on what Dentrix, CareStack, Cliniko and SimplePractice sell. ✅ = we have it.

| Domain | Features | Status |
|---|---|---|
| **Scheduling** | Booking ✅, day view ✅, check-in / waiting room ✅, calendar week view, recurring appointments, online patient self-booking, waitlist | Core ✅ |
| **Clinical** | Consult notes ✅, prescriptions + PDF ✅, medical history ✅ (basic), **dental chart (odontogram)** ⭐, treatment plans with staged visits, allergy alerts, file/X-ray attachments | Core ✅ |
| **Billing** | Treatment price list, invoices per visit, payment tracking, insurance claims, daily cash report | Phase 5 |
| **Patient engagement** | Appointment reminders (WhatsApp/SMS) ⭐ high demand, patient portal, recall campaigns ("6-month cleaning due"), digital consent forms | Phase 5–6 |
| **Operations** | Role dashboards ✅, reports & analytics, audit log, inventory (materials), multi-branch | Phase 4–6 |
| **Platform (Super Admin — the SaaS owner)** | Clinic onboarding & subscription billing, plans/limits, usage analytics, support impersonation — a SEPARATE back-office app, never mixed with clinic UI | Phase 7 |

⭐ = differentiators clinics actually switch products for: the odontogram (visual
tooth chart) and WhatsApp reminders matter more in sales demos than any report.

### Phase 5 — Money features (billing + reminders)
### Phase 6 — Clinical depth (odontogram, treatment plans, attachments, portal)
### Phase 7 — Platform back-office (super admin, subscriptions, multi-branch)

## Decision log

| # | Decision | Why | Date |
|---|---|---|---|
| 1 | **Keep** SystemUser/TenantUser/TenantUserRole (vs simple User.ClinicId) | Already built, working, and isolation-tested. Removing it is a rewrite with zero user value; the vision (visiting dentists, multi-branch, orgs) needs it anyway. Architecture is judged by cost-to-change vs value, not by "simplest possible on paper". | 2026-07-02 |
| 2 | Tenant isolation via EF global query filters + write guards, not per-query `Where` | Central enforcement can't be forgotten; verified cross-tenant reads return 404/empty. | 2026-07-02 |
| 3 | Roles are a closed seeded set (Admin/Doctor/Receptionist) | `[Authorize]` matches exact strings; free-text roles silently break security. | 2026-07-02 |
| 4 | Modular monolith, no microservices, no repository pattern | Right size for the team (1 dev) and stage; EF Core's DbContext already is a repository/UoW. | 2026-07-02 |
| 5 | Errors as RFC 7807 Problem Details from one handler | Standard, machine-readable, no leaked internals; Angular can render `detail` directly. | 2026-07-02 |
| 6 | Dental-first (not generic clinic) | Real client is a dental clinic; niche focus is a selling advantage. Generic naming stays in code (Patient, Appointment) so other verticals remain possible. | 2026-07-02 |
