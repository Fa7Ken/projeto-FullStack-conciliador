import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import {
  LineStatus,
  OPERATION_LABELS,
  ReconciliationDetail,
  ReconciliationLine,
  SEVERITY_LABELS,
  STATUS_LABELS,
} from '../core/api.models';
import { problemMessage } from '../core/problem';
import { ReconciliationService } from '../core/reconciliation.service';

interface LineRow extends ReconciliationLine {
  firstOfDay: boolean;
}

@Component({
  selector: 'app-reconciliation-detail',
  imports: [CurrencyPipe, DatePipe, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    .tiles { display: grid; grid-template-columns: repeat(auto-fit, minmax(190px, 1fr)); gap: 0.75rem; margin-bottom: 1rem; }
    .tile { background: var(--surface); border: 1px solid var(--line); border-radius: var(--radius); padding: 0.8rem 1rem; }
    .tile .label { font-size: 0.75rem; text-transform: uppercase; letter-spacing: 0.04em; color: var(--ink-soft); }
    .tile .value { font-family: var(--mono); font-variant-numeric: tabular-nums; font-size: 1.25rem; font-weight: 700; }
    .section-head { display: flex; justify-content: space-between; align-items: center; gap: 0.75rem; flex-wrap: wrap; margin-bottom: 0.75rem; }
    .section-head h2 { margin: 0; }
    .tools { display: flex; gap: 0.75rem; align-items: center; flex-wrap: wrap; }
    .sources { list-style: none; padding: 0; margin: 0; font-size: 0.9rem; }
    .sources li { padding: 0.25rem 0; overflow-wrap: anywhere; }
  `,
  template: `
    <div class="page">
      @if (loading()) {
        <p class="muted" aria-live="polite"><span class="spinner" aria-hidden="true"></span> Carregando a conciliação…</p>
      } @else if (error(); as message) {
        <div class="alert error" role="alert">{{ message }}</div>
        <a class="btn ghost" routerLink="/conciliacoes">Voltar ao histórico</a>
      } @else if (detail(); as d) {
        <div class="page-head">
          <div>
            <h1>{{ operationLabel() }} · {{ d.summary.startDate | date: 'dd/MM/yyyy' }} a {{ d.summary.endDate | date: 'dd/MM/yyyy' }}</h1>
            <p class="muted" style="margin: 0">
              Conciliado em {{ d.summary.createdAtUtc | date: 'dd/MM/yyyy HH:mm' }} por {{ d.summary.createdBy }}
            </p>
          </div>
          <a class="btn ghost" routerLink="/conciliacoes/nova">Nova conciliação</a>
        </div>

        @if (d.summary.isFullyMatched) {
          <div class="alert ok" role="status"><strong>Tudo bate</strong>Todos os {{ d.lines.length }} repasses do período conferem com as vendas.</div>
        }

        @for (issue of d.issues; track issue.code) {
          <div class="alert" [class]="issue.severity.toLowerCase()" [attr.role]="issue.severity === 'Error' ? 'alert' : 'status'">
            <strong>{{ severityLabels[issue.severity] }}</strong>{{ issue.message }}
          </div>
        }

        <div class="tiles">
          <div class="tile"><div class="label">Vendas (líquido)</div><div class="value">{{ d.summary.totalSales | currency: 'BRL' }}</div></div>
          <div class="tile"><div class="label">Extrato</div><div class="value">{{ d.summary.totalStatement | currency: 'BRL' }}</div></div>
          <div class="tile">
            <div class="label">Diferença (vendas − extrato)</div>
            <div class="value" [class]="diffClass(d.summary.totalDifference)">{{ d.summary.totalDifference | currency: 'BRL' }}</div>
          </div>
          <div class="tile">
            <div class="label">Repasses conferidos</div>
            <div class="value">{{ d.summary.matchedLines }} de {{ d.lines.length }}</div>
          </div>
        </div>

        @if (d.brands.length > 0) {
          <section class="card">
            <h2>Por bandeira</h2>
            <div class="table-wrap">
              <table class="grid">
                <thead>
                  <tr><th>Bandeira</th><th class="num">Vendas</th><th class="num">Extrato</th><th class="num">Diferença</th><th class="num">Batem</th><th class="num">Divergem</th></tr>
                </thead>
                <tbody>
                  @for (brand of d.brands; track brand.brand) {
                    <tr>
                      <td>{{ brand.brand }}</td>
                      <td class="num">{{ brand.salesAmount | currency: 'BRL' }}</td>
                      <td class="num">{{ brand.statementAmount | currency: 'BRL' }}</td>
                      <td class="num" [class]="diffClass(brand.difference)">{{ brand.difference | currency: 'BRL' }}</td>
                      <td class="num">{{ brand.matchedLines }}</td>
                      <td class="num">{{ brand.divergentLines }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </section>

          <section class="card">
            <div class="section-head">
              <h2>Dia a dia</h2>
              <div class="tools">
                <label><input type="checkbox" [checked]="onlyDivergent()" (change)="onlyDivergent.set($any($event.target).checked)" /> Só divergências ({{ d.summary.divergentLines }})</label>
                <button type="button" class="btn ghost small" (click)="exportCsv()">Baixar CSV</button>
              </div>
            </div>
            @if (rows().length === 0) {
              <p class="muted">Nenhuma divergência no período.</p>
            } @else {
              <div class="table-wrap">
                <table class="grid">
                  <thead>
                    <tr>
                      <th>Pagamento</th><th>Bandeira</th><th class="num">Qtd. vendas</th><th class="num">Vendas</th>
                      <th class="num">Extrato</th><th class="num">Diferença</th><th>Situação</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (row of rows(); track row.date + row.brand) {
                      <tr [class.day-start]="row.firstOfDay" [class.divergent]="row.status !== 'Matched'">
                        <td>{{ row.firstOfDay ? (row.date | date: 'dd/MM/yyyy') : '' }}</td>
                        <td>{{ row.brand }}</td>
                        <td class="num">{{ row.salesCount }}</td>
                        <td class="num">{{ row.salesAmount | currency: 'BRL' }}</td>
                        <td class="num">{{ row.statementAmount | currency: 'BRL' }}</td>
                        <td class="num" [class]="diffClass(row.difference)">{{ row.difference | currency: 'BRL' }}</td>
                        <td><span class="tag" [class]="tagClass(row.status)">{{ statusLabels[row.status] }}</span></td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            }
          </section>
        }

        <section class="card">
          <h2>Arquivos usados</h2>
          <ul class="sources">
            @for (source of d.sources; track source.fileName + source.kind) {
              <li>
                <strong>{{ source.kind === 'SalesReport' ? 'Vendas' : 'Extrato' }}:</strong> {{ source.fileName }}
                <span class="muted">
                  @if (source.periodStart && source.periodEnd) {
                    · {{ source.periodStart | date: 'dd/MM/yyyy' }} a {{ source.periodEnd | date: 'dd/MM/yyyy' }}
                  }
                  · {{ source.entryCount }} lançamento(s) de cartão
                </span>
              </li>
            }
          </ul>
        </section>
      }
    </div>
  `,
})
export class ReconciliationDetailComponent {
  private readonly service = inject(ReconciliationService);

  // Preenchido pelo roteador a partir de /conciliacoes/:id
  readonly id = input.required<string>();

  protected readonly statusLabels = STATUS_LABELS;
  protected readonly severityLabels = SEVERITY_LABELS;

  protected readonly detail = signal<ReconciliationDetail | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly onlyDivergent = signal(false);

  protected readonly operationLabel = computed(() => {
    const current = this.detail();
    return current ? OPERATION_LABELS[current.summary.operation] : '';
  });

  protected readonly rows = computed<LineRow[]>(() => {
    const current = this.detail();
    if (!current) return [];

    const lines = this.onlyDivergent() ? current.lines.filter((l) => l.status !== 'Matched') : current.lines;
    return lines.map((line, index) => ({ ...line, firstOfDay: index === 0 || lines[index - 1].date !== line.date }));
  });

  constructor() {
    // Recarrega sempre que o id da rota mudar; cancela a requisicao anterior se ainda estiver em andamento
    effect((onCleanup) => {
      const subscription = this.load(this.id());
      onCleanup(() => subscription.unsubscribe());
    });
  }

  protected diffClass(value: number): string {
    return value === 0 ? 'diff-zero' : value > 0 ? 'diff-pos' : 'diff-neg';
  }

  protected tagClass(status: LineStatus): string {
    if (status === 'Matched') return 'ok';
    return status === 'MissingSales' || status === 'MissingStatement' ? 'warn' : 'bad';
  }

  protected exportCsv(): void {
    const current = this.detail();
    if (!current) return;

    const header = ['Pagamento', 'Bandeira', 'Qtd vendas', 'Vendas', 'Extrato', 'Diferenca', 'Situacao'];
    const body = this.rows().map((row) =>
      [
        row.date.split('-').reverse().join('/'),
        row.brand,
        row.salesCount,
        formatDecimal(row.salesAmount),
        formatDecimal(row.statementAmount),
        formatDecimal(row.difference),
        STATUS_LABELS[row.status],
      ].join(';'),
    );

    // BOM para o Excel abrir os acentos corretamente; ponto e virgula e o separador do Excel em pt-BR
    const blob = new Blob(['﻿' + [header.join(';'), ...body].join('\r\n')], { type: 'text/csv;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `conciliacao-${current.summary.operation.toLowerCase()}-${current.summary.startDate}-a-${current.summary.endDate}.csv`;
    link.click();
    URL.revokeObjectURL(url);
  }

  private load(id: string): Subscription {
    this.loading.set(true);
    this.error.set(null);
    this.detail.set(null);

    return this.service.getById(id).subscribe({
      next: (result) => {
        this.detail.set(result);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.error.set(problemMessage(err));
        this.loading.set(false);
      },
    });
  }
}

function formatDecimal(value: number): string {
  return value.toFixed(2).replace('.', ',');
}
