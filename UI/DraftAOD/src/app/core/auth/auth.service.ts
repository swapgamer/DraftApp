import { Injectable, computed, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { EMPTY, Observable, catchError, finalize, shareReplay, tap, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthResponse, AuthUser, LoginRequest, RegisterRequest } from '../models/auth.models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly state = signal<AuthResponse | null>(this.read());
  private refreshTimer?: ReturnType<typeof setTimeout>;
  private refreshRequest?: Observable<AuthResponse>;
  readonly user = computed<AuthUser | null>(() => this.state()?.user ?? null);
  readonly isAuthenticated = computed(() => this.user() !== null);
  readonly isAdmin = computed(() => this.user()?.roles.includes('Admin') ?? false);

  constructor(private readonly http: HttpClient) { const session = this.state(); if (session) this.scheduleRefresh(session); }
  login(request: LoginRequest) { return this.http.post<AuthResponse>(`${environment.apiUrl}/auth/login`, request).pipe(tap((response) => this.persist(response))); }
  register(request: RegisterRequest) { return this.http.post<AuthResponse>(`${environment.apiUrl}/auth/register`, request).pipe(tap((response) => this.persist(response))); }

  refresh(): Observable<AuthResponse> {
    const refreshToken = this.state()?.refreshToken;
    if (!refreshToken) return throwError(() => new Error('No refresh token is available.'));
    if (!this.refreshRequest) this.refreshRequest = this.http.post<AuthResponse>(`${environment.apiUrl}/auth/refresh`, { refreshToken }).pipe(
      tap((response) => this.persist(response)),
      // A stale local session is normal after a token expires or is revoked. Treat it
      // as signed out rather than leaving an expected 401 as an application error.
      catchError((error) => { if (error?.status === 401) { this.clear(); return EMPTY; } return throwError(() => error); }),
      finalize(() => this.refreshRequest = undefined),
      shareReplay({ bufferSize: 1, refCount: false }),
    );
    return this.refreshRequest;
  }

  logout() { return this.http.post<void>(`${environment.apiUrl}/auth/logout`, { refreshToken: this.state()?.refreshToken ?? '' }).pipe(finalize(() => this.clear())); }
  token(): string | undefined { return this.state()?.accessToken; }
  session(): AuthResponse | null { return this.state(); }

  private persist(value: AuthResponse): void { localStorage.setItem('draft-datastore.auth', JSON.stringify(value)); this.state.set(value); this.scheduleRefresh(value); }
  private scheduleRefresh(session: AuthResponse): void {
    if (this.refreshTimer) clearTimeout(this.refreshTimer);
    const refreshAt = new Date(session.accessTokenExpiresAtUtc).getTime() - Date.now() - 60_000;
    if (refreshAt <= 0) { this.clear(); return; }
    this.refreshTimer = setTimeout(() => this.refresh().subscribe({ error: () => this.clear() }), refreshAt);
  }
  private read(): AuthResponse | null {
    try {
      const value = JSON.parse(localStorage.getItem('draft-datastore.auth') ?? 'null') as AuthResponse | null;
      if (!value || new Date(value.accessTokenExpiresAtUtc).getTime() <= Date.now()) { localStorage.removeItem('draft-datastore.auth'); return null; }
      return value;
    } catch { localStorage.removeItem('draft-datastore.auth'); return null; }
  }
  private clear(): void { if (this.refreshTimer) clearTimeout(this.refreshTimer); this.refreshTimer = undefined; localStorage.removeItem('draft-datastore.auth'); this.state.set(null); }
}
