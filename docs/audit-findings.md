# Klivia UI audit — findings backlog

Recovered from the 89-agent audit (run wf_8d8adaf3). This file is the working list for the polish fixes.

## Progress

**3 criticals — FIXED (53cf6b1):** wrong-patient record race · payment 500 · multi-clinic refresh.

**Batches 1–6 — FIXED (8e21f16, e5f8a4c, bbbaf72, 9502bb6, 3d7a5af + a11y):**
- Globals: en-IN locale (₹ grouping + "Rs"→"₹"), scroll restoration, SW new-version toast, viewport-fit=cover, og:image, Google-fonts caching, `.chip-danger`, `.sr-only`, mobile 40px tap targets, drawer overscroll containment.
- Silent failures surfaced with retry: appointments (+ status-change errors, cancel confirm, IST "today" bug), reports (blank→error), staff (errors out of the green banner + resend busy state), inventory (drawer closes on delete, error clears, min-0 price), forgot-password (no false "check inbox" on network error), patients (error state gated).
- Overlays: invoice last-page clamp, fill-drawer template re-filter (data correctness), Escape closes overlays.
- Mobile: invoice item-row collapses, More-sheet scrolls, topbar shrink guards, line-fill/note-row wrap.
- Auth/forms: password pattern validators, Enter-to-continue on register, `type=tel` phone, cross-tab session sync, logout clears cached tenant state + stops polling, iOS-safe PDF download.
- a11y: keyboard-reachable password toggle.

**Batch 7–8 — also FIXED (5982717, 4c33b0c):**
- reset-password / accept-invite: confirm-password feedback + expired-link "request new link" CTA + password pattern.
- switchClinic error handling (no more stuck spinner); forms.ts error-clear + iOS-safe preview.
- aria-pressed on appointments/invoices filter chips; notification unread sr-only marker.

**STILL DEFERRED — needs a real browser/phone to verify (do with the app open):**
- In-table menu + calendar-pop + custom-select + inventory-adjust popover clipping (position:fixed + getBoundingClientRect + flip-above logic — must be eyeballed).
- Android hardware-back closes overlays (history.pushState integration — needs an Android device).
- Full body scroll-lock while overlays open (partial: overscroll-behavior done).
- Lower-value broad ARIA: aria-live on every alert, label associations on custom controls, segmented/select roving-tabindex + arrow keys.
- legal page email left as-is: taveperz@ was explicitly authorized as public contact.

The per-file list below is the full original backlog for reference.

---


## frontend/src/app/features/appointments/appointments.ts (7)

- **[major] L127** Patient-search and medicine-suggestion typeaheads die permanently after a single HTTP error
  - Fix: Wrap the inner request so errors don't kill the outer stream: `switchMap((term) => this.patientsApi.getAll(term, 1, 8).pipe(catchError(() => of({ items: [] } as PagedResult<PatientDto>))))`. Apply the same to medicineQuery$ (line 136) and to the identical pipeline in frontend/src/app/features/invoices/invoices.ts line 80.
- **[major] L155** List load race: rapid day-arrow / status-filter taps let a stale slow response overwrite the newer day's list
  - Fix: Drive the list through a single Subject + switchMap (`reload$.pipe(switchMap(() => this.api.getAll({...})))`) so each new load cancels the previous request, or capture a monotonically increasing requestId and ignore responses whose id is not the latest.
- **[major] L260** Booking with no doctor selected does nothing — no message, no red field, button appears dead
  - Fix: app-select (select.ts) renders no invalid/touched state, so markAllAsTouched is invisible. Either set this.formError.set('Select a doctor.') on this branch (matching the existing 'Select a patient first.' pattern), or add an `invalid` input to app-select bound to `bookForm.controls.doctorTenantUserId.invalid && touched` with a .invalid class and a field-error span in appointments.html (lines 180–185).
- **[major] L409** 'Today' is computed from the UTC clock — between 12:00 AM and 5:30 AM IST the app opens on yesterday and books appointments for the wrong day
  - Fix: Use a local-date formatter instead of toISOString(). calendar-pop.ts:320 already has the correct implementation (toIso() using getFullYear/getMonth/getDate) — export it to a shared date-utils and reuse in: appointments.ts:409 (todayIso), appointments.ts:186 (shiftDate's toISOString round-trip), dashboard.ts:145 (const todayIso = this.today.toISOString().split('T')[0]), and platform.ts:119 (payDate default new Date().toISOString().slice(0, 10)).
- **[major] L215** Check-in / Start / Cancel status changes have no error handler — on network or server failure the tap silently does nothing
  - Fix: Add an error callback that surfaces the failure, e.g. error: (err) => this.formError… — better, add a page-level error signal like Invoices has and render it above the table: error: (err) => this.error.set(parseApiError(err).message). Apply the same fix to dashboard.ts:180-182 (checkIn) which has the identical bare { next: ... } subscribe.
- **[major] L215** Check in / Start / Cancel status updates fail silently — no error handler
  - Fix: Add an error callback that shows the parsed message in a page-level banner like the other pages: `error: (err) => this.error.set(parseApiError(err).message)` (add an `error` signal + the standard dismissable .alert-error block to appointments.html), and do the same for dashboard.checkIn and the two silent PDF downloads.
- **[minor] L215** Status changes (Check in / Complete / Cancel) fail silently — no error callback
  - Fix: Add an error callback that surfaces the failure, e.g. set a dismissible error signal: `error: (err) => this.error.set(parseApiError(err).message)`. Apply the same to dashboard.ts checkIn (frontend/src/app/features/dashboard/dashboard.ts line 180-182), which has the identical next-only subscribe.

## frontend/src/app/layout/shell.scss (5)

- **[major] L290** Topbar trial chip and icon buttons have no shrink protection — chip wraps to a 2-line blob and the bell circle squashes at 320-400px
  - Fix: In shell.scss add `.trial-chip { white-space: nowrap; flex: none; }` and `.topbar-icon, .profile { flex: none; }` so only .topbar-clinic (which already has min-width:0 + ellipsis) absorbs the shrink. Optionally shorten the chip text to 'Trial · 14d' under 400px.
- **[major] L81** Sticky sidebar's stacking context traps the clinic-switcher backdrop and panel
  - Fix: Move the switcher backdrop + panel markup out of the sticky sidebar (render at shell template root with position:fixed, panel placed via the trigger's getBoundingClientRect), or give .sidebar an explicit z-index above the topbar and audit the profile-menu interplay (see the related profile finding).
- **[major] L537** Mobile "More" sheet has no max-height or scrolling — top items unreachable on 640px-tall phones
  - Fix: Add max-height: calc(100dvh - 24px); overflow-y: auto; overscroll-behavior: contain; to .more-sheet.
- **[minor] L536** Mobile 'More' sheet has no max-height or internal scroll — top items become unreachable on short phones
  - Fix: Add `max-height: calc(100dvh - 40px); overflow-y: auto;` to .more-sheet (keep the handle sticky if desired).
- **[minor] L355** Profile menu backdrop trapped under the sticky topbar's stacking context — tab bar stays live
  - Fix: Render the profile backdrop + panel outside the sticky topbar (at shell root with position:fixed, panel anchored via getBoundingClientRect), the same hoist recommended for the clinic switcher — solving both sticky-ancestor traps consistently.

## frontend/src/app/features/appointments/appointments.html (4)

- **[major] L189** Labels are not programmatically associated with the custom form controls — including a for="b-date" pointing at an id that doesn't exist
  - Fix: Add an inputId/label input to DateField (apply it to the inner <input id=...>), an aria-label/aria-labelledby input to Select (on the trigger button) and to Segmented (on the radiogroup div), then wire the templates: appointments.html 180 (Doctor select), 189 (Date field), patients.html 131 (Gender segmented), 136 (DOB), inventory.html 161/195, invoices.html 159/185. Also give the naked inputs relying on placeholders (vitals at appointments.html 254-259, invoice item rows at invoices.html 188-193) real aria-labels.
- **[major] L41** Filter chips and toggles convey selected state only via an .active CSS class — no aria-pressed anywhere
  - Fix: Add [attr.aria-pressed]="status() === filter" (and the equivalent condition) to every filter-chip/toggle button: appointments.html 41 (status filters) and 47 ("My patients only"), invoices.html 46 (status) and 235 (payment method), forms.html 162-167 (template targeting), patients.html 352-355 (fill template) — grep confirms aria-pressed exists nowhere except the password toggle.
- **[major] L100** Appointment Cancel is a single unconfirmed tap with no busy state and silently swallowed errors
  - Fix: Add a confirm step like invoices already have (`if (!confirm(...)) return;` — invoices.ts cancelInvoice), disable the row's action buttons via a busy signal while the request is in flight, and add an error callback that surfaces parseApiError(err).message.
- **[major] L100** Cancelling an appointment has no confirmation, unlike every other destructive action
  - Fix: Match the app's existing pattern: wrap the two Cancel calls (lines 100 and 108) with a guard, e.g. (click)="confirmCancel(a)" where confirmCancel does `if (!confirm(`Cancel ${a.patientName}'s appointment?`)) return; this.updateStatus(a, 'Cancelled');`.

## frontend/src/app/layout/shell.html (4)

- **[major] L312** Escape key dismisses nothing — every drawer, sheet, modal, menu and the calendar ignore it
  - Fix: Add a document:keydown.escape HostListener in Shell that closes the topmost open signal (moreOpen, installHelpOpen, newClinicOpen, notifOpen, switcherOpen, profileOpen), and a small shared closeOnEscape directive (or emit closed on Escape in CalendarPop/OnboardingTour) applied to each feature drawer.
- **[minor] L135** Topbar profile menu and clinic switcher lack aria-expanded, have icon/initials-only accessible names, and role="menu" contains no menuitems
  - Fix: On the profile trigger add [attr.aria-expanded]="profileOpen()" and aria-label="Account menu" (the current accessible name is just the user's initials, e.g. "AK"). On the clinic switcher button (line 6) add aria-haspopup and [attr.aria-expanded]="switcherOpen()". Either add role="menuitem" + arrow-key navigation to the .profile-item links/buttons inside the role="menu" panel (line 141), or drop role="menu" so screen readers don't promise menu semantics that aren't implemented. Same for the invoices "Mark paid ▾" popup trigger (invoices.html:110).
- **[minor] L388** Notification unread state and type are communicated by color alone (background tint + colored dot) with no text equivalent
  - Fix: Add a visually-hidden text marker for unread rows (e.g. <span class="sr-only">Unread — </span> inside .notif-title when !notification.isRead) and either an aria-label/sr-only text for the type dot or aria-hidden="true" if the type is decorative. Add a .sr-only utility to styles.scss if one doesn't exist.
- **[minor] L337** Required-asterisk labels drift from the app's "Optional" chip convention — including two copies of the same drawer disagreeing
  - Fix: Drop the " *" from shell.html:337 and from the 'Opening stock *' ternary in inventory.html:178 to match the convention.

## frontend/src/app/features/invoices/invoices.ts (4)

- **[major] L176** Receipt PDF opened via window.open inside an async callback — blocked by iOS Safari's popup blocker
  - Fix: Use the same anchor-element pattern already used in reports.ts downloadPdf (create <a href=url download=...> and click() it), or open the window synchronously in the click handler (`const w = window.open('', '_blank')`) and set `w.location = url` when the blob arrives. Fix the identical call in frontend/src/app/features/forms/forms.ts line 103 (preview).
- **[major] L176** Receipt/PDF opened via window.open() inside an async subscribe callback — blocked by Safari's popup blocker on iPhone
  - Fix: Use the anchor-click download pattern that patients.ts printIntakeForm() already uses (create <a>, set href/download, link.click()), or open the window synchronously in the click handler and set its location when the blob arrives. Same fix needed in forms.ts preview() line 103. Also add an error callback to downloadPdf — currently a failed PDF request shows nothing.
- **[major] L146** Marking the last invoice on the last page paid (or cancelling it) strands the user on an empty page with a misleading 'No invoices with this status' screen
  - Fix: After a row-removing mutation, clamp the page before reloading. In load()'s next handler add: if (result.items.length === 0 && this.page() > 1) { this.page.set(Math.max(1, result.totalPages)); this.load(); return; }. Same guard applies to cancelInvoice() (line 163). The backend (Clinic.Infrastructure/Services/InvoiceService.cs:49) does a raw Skip/Take with no page clamping, so page 2 of a now-1-page filtered set returns zero items.
- **[minor] L136** In-table menus (pay/adjust/plan) have no click-away, backdrop, or Escape close
  - Fix: Reuse the pattern from select.ts:217 — a @HostListener('document:click') that clears payMenuFor/adjustFor/planMenuFor when the click lands outside the menu — or render a fixed click-away backdrop behind each menu, plus Escape.

## frontend/src/app/features/staff/staff.ts (4)

- **[major] L141** Resend-invite failure message is displayed in the green success banner
  - Fix: Give the page a separate `error` signal rendered with the standard dismissable .alert-error block (as on inventory/invoices), and change this callback to `this.error.set(parseApiError(err).message)`.
- **[minor] L141** resendInvite() renders API errors inside the green success banner
  - Fix: Route the failure to an error-styled signal instead: add `readonly error = signal('')` rendered with .alert-error (as other pages do) and use `error: (err) => this.error.set(parseApiError(err).message)`.
- **[minor] L141** "Resend invite" shows errors inside the green success banner and has no busy state against double-clicks
  - Fix: Route errors to a dedicated error signal rendered with alert-error styling, and add a per-member busy signal that disables the Resend button while the request is in flight (each click currently fires another email).
- **[minor] L141** resendInvite failure message is pushed into the success notice — errors render in the green 'success' banner
  - Fix: Add a page-level error signal and render it with alert-error in staff.html (the page currently only has the notice/alert-success block at staff.html:12-17). Change the handler to error: (err) => this.error.set(parseApiError(err).message).

## frontend/src/app/features/inventory/inventory.ts (4)

- **[major] L151** Removing an inventory item leaves the edit drawer open showing the deleted item
  - Fix: In the delete success callback add `this.drawerOpen.set(false); this.editing.set(null);` before setting the notice, matching the save() flow on line 138-142.
- **[minor] L153** Removing an item from the edit drawer leaves the drawer open on the deleted item
  - Fix: In the delete success handler add this.drawerOpen.set(false) (and clear editing). The Remove button lives in the drawer footer (inventory.html line 203), so the drawer must close on success.
- **[minor] L55** Inventory unit price accepts typed negative values — stored and displayed as a negative rupee amount
  - Fix: Add Validators.min(0) to unitPriceRupees and render errorFor('unitPriceRupees') under the price input (inventory.html line 172 — its min="0" attribute only constrains the spinner arrows, not typed input). Backend InventoryService has no negative-price guard either, so the client is the only gate.
- **[minor] L154** Removing an item from the edit drawer leaves the drawer open on the deleted item — a follow-up 'Save changes' hits a deleted record
  - Fix: In the delete success handler add this.drawerOpen.set(false); this.editing.set(null); so the drawer closes when the item is gone. The Remove button lives inside the drawer footer (inventory.html:203: (click)="remove(editing()!)").

## frontend/src/app/shared/ui/onboarding-tour.ts (3)

- **[major] L80** Onboarding tour declares aria-modal="true" but never moves focus into the dialog and has no Escape
  - Fix: In OnboardingTour, after the layer renders (constructor effect or afterNextRender), focus the .tour-card (give it tabindex="-1"), add @HostListener('document:keydown.escape') { this.closed.emit(); }, and trap Tab within the card while the tour is open; restore focus on close.
- **[minor] L241** Tour spotlight coordinates go stale: page scrolls behind the fixed layer, hole stays put
  - Fix: Re-run locate(this.steps()[this.index()]) from window scroll (capture, passive) and resize listeners while the tour is open — or simpler, lock body scroll for the tour's duration (pairs with the global scroll-lock fix).
- **[minor] L241** Tour spotlight coordinates are captured once per step and never updated on scroll or resize — the highlight ring drifts off its target
  - Fix: Re-run locate() from `window.addEventListener('resize', ...)` / `scroll` (or a ResizeObserver on document.body) while the tour is open, and remove the listeners on destroy. Cheap alternative: lock body scroll while the tour is open and re-locate on resize/orientationchange only.

## frontend/src/app/core/auth/auth.service.ts (3)

- **[major] L52** switchClinic() has no error handler — clinic switching fails silently and the Clinics page spinner sticks forever
  - Fix: Add an error callback that surfaces the failure and lets callers reset their busy state — e.g. return the Observable (like createClinic does) and let Shell/Clinics subscribe with next+error, resetting switchingTo.set(null) and showing parseApiError(err).message.
- **[major] L87** logout() never clears cached tenant signals — next user on the same device sees the previous clinic's trial banner, settings and unread count
  - Fix: In logout(), reset all tenant-scoped cached state: BillingService.summary.set(null), SettingsService.settings.set(null), NotificationsService.unreadCount.set(0) (inject them or expose a reset hook each service registers). switchClinic avoids this only because it does location.assign('/'); logout/login uses router navigation and keeps the injector alive.
- **[major] L26** Session is never synced across tabs — with two tabs open, refresh-token rotation force-logs-out the second tab
  - Fix: Listen for cross-tab changes in the AuthService constructor: `window.addEventListener('storage', (e) => { if (e.key === STORAGE_KEY) this._session.set(readStoredSession()); })`. Tab B then picks up the rotated tokens tab A stored (and also drops its session when tab A logs out).

## frontend/src/app/features/patients/patients.ts (3)

- **[major] L280** Patient list race: an older slower search response overwrites the newer results
  - Fix: Since search input is already a stream, extend it: merge search/page changes into one Subject and pipe through switchMap to this.api.getAll(...) so newer loads cancel older ones. The same unguarded-load pattern also exists in frontend/src/app/features/invoices/invoices.ts line 99 and frontend/src/app/features/inventory/inventory.ts line 74 (debounced search calls load() with no cancellation).
- **[major] L285** API failure on Patients/Appointments list renders a misleading "No patients yet" empty state
  - Fix: Copy the invoices pattern: add an `error` signal, set it in the load() error callback (`this.error.set(parseApiError(err).message)`), render the standard dismissable .alert-error, and gate the empty state so it only shows when the load actually succeeded (e.g. `@else if (result() && result()!.items.length === 0)`). Apply to both patients.ts and appointments.ts.
- **[minor] L134** Switching the template inside the 'Fill intake form' drawer does not re-filter custom sections — answers are captured against the wrong template's sections
  - Fix: Store the full unfiltered sections in a signal, make fillTemplate a signal, and derive the visible list with computed(() => all().filter(s => s.template === 'both' || s.template === this.fillTemplate())). The template chips in patients.html:352-355 only assign this.fillTemplate = 'dental'|'general' and never re-run the filter.

## frontend/src/app/app.config.ts (3)

- **[major] L18** No en-IN LOCALE_ID registered — every ₹ amount piped through DecimalPipe uses US digit grouping, inconsistent with the en-IN toLocaleString used in toasts
  - Fix: In app.config.ts: import localeEnIN from '@angular/common/locales/en-IN'; registerLocaleData(localeEnIN); and add { provide: LOCALE_ID, useValue: 'en-IN' } to providers. All existing `| number` and `| date` pipes then pick up Indian grouping automatically; remove the ad-hoc .toLocaleString('en-IN') in invoices.ts:262 for consistency.
- **[major] L31** Service worker registered but SwUpdate is never used — installed users run stale versions for days with no reload prompt
  - Fix: Add an update service instantiated at bootstrap: inject SwUpdate; subscribe to `updates.versionUpdates.pipe(filter(e => e.type === 'VERSION_READY'))` and show a toast/banner ('New version available — Tap to refresh') that calls `document.location.reload()`; also subscribe to `updates.unrecoverable` and force `document.location.reload()`. Optionally poll `updates.checkForUpdate()` every ~6h via setInterval for always-open reception desktops.
- **[major] L20** No scroll restoration configured — navigating from a scrolled page opens the next page mid-scroll
  - Fix: Add `withInMemoryScrolling({ scrollPositionRestoration: 'enabled', anchorScrolling: 'enabled' })` to the provideRouter call (anchorScrolling also helps the reported broken landing-page anchors).

## frontend/src/app/shared/ui/date-field.ts (2)

- **[major] L31** Calendar opener button is removed from the tab order (tabindex="-1") and the calendar popup has no Escape or keyboard close
  - Fix: Remove tabindex="-1" from the .cal-btn so keyboard users can reach it. In CalendarPop, add a host '(keydown.escape)': 'closed.emit()' (it already has role="dialog" at calendar-pop.ts:26 but zero keyboard handlers), move focus into the panel when it opens and back to the opener on close. Also give day buttons a full-date aria-label (e.g. [attr.aria-label]="cell.iso") so 'Choose a date' isn't a grid of bare numbers.
- **[minor] L171** Typed dates use a UTC future-check while the calendar uses local time — typing today's date as DOB is rejected between 00:00 and 05:30 IST
  - Fix: Compare local calendar dates, matching CalendarPop's approach: build the local ISO string for today (like calendar-pop.ts toIso()) and reject only when `iso > todayIso` — the `date` here was constructed with Date.UTC (line 156), so comparing it to Date.now() shifts the boundary by the timezone offset.

## frontend/src/app/layout/shell.ts (2)

- **[major] L131** Notification polling never stops when the session expires — 60s interval keeps firing on the login page forever
  - Fix: Move the cleanup to the single logout path: call an injected NotificationsService.stopPolling() inside AuthService.logout() (frontend/src/app/core/auth/auth.service.ts line 87), or give Shell an ngOnDestroy that calls stopPolling(). Also have startPolling() always call refreshUnreadCount() so a re-login shows the correct badge immediately.
- **[major] L65** Android hardware back button with any drawer/sheet open navigates the route (or exits the app) instead of closing the overlay — half-filled forms are destroyed
  - Fix: Integrate overlays with history: when a drawer/sheet/modal opens, `history.pushState({drawer: true}, '')`; close it from a `popstate` listener (and `history.back()` when it is closed via its own X button). A small shared service or directive applied to the 15+ role="dialog" surfaces covers the whole app in one place.

## frontend/src/app/features/invoices/invoices.scss (2)

- **[major] L44** "Mark paid" dropdown is clipped by the table's scroll container
  - Fix: Position these menus with position:fixed coordinates taken from the trigger's getBoundingClientRect() (flip above when near the viewport bottom), or render them in a body-level overlay container. z-index cannot escape overflow clipping — only removing the menu from the scroll container's clip does.
- **[minor] L43** 'Mark paid' payment-method menu is clipped by the table's scroll wrapper on the last rows
  - Fix: Same remedy as the inventory popover: render the method choices in a fixed-position menu anchored via getBoundingClientRect(), or reuse the bottom-sheet/modal pattern on small screens instead of an in-table dropdown.

## frontend/src/styles.scss (2)

- **[major] L411** Page behind drawers/sheets/modals stays scrollable — no scroll lock anywhere
  - Fix: Add a tiny shared effect/service: while any overlay signal is open, set document.body.style.overflow = 'hidden' (restore on close). Additionally set overscroll-behavior: contain on .drawer-body, .more-sheet and .modal-card, and touch-action: none on the backdrops.
- **[minor] L379** Filter chips — the primary mobile filter controls — are only ~31px tall, under the 40px touch-target minimum
  - Fix: Add `@media (max-width: 640px) { .filter-chip { padding: 9px 16px; } }` in styles.scss (and bump forms' .section-actions .btn padding similarly) to reach ≥40px tap height on touch devices without changing the desktop look.

## frontend/src/app/features/patients/patients.html (2)

- **[major] L352** "Fill intake form" drawer: switching the Dental/General template chip does not reload the custom sections — wrong sections are shown and saved
  - Fix: Sections are fetched once in openFill() and filtered by the template that was active at open time (patients.ts lines 133–137). Store the unfiltered section list, make fillTemplate a signal, and derive the visible sections with a computed: `sections.filter(s => s.template === 'both' || s.template === fillTemplate())`, so chip clicks re-filter instantly.
- **[major] L125** Patient phone input is a plain text field — no tel keyboard, no maxlength on the most-typed field in the app
  - Fix: Add type="tel" autocomplete="tel" maxlength="20" (backend RegisterPatientRequestValidator caps Phone at 20 chars and pattern ^\+?[0-9 ()\-]{7,20}$ — also worth mirroring as a Validators.pattern so typos are caught before submit). Apply the same to the clinic phone in settings.html line 38.

## frontend/src/app/features/legal/legal.ts (2)

- **[major] L24** Legal page publishes a personal Gmail address, breaking the deliberate WhatsApp-only contact policy
  - Fix: Remove `contactEmail` and the mailto sentence in legal.html:132-136, leaving the WhatsApp link as the sole contact — consistent with every other page.
- **[minor] L24** Privacy policy publishes a personal Gmail as the official contact, contradicting the deliberate WhatsApp-only contact design
  - Fix: Remove the contactEmail field and the mailto sentence from legal.html's Contact section, leaving the WhatsApp link as the single contact channel (matching the landing footer), or replace it with a monitored product-domain address if email contact is genuinely intended.

## frontend/src/index.html (2)

- **[major] L7** Viewport meta lacks viewport-fit=cover, so every env(safe-area-inset-bottom) in the layout resolves to 0 on iPhone
  - Fix: Change line 7 to: <meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">. The existing safe-area padding already handles the resulting edge-to-edge layout, so no CSS changes are needed.
- **[minor] L12** No og:image (or og:url) — WhatsApp link shares show a text-only preview
  - Fix: Add <meta property="og:image" content="https://klivia.vercel.app/icon-512.png"> (absolute URL — WhatsApp ignores relative paths) plus <meta property="og:url" content="https://klivia.vercel.app/">; ideally a 1200x630 branded card image for a large preview.

## frontend/src/app/core/auth/auth.interceptor.ts (2)

- **[major] L27** Concurrent 401s each fire their own refresh against a single-use refresh token — the losers call logout() and randomly kick the user out
  - Fix: Deduplicate the refresh: keep one in-flight `refresh$` observable in AuthService (e.g. `refresh$ ??= this.http.post(...).pipe(tap(storeSession), shareReplay(1), finalize(() => this.refresh$ = null))`) so every 401 waits on the SAME refresh and retries with the new token; only log out if that single shared refresh fails.
- **[minor] L30** Session expiry is completely silent — user is dumped on a bare login form with no explanation and no way back to where they were
  - Fix: Have logout() accept a reason and forward context: `this.router.navigate(['/login'], { queryParams: { expired: 1, returnUrl: this.router.url } })`; login.ts shows a "Your session expired — please sign in again" notice when `expired=1` and navigates to `returnUrl` instead of '/' after sign-in.

## frontend/src/app/shared/ui/select.ts (1)

- **[major] L202** Custom select's arrow-key navigation is visual-only: no aria-activedescendant and no DOM focus movement, so screen readers announce nothing while navigating options
  - Fix: Give each option an id (e.g. [id]="'opt-' + option.value") and bind [attr.aria-activedescendant] on the trigger to the focused option's id (plus role="combobox" on the trigger); alternatively move real DOM focus onto the option buttons as arrows are pressed. Also handle keydown (Escape/arrows/Enter) on the panel's option buttons, not only on the trigger, so keys still work after the user Tabs into an option.

## frontend/src/app/features/invoices/invoices.html (1)

- **[major] L15** Success/error banners and drawer form errors have no aria-live/role="alert" — screen readers never hear save results or failures
  - Fix: Add role="status" (or aria-live="polite") to .alert-success banners and role="alert" to .alert-error banners app-wide — invoices.html (15, 21, 155), inventory.html (15, 21, 150), forms.html (15, 21, 126), patients.html (105), appointments.html (149, 232), shell.html (322) — ideally via one shared alert component. Also add role="alert" to the inline .field-error spans or aria-describedby wiring so validation failures are announced.

## frontend/src/app/shared/ui/password-input.ts (1)

- **[minor] L28** Show/hide password toggle is keyboard-unreachable (tabindex="-1")
  - Fix: Remove tabindex="-1" from the .pw-toggle button — it already has a correct aria-label and aria-pressed, so it just needs to be reachable. It sits after the input in the DOM, adding exactly one tab stop.

## frontend/src/app/shared/ui/segmented.ts (1)

- **[minor] L11** Segmented control declares role="radiogroup"/role="radio" but implements no arrow-key navigation or roving tabindex
  - Fix: Implement the ARIA radio group pattern: keep only the checked (or first) segment at tabindex="0", set tabindex="-1" on the rest, and handle ArrowLeft/ArrowRight on the group to move selection and focus. Alternatively, drop the radio roles and use plain buttons with [attr.aria-pressed] — cheaper and honest about the current Tab-based behavior.

## frontend/src/app/features/forms/forms.ts (1)

- **[minor] L55** Error banner is never cleared and has no dismiss button — one failed action leaves a permanent stale error
  - Fix: Clear the signal at the start of each operation (`this.error.set('')` in load/setDefault/move/remove, as platform.ts mutate() does) and/or add the dismiss ✕ button that notice() already has in forms.html. The same never-cleared error signal exists in frontend/src/app/features/inventory/inventory.ts line 34 (set at lines 86/158/180, cleared nowhere, no dismiss in inventory.html line 20).

## frontend/src/app/features/inventory/inventory.scss (1)

- **[major] L13** ± Stock adjust popover is absolutely positioned inside the scrolling .table-wrap, so it renders clipped below the table
  - Fix: Don't render the popover inside the scroll container: either convert the adjust UI to the existing drawer/modal pattern on all sizes, or set `.adjust-pop { position: fixed; }` with coordinates from the trigger button's getBoundingClientRect(), or give `.table-wrap` a `padding-bottom`/`overflow: visible` escape via a portal. A modal-card at ≤640px is the simplest robust fix.

## frontend/src/app/features/platform/platform.scss (1)

- **[minor] L44** Platform payment-note row never wraps — non-shrinkable button crushes the note and overflows 320px screens
  - Fix: Add `flex-wrap: wrap;` to .payment-note-row (the button then drops to its own line), or stack the row with a `@media (max-width: 560px) { .payment-note-row { flex-direction: column; } }`.

## frontend/src/app/features/patients/patients.scss (1)

- **[minor] L282** 'Lines' custom-section rows in the Fill-digitally drawer use a fixed 130px label column, overflowing 320-360px phones
  - Fix: Either stack on small screens (`@media (max-width: 560px) { .line-fill { grid-template-columns: 1fr; gap: 4px; } }`) or use `grid-template-columns: minmax(90px, 130px) minmax(0, 1fr)` and add `min-width: 0` to the input.

## frontend/src/app/features/forms/forms.html (1)

- **[minor] L161** Inline flex chip rows in drawers omit flex-wrap — three template chips squash into multi-line pills / overflow at 320px
  - Fix: Add `flex-wrap:wrap;` to the inline styles at forms.html:161, staff.html:130 and platform.html:241 (matching the `display:flex; gap:8px; flex-wrap:wrap;` already used at platform.html:216 and invoices.html:233).

## frontend/src/app/features/settings/settings.html (1)

- **[minor] L33** Clearing the clinic name makes "Save settings" silently do nothing — required error is never rendered
  - Fix: Add [class.invalid] and a field-error span for the name control (the form only markAllAsTouched()s and returns in settings.ts save() lines 91–94 with no visible invalid styling anywhere on this page). Same pattern gap: the required 'unit' field in inventory.html line 168 has no error binding either.

## frontend/src/app/features/auth/register.html (1)

- **[minor] L96** Pressing Enter on registration step 1 does nothing — no submit button exists in the form at that point
  - Fix: Make the step-1 button type="submit" and branch the form's (ngSubmit) handler: when step() === 1 call continueToClinic(), else the existing submit(). This restores the expected Enter-to-continue behaviour.

## frontend/src/app/shared/ui/calendar-pop.ts (1)

- **[major] L89** Calendar popover is clipped inside every drawer's scrolling body
  - Fix: In CalendarPop (and Select), position the panel with position:fixed coordinates measured from the anchor button, flipping above the field when there is no room below — the existing fixed .cal-backdrop already escapes the scroller, only the panel is trapped. Alternatively hoist the panel to a body-level overlay outlet.

## frontend/src/app/features/platform/platform.html (1)

- **[minor] L374** Payment history tracks rows by payment.paidAt, which is a date-only value — two payments recorded for the same date collide (NG0955 duplicate track keys)
  - Fix: Track by index or a composite: @for (payment of payments(); track $index). paidAt cannot be unique: the record-payment drawer sends payDate as a bare 'yyyy-MM-dd' (platform.ts:119/134) and the backend stores it as-is (PlatformService.cs:214 'var paidAt = request.PaidAt ?? now'), so every payment recorded via the drawer has a midnight timestamp — a second payment for the same clinic on the same date has an identical key.

## frontend/src/app/features/reports/reports.ts (1)

- **[major] L30** Reports page goes completely blank when the overview request fails
  - Fix: Add an `error` signal set from `parseApiError(err).message` in the error callback, and add an `@else` branch in reports.html rendering the standard .alert-error with the billing-style "Refresh the page to retry." suffix.

## frontend/src/app/features/platform/platform.ts (1)

- **[major] L187** Suspending an entire clinic happens on a single tap with no confirmation
  - Fix: Guard the suspend direction with the app's existing confirm pattern: `if (tenant.isActive && !confirm(`Suspend ${tenant.name}? All their staff will be unable to sign in.`)) return;` at the top of toggleActive().

## frontend/src/app/core/models/plan-pricing.ts (1)

- **[minor] L74** Currency formatting drift: "Rs" vs "₹", and Indian vs Western digit grouping on the same screen
  - Fix: Change formatInr to use the ₹ symbol, and register the Indian locale app-wide (registerLocaleData(localeEnIN) + `{ provide: LOCALE_ID, useValue: 'en-IN' }` in app.config.ts) so DecimalPipe grouping matches the toLocaleString('en-IN') strings.

## frontend/src/app/features/billing/billing.html (1)

- **[minor] L8** Billing notices lack the dismiss button every other page has; busy label uses "..." instead of "…"
  - Fix: Reuse the shared dismissable alert markup for success()/error() on billing (and the plain error alerts on forms.html:21, settings.html:15, clinics drawer), and change line 84 to show the inline-spinner with 'Switching…'.

## frontend/src/app/features/inventory/inventory.html (1)

- **[minor] L92** "Low stock" alert reuses the grey Cancelled chip although the page itself promises a red flag
  - Fix: Add a danger chip style (e.g. .chip-danger using --color-danger with a red-tinted background) in styles.scss and use it for Low stock here and Payment overdue in platform.html:139-143, keeping chip-cancelled for genuinely inactive states.

## frontend/ngsw-config.json (1)

- **[major] L4** Google Fonts (CSS from fonts.googleapis.com + woff2 from fonts.gstatic.com) are not covered by any asset group
  - Fix: Add a third asset group caching the font origins: { "name": "fonts", "installMode": "lazy", "updateMode": "prefetch", "resources": { "urls": ["https://fonts.googleapis.com/**", "https://fonts.gstatic.com/**"] } } — or better for budget Android on slow networks, self-host the two font families in /public/fonts so the existing extension-based assets group prefetches them.

## frontend/src/app/features/landing/landing.html (1)

- **[major] L9** Landing nav links and 'See pricing' CTA reload the app to the top instead of scrolling — anchors broken by <base href="/"> on /welcome
  - Fix: Replace the raw anchors with router fragments: <a [routerLink]="['/welcome']" fragment="pricing">, and enable withInMemoryScrolling({ anchorScrolling: 'enabled', scrollPositionRestoration: 'enabled' }) in app.config.ts provideRouter(...). (Or simply use href="/welcome#pricing".) The existing scroll-margin-top in landing.scss already handles the sticky-nav offset.

## frontend/src/app/features/auth/register.ts (1)

- **[major] L61** Password validators mismatch backend rules: placeholders promise 'letters + digits' but only length is checked
  - Fix: Add Validators.pattern(/^(?=.*[A-Za-z])(?=.*[0-9]).*$/) to the password controls in register.ts, reset-password.ts and accept-invite.ts, with the inline message 'At least 8 characters, with a letter and a digit.' Also surface parsed.fieldErrors['newPassword'] / ['password'] in the reset and invite error handlers instead of the generic title.

## frontend/src/app/features/auth/reset-password.html (1)

- **[major] L43** Confirm-password field gives zero feedback when empty — the Save button appears dead
  - Fix: In both reset-password.html and accept-invite.html, bind [invalid]="form.controls.confirm.invalid && form.controls.confirm.touched" on the confirm app-password-input and add a field-error span ('Please repeat the password.') mirroring the pattern used on the password field.

## frontend/src/app/features/auth/reset-password.ts (1)

- **[major] L48** Expired or already-used reset link is a dead end — no path to request a new link
  - Fix: Track the failure: on error with status 401 set a tokenRejected signal, and in the template render the same 'Request new link' CTA (routerLink="/forgot-password") shown for the missing-token case, instead of (or below) the dead form.

## frontend/src/app/features/auth/forgot-password.ts (1)

- **[major] L36** Forgot-password shows 'Check your inbox' even when the request never reached the server
  - Fix: In the error callback, inspect parseApiError(err).status: for status 0 or >= 500 show an error signal ('Couldn't reach the server — check your connection and try again.') and keep the form; only set sent(true) for 2xx (next) responses.

## frontend/src/app/app.routes.ts (1)

- **[minor] L16** /privacy opens the combined legal page at the 'Terms of Service' heading — privacy content is a full page-length scroll away
  - Fix: In Legal's constructor, read the current route path (inject(Router).url or ActivatedRoute snapshot) and if it is /privacy call document.getElementById('privacy')?.scrollIntoView() in afterNextRender; or enable withInMemoryScrolling({ anchorScrolling: 'enabled' }) and change the links to [routerLink]="'/terms'" fragment="privacy".
