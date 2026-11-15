import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, map, tap } from 'rxjs';
import { LoginResponse } from './api.models';

const STORAGE_KEY = 'conciliador.session';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly session = signal<LoginResponse | null>(readStoredSession());

  readonly user = computed(() => {
    const current = this.session();
    return current ? { name: current.name, email: current.email } : null;
  });

  readonly isAuthenticated = computed(() => this.session() !== null);

  token(): string | null {
    const current = this.session();
    if (!current) return null;

    // Token vencido: encerra a sessao em vez de mandar uma requisicao que vai falhar
    if (new Date(current.expiresAtUtc).getTime() <= Date.now()) {
      this.logout();
      return null;
    }

    return current.token;
  }

  login(email: string, password: string): Observable<void> {
    return this.http.post<LoginResponse>('/api/auth/login', { email, password }).pipe(
      tap((response) => {
        this.session.set(response);
        writeStoredSession(response);
      }),
      map(() => undefined),
    );
  }

  logout(): void {
    this.session.set(null);
    writeStoredSession(null);
  }
}

// sessionStorage pode falhar (modo privado, bloqueio do navegador); a sessao segue so em memoria
function readStoredSession(): LoginResponse | null {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY);
    if (!raw) return null;

    const parsed = JSON.parse(raw) as LoginResponse;
    return new Date(parsed.expiresAtUtc).getTime() > Date.now() ? parsed : null;
  } catch {
    return null;
  }
}

function writeStoredSession(session: LoginResponse | null): void {
  try {
    if (session) sessionStorage.setItem(STORAGE_KEY, JSON.stringify(session));
    else sessionStorage.removeItem(STORAGE_KEY);
  } catch {
    // Sem armazenamento disponivel: nada a fazer
  }
}
