import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    header { background: var(--ink); color: #fff; }
    .bar { max-width: 1080px; margin: 0 auto; padding: 0.7rem 1.25rem; display: flex; align-items: center; gap: 1.5rem; flex-wrap: wrap; }
    .brand { font-weight: 700; letter-spacing: 0.02em; color: #fff; text-decoration: none; }
    nav { display: flex; gap: 0.25rem; flex: 1; }
    nav a { color: #c9cfdb; text-decoration: none; padding: 0.3rem 0.7rem; border-radius: var(--radius); }
    nav a.active { background: rgba(255, 255, 255, 0.14); color: #fff; }
    .user { display: flex; align-items: center; gap: 0.75rem; font-size: 0.9rem; color: #c9cfdb; }
    .user button { background: transparent; border: 1px solid #6d7688; color: #fff; border-radius: var(--radius); padding: 0.25rem 0.7rem; font: inherit; cursor: pointer; }
  `,
  template: `
    @if (auth.isAuthenticated()) {
      <header>
        <div class="bar">
          <a class="brand" routerLink="/conciliacoes/nova">Conciliador</a>
          <nav aria-label="Principal">
            <a routerLink="/conciliacoes/nova" routerLinkActive="active">Nova conciliação</a>
            <a routerLink="/conciliacoes" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }">Histórico</a>
          </nav>
          <div class="user">
            <span>{{ auth.user()?.name }}</span>
            <button type="button" (click)="logout()">Sair</button>
          </div>
        </div>
      </header>
    }
    <main>
      <router-outlet />
    </main>
  `,
})
export class AppComponent {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected logout(): void {
    this.auth.logout();
    void this.router.navigate(['/login']);
  }
}
