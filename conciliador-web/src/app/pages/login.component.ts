import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { problemMessage } from '../core/problem';

@Component({
  selector: 'app-login',
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    .wrap { min-height: 100vh; display: grid; place-items: center; padding: 1.5rem; }
    .box { width: 100%; max-width: 380px; }
    .field-row { margin-bottom: 0.9rem; }
    .btn { width: 100%; justify-content: center; margin-top: 0.4rem; }
  `,
  template: `
    <div class="wrap">
      <form class="card box" (ngSubmit)="submit()" novalidate>
        <h1>Conciliador</h1>
        <p class="muted">Vendas de cartão × extrato bancário, dia a dia.</p>

        @if (error(); as message) {
          <div class="alert error" role="alert">{{ message }}</div>
        }

        <div class="field-row">
          <label class="field" for="email">E-mail</label>
          <input id="email" name="email" type="email" autocomplete="username" required [(ngModel)]="email" />
        </div>
        <div class="field-row">
          <label class="field" for="password">Senha</label>
          <input id="password" name="password" type="password" autocomplete="current-password" required [(ngModel)]="password" />
        </div>

        <button class="btn" type="submit" [disabled]="loading()">
          @if (loading()) {
            <span class="spinner" aria-hidden="true"></span> Entrando…
          } @else {
            Entrar
          }
        </button>
      </form>
    </div>
  `,
})
export class LoginComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly email = signal('');
  protected readonly password = signal('');
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  protected submit(): void {
    if (this.loading()) return;

    if (!this.email().trim() || !this.password()) {
      this.error.set('Informe o e-mail e a senha.');
      return;
    }

    this.loading.set(true);
    this.error.set(null);

    this.auth.login(this.email().trim(), this.password()).subscribe({
      next: () => void this.router.navigate(['/conciliacoes/nova']),
      error: (err: unknown) => {
        this.error.set(problemMessage(err));
        this.loading.set(false);
      },
    });
  }
}
