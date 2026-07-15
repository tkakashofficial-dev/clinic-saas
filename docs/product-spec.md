# Klivia — Product Specification

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
| **Operations** | Role dashboards ✅, multi-clinic switcher ✅, reports & analytics, audit log, **pharmacy & inventory** (dispensing, stock, batch/expiry), multi-branch | Phase 4–6 |
| **India-specific** | **ABDM / ABHA compliance** (govt health ID — competitors sell this hard), multi-language prescriptions (Malayalam), UPI payments, WhatsApp-first communication | Phase 5–6 |
| **Platform (Super Admin — the SaaS owner)** | Clinic onboarding & subscription billing, plans/limits, usage analytics, support impersonation — a SEPARATE back-office app, never mixed with clinic UI | Phase 7 |

⭐ = differentiators clinics actually switch products for: the odontogram (visual
tooth chart) and WhatsApp reminders matter more in sales demos than any report.

### Phase 5 — Money features (patient billing/invoices + WhatsApp reminders + Razorpay)
### Phase 6 — Clinical depth (odontogram, treatment plans, attachments, portal, permission-based custom roles)
### Phase 7 — Platform back-office (super admin ✅ SHIPPED v1, subscriptions, multi-branch)
v1 shipped 2026-07-14: `/platform` console (email-allowlist gated, server-side
re-check on every call) lists every clinic with plan/trial/staff/patient
counts; owner can change plans (applied manually after UPI/bank payment —
Razorpay automates this later) and suspend/re-activate clinics (suspension
blocks sign-in at token issue; data untouched).

### Clinic forms & templates (validated by a REAL clinic's paper form)
A Nadapuram dental clinic's actual intake sheet (patient ID JDC000486,
alerts box, disease checklist, oral exam grid, bilingual consent) proved the
demand. Phased plan:
- **v1 (SHIPPED)**: printable clinic-branded intake form PDF, pre-filled with
  registration data (patient number, name, age, sex, phone, address, date);
  clinical sections stay blank for the doctor's pen — the paper→digital
  bridge clinics actually adopt. Human patient numbers (P-000123, per-clinic
  sequence) shipped with it.
- **v2 (SHIPPED 2026-07-14)**: two seeded template designs available
  to every clinic — **dental** (oral health, ortho, intra/extra oral exam) and
  **general** (vitals strip, general + systemic examination). Picked per print
  from the patient record drawer (`?template=` on the API), and the clinic's
  **default template** is chosen by the Admin in **Settings** (template picker
  cards; stored as `Tenant.DefaultIntakeTemplate`; the default leads with a ★
  in the drawer). Settings is also the clinic's LETTERHEAD: name/phone/address
  printed on every prescription and intake form. Editing rights: clinic ADMIN
  manages templates, doctors use them. Still to come: toggleable sections,
  clinic logo, Malayalam consent text.
- **v3**: full form BUILDER — custom fields, digital filling on tablet,
  versioned templates, stored submissions. Sellable add-on (eka.care charges
  ₹9,999/yr for exactly this).

### Under consideration: Payroll & staff leave
Verdict (2026-07-02): **Leave/attendance = yes, later** — simple to build
(leave requests, approvals, a calendar), high stickiness, receptionists love
it. **Full payroll = not yet** — Indian payroll means PF/ESI/TDS compliance,
a product in itself; getting it wrong damages trust in the clinical product.
Sequence: billing → leave/attendance (Phase 6/7) → payroll only via an
integration or once revenue justifies a dedicated effort.

## Decision log

| # | Decision | Why | Date |
|---|---|---|---|
| 1 | **Keep** SystemUser/TenantUser/TenantUserRole (vs simple User.ClinicId) | Already built, working, and isolation-tested. Removing it is a rewrite with zero user value; the vision (visiting dentists, multi-branch, orgs) needs it anyway. Architecture is judged by cost-to-change vs value, not by "simplest possible on paper". | 2026-07-02 |
| 2 | Tenant isolation via EF global query filters + write guards, not per-query `Where` | Central enforcement can't be forgotten; verified cross-tenant reads return 404/empty. | 2026-07-02 |
| 3 | Roles are a closed seeded set (Admin/Doctor/Receptionist) | `[Authorize]` matches exact strings; free-text roles silently break security. | 2026-07-02 |
| 4 | Modular monolith, no microservices, no repository pattern | Right size for the team (1 dev) and stage; EF Core's DbContext already is a repository/UoW. | 2026-07-02 |
| 5 | Errors as RFC 7807 Problem Details from one handler | Standard, machine-readable, no leaked internals; Angular can render `detail` directly. | 2026-07-02 |
| 6 | Dental-first (not generic clinic) | Real client is a dental clinic; niche focus is a selling advantage. Generic naming stays in code (Patient, Appointment) so other verticals remain possible. | 2026-07-02 |
| 7 | Roles stay a closed set; NO admin-created free-text roles | `[Authorize]` matches exact strings — user-invented roles would silently get no permissions (or be a security hole). The industry answer for "other roles" (Nurse, Pharmacist, Accountant) is PERMISSION-BASED RBAC: fine-grained permissions, roles = named permission bundles, custom roles as an enterprise feature. Planned Phase 6. | 2026-07-02 |
| 8 | Multi-clinic owner = multiple tenant memberships + clinic switcher (not an Organization entity yet) | SystemUser↔TenantUser already supports N clinics per person. Login scopes to one clinic; switch-clinic re-issues the token; "New clinic" self-serve provisioning. A true Organization layer (consolidated reporting, shared patients across branches) only makes sense with real multi-branch customers — Phase 7. | 2026-07-02 |
| 9 | Brand: Klinovo (provisional) | "Clinora" already exists in market. "Klinovo" had zero search collisions (verified 2026-07-02); formal trademark + domain check still required before spending on branding. | 2026-07-02 |
| 10 | Platform admin = config email allowlist (`Platform:AdminEmails`), NOT a role | Clinic roles are tenant-scoped; the SaaS owner is above tenants. An allowlist in config can't be granted by any in-app action (no privilege-escalation path), and the server re-checks it on every `/api/platform` call — the JWT flag is display-only. | 2026-07-14 |
| 11 | Payment collection: manual UPI/bank transfer + owner applies plan in `/platform` | Zero gateway fees and zero integration cost while customer count is small; Razorpay checkout automates the same `ChangePlan` path later, so nothing is throwaway. | 2026-07-14 |
| 12 | Suspension enforced at token issue (login/refresh/switch filter on `Tenant.IsActive`) | One choke point covers every entry path; existing access tokens age out within 60 min. Data is never deleted — a paying-again clinic is one click from restored. | 2026-07-14 |
| 13 | Inventory v1 = single quantity + adjust (±), no movement ledger yet | Clinics start by wanting "do we have it & when to reorder" — one honest number beats an audit trail nobody fills in. Stock can't go negative (domain rule); a movement ledger + dispense-from-prescription comes with patient billing, where it earns its keep. | 2026-07-14 |
| 14 | Prescription entry: guided free-text (inventory autocomplete + 1-0-1 / food-timing chips), not a rigid medicine master | Indian dose convention (morning-noon-night) as one-tap chips speeds doctors up without blocking unusual prescriptions; names suggest from the clinic's OWN inventory so spelling matches the shelf. A structured drug database is a later, paid dataset problem. | 2026-07-14 |
