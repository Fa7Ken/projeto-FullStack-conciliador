import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('stores the session after login and clears it on logout', () => {
    const auth = TestBed.inject(AuthService);
    expect(auth.isAuthenticated()).toBe(false);

    auth.login('admin@test.local', 'secret').subscribe();
    http.expectOne('/api/auth/login').flush({
      token: 'jwt',
      expiresAtUtc: new Date(Date.now() + 60_000).toISOString(),
      name: 'Admin',
      email: 'admin@test.local',
    });

    expect(auth.isAuthenticated()).toBe(true);
    expect(auth.token()).toBe('jwt');
    expect(auth.user()).toEqual({ name: 'Admin', email: 'admin@test.local' });

    auth.logout();
    expect(auth.token()).toBeNull();
    expect(sessionStorage.length).toBe(0);
  });

  it('drops an expired session instead of sending its token', () => {
    const auth = TestBed.inject(AuthService);

    auth.login('admin@test.local', 'secret').subscribe();
    http.expectOne('/api/auth/login').flush({
      token: 'old',
      expiresAtUtc: new Date(Date.now() - 1_000).toISOString(),
      name: 'Admin',
      email: 'admin@test.local',
    });

    expect(auth.token()).toBeNull();
    expect(auth.isAuthenticated()).toBe(false);
  });
});
