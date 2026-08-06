import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { provideRouter } from '@angular/router';
import { AuthService } from '../auth/auth.service';
import { adminGuard, authGuard } from './auth.guards';

describe('route guards', () => {
  const authState = { isAuthenticated: jasmine.createSpy(), isAdmin: jasmine.createSpy() };

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideRouter([]), { provide: AuthService, useValue: authState }] });
    authState.isAuthenticated.calls.reset();
    authState.isAdmin.calls.reset();
  });

  it('redirects guests to sign in and preserves the requested URL', () => {
    authState.isAuthenticated.and.returnValue(false);
    const result = TestBed.runInInjectionContext(() => authGuard({} as never, { url: '/hall-of-fame?page=2' } as never));
    expect(TestBed.inject(Router).serializeUrl(result as ReturnType<Router['createUrlTree']>)).toBe('/login?returnUrl=%2Fhall-of-fame%3Fpage%3D2');
  });

  it('allows authenticated users through', () => {
    authState.isAuthenticated.and.returnValue(true);
    expect(TestBed.runInInjectionContext(() => authGuard({} as never, { url: '/search' } as never))).toBeTrue();
  });

  it('redirects non-admin users to the access-restricted page', () => {
    authState.isAdmin.and.returnValue(false);
    const result = TestBed.runInInjectionContext(() => adminGuard({} as never, {} as never));
    expect(TestBed.inject(Router).serializeUrl(result as ReturnType<Router['createUrlTree']>)).toBe('/unauthorized');
  });
});
