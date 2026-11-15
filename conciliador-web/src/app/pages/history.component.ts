import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { OPERATION_LABELS, PagedResult, ReconciliationSummary } from '../core/api.models';
import { problemMessage } from '../core/problem';
import { ReconciliationService } from '../core/reconciliation.service';

const PAGE_SIZE = 20;

@Component({
  selector: 'app-history',
  imports: [CurrencyPipe, DatePipe, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    .pager { display: flex; justify-content: space-between; align-items: center; margin-top: 0.9rem; gap: 0.75rem; flex-wrap: wrap; }
  `,
  template: `
    <div class="page">
      <div class="page-head">
        <div>
          <h1>Histórico</h1>
          <p class="muted" style="margin: 0">Conciliações já feitas, da mais recente para a mais antiga.</p>
        </div>
        <a class="btn" routerLink="/conciliacoes/nova">Nova conciliação</a>
      </div>

      @if (error(); as message) {
        <div class="alert error" role="alert">{{ message }}</div>
      }

      <section class="card">
        @if (loading()) {
          <p class="muted" aria-live="polite"><span class="spinner" aria-hidden="true"></span> Carregando…</p>
        } @else if (page(); as p) {
          @if (p.items.length === 0) {
            <p class="muted" style="margin: 0">Nenhuma conciliação ainda. Comece por <a routerLink="/conciliacoes/nova">uma nova conciliação</a>.</p>
          } @else {
            <div class="table-wrap">
              <table class="grid">
                <thead>
                  <tr>
                    <th>Feita em</th><th>Operação</th><th>Período de pagamento</th><th class="num">Vendas</th>
                    <th class="num">Extrato</th><th class="num">Diferença</th><th>Situação</th>
                  </tr>
                </thead>
                <tbody>
                  @for (item of p.items; track item.id) {
                    <tr class="clickable" (click)="open(item)">
                      <td><a [routerLink]="['/conciliacoes', item.id]" (click)="$event.stopPropagation()">{{ item.createdAtUtc | date: 'dd/MM/yyyy HH:mm' }}</a></td>
                      <td>{{ operationLabels[item.operation] }}</td>
                      <td>{{ item.startDate | date: 'dd/MM/yyyy' }} a {{ item.endDate | date: 'dd/MM/yyyy' }}</td>
                      <td class="num">{{ item.totalSales | currency: 'BRL' }}</td>
                      <td class="num">{{ item.totalStatement | currency: 'BRL' }}</td>
                      <td class="num" [class]="item.totalDifference === 0 ? 'diff-zero' : 'diff-pos'">{{ item.totalDifference | currency: 'BRL' }}</td>
                      <td>
                        @if (item.isFullyMatched) {
                          <span class="tag ok">Tudo bate</span>
                        } @else if (item.matchedLines + item.divergentLines === 0) {
                          <span class="tag warn">Sem lançamentos</span>
                        } @else {
                          <span class="tag bad">{{ item.divergentLines }} divergência(s)</span>
                        }
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
            <div class="pager">
              <span class="muted">{{ p.total }} conciliação(ões) · página {{ p.page }} de {{ totalPages() }}</span>
              <span>
                <button type="button" class="btn ghost small" [disabled]="p.page <= 1" (click)="load(p.page - 1)">Anterior</button>
                <button type="button" class="btn ghost small" [disabled]="p.page >= totalPages()" (click)="load(p.page + 1)">Próxima</button>
              </span>
            </div>
          }
        }
      </section>
    </div>
  `,
})
export class HistoryComponent {
  private readonly service = inject(ReconciliationService);
  private readonly router = inject(Router);

  protected readonly operationLabels = OPERATION_LABELS;
  protected readonly page = signal<PagedResult<ReconciliationSummary> | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly totalPages = computed(() => {
    const current = this.page();
    return current ? Math.max(1, Math.ceil(current.total / current.pageSize)) : 1;
  });

  constructor() {
    this.load(1);
  }

  protected load(pageNumber: number): void {
    this.loading.set(true);
    this.error.set(null);

    this.service.list(pageNumber, PAGE_SIZE).subscribe({
      next: (result) => {
        this.page.set(result);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.error.set(problemMessage(err));
        this.loading.set(false);
      },
    });
  }

  protected open(item: ReconciliationSummary): void {
    void this.router.navigate(['/conciliacoes', item.id]);
  }
}
