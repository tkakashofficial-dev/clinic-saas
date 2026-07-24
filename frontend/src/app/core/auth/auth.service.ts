import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, finalize, shareReplay, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AuthResponse,
  LoginRequest,
  Membership,
  RegisterRequest,
  Role,
} from '../models/api.models';

const STORAGE_KEY = 'clinic.auth';

/**
 * Holds the current session as a signal so any component/guard reacts to
 * login/logout instantly. Tokens live in localStorage — a pragmatic choice
 * for the MVP; consider httpOnly cookies + BFF pattern before enterprise sales.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly _session = signal<AuthResponse | null>(readStoredSession());

  constructor() {
    // Keep tabs in sync: when one tab rotates its tokens (or logs out), the
    // others pick up the change instead of holding a now-dead refresh token
    // and force-logging the user out on their next request.
    window.addEventListener('storage', (event) => {
      if (event.key === STORAGE_KEY) this._session.set(readStoredSession());
    });
  }

  readonly session = this._session.asReadonly();
  readonly isLoggedIn = computed(() => this._session() !== null);
  readonly role = computed<Role | null>(() => this._session()?.role ?? null);
  /** All roles; falls back to [role] for sessions stored before multi-role. */
  readonly roles = computed<Role[]>(() => {
    const session = this._session();
    if (!session) return [];
    return session.roles?.length ? session.roles : [session.role];
  });
  readonly fullName = computed(() => this._session()?.fullName ?? '');
  readonly clinicName = computed(() => this._session()?.clinicName ?? '');
  readonly memberships = computed<Membership[]>(() => this._session()?.memberships ?? []);
  readonly currentTenantId = computed(() => this._session()?.tenantId ?? '');
  /** SaaS owner — the API re-checks its allowlist on every platform call. */
  readonly isPlatformAdmin = computed(() => this._session()?.isPlatformAdmin === true);

  hasRole(...allowed: Role[]): boolean {
    return this.roles().some((role) => allowed.includes(role));
  }

  /** Re-scopes the session to another clinic, then fully reloads the app. */
  switchClinic(tenantId: string): void {
    this.http
      .post<AuthResponse>(`${environment.apiUrl}/auth/switch-clinic`, { tenantId })
      .subscribe({
        next: (response) => {
          this.storeSession(response);
          location.assign('/'); // full reload: every screen refetches for the new clinic
        },
      });
  }

  /** Opens an additional clinic (caller becomes its Admin) and lands in it. */
  createClinic(name: string, ownerIsDoctor: boolean): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${environment.apiUrl}/auth/clinics`, { name, ownerIsDoctor })
      .pipe(tap((response) => this.storeSession(response)));
  }

  login(request: LoginRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${environment.apiUrl}/auth/login`, request)
      .pipe(tap((response) => this.storeSession(response)));
  }

  register(request: RegisterRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${environment.apiUrl}/auth/register`, request)
      .pipe(tap((response) => this.storeSession(response)));
  }

  /** In-flight refresh, shared by every 401 that hits at the same moment. */
  private refreshInFlight$: Observable<AuthResponse> | null = null;

  refresh(): Observable<AuthResponse> {
    // Single-flight: the refresh token is SINGLE-USE (rotation), so two
    // concurrent 401s racing separate refreshes would revoke each other
    // and randomly log the user out. Everyone shares one rotation.
    this.refreshInFlight$ ??= this.http
      .post<AuthResponse>(`${environment.apiUrl}/auth/refresh`, {
        refreshToken: this._session()?.refreshToken ?? '',
        // Stay in the clinic being worked in — without this, multi-clinic
        // owners were silently flipped back to their first clinic mid-task
        tenantId: this.currentTenantId() || null,
      })
      .pipe(
        tap((response) => this.storeSession(response)),
        finalize(() => (this.refreshInFlight$ = null)),
        shareReplay({ bufferSize: 1, refCount: false }),
      );
    return this.refreshInFlight$;
  }

  /** Callbacks that wipe tenant-scoped cached state on logout. Services
   *  register themselves so AuthService needn't import each feature service
   *  (and risk a DI cycle). Without this, the next user on a shared reception
   *  PC saw the previous clinic's trial banner, settings and unread badge. */
  private readonly resetHooks = new Set<() => void>();

  onLogout(reset: () => void): void {
    this.resetHooks.add(reset);
  }

  logout(): void {
    localStorage.removeItem(STORAGE_KEY);
    this._session.set(null);
    this.resetHooks.forEach((reset) => reset());
    void this.router.navigate(['/login']);
  }

  accessToken(): string | null {
    return this._session()?.accessToken ?? null;
  }

  private storeSession(response: AuthResponse): void {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(response));
    this._session.set(response);
  }
}

function readStoredSession(): AuthResponse | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as AuthResponse) : null;
  } catch {
    return null;
  }
}
