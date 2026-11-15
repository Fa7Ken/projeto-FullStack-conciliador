import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { OperationType } from '../core/api.models';
import { problemMessage } from '../core/problem';
import { ReconciliationService } from '../core/reconciliation.service';

type Side = 'sales' | 'statement';

const MAX_FILES_PER_SIDE = 12;
const MAX_FILE_SIZE = 20 * 1024 * 1024;
const MAX_RANGE_DAYS = 366;

@Component({
  selector: 'app-new-reconciliation',
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    .steps { display: grid; gap: 1rem; }
    .row { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 1rem; align-items: end; }
    .segmented { display: inline-flex; border: 1px solid var(--accent); border-radius: var(--radius); overflow: hidden; }
    .segmented label { padding: 0.5rem 1.2rem; cursor: pointer; font-weight: 600; color: var(--accent); }
    .segmented input { position: absolute; opacity: 0; pointer-events: none; }
    .segmented label:has(input:checked) { background: var(--accent); color: #fff; }
    .segmented label:has(input:focus-visible) { outline: 2px solid var(--ink); outline-offset: -3px; }
    .sides { display: grid; grid-template-columns: repeat(auto-fit, minmax(300px, 1fr)); gap: 1rem; }
    .drop { border: 1.5px dashed #b9b4a8; border-radius: var(--radius); padding: 0.9rem; background: #fbfaf7; }
    .drop.over { border-color: var(--accent); background: var(--info-bg); }
    .files { list-style: none; margin: 0.6rem 0 0; padding: 0; }
    .files li { display: flex; justify-content: space-between; align-items: center; gap: 0.5rem; padding: 0.3rem 0; border-top: 1px solid #ece9e2; font-size: 0.9rem; }
    .files .name { overflow-wrap: anywhere; }
    .remove { background: none; border: 0; color: var(--bad); cursor: pointer; font: inherit; padding: 0.1rem 0.3rem; }
    .actions { display: flex; align-items: center; gap: 1rem; flex-wrap: wrap; }
  `,
  template: `
    <div class="page">
      <div class="page-head">
        <div>
          <h1>Nova conciliação</h1>
          <p class="muted">Compare o que foi vendido no cartão com o que entrou no extrato, dia a dia e por bandeira.</p>
        </div>
      </div>

      <form class="steps" (ngSubmit)="submit()" novalidate>
        <section class="card">
          <h2>1. Operação e período de pagamento</h2>
          <div class="row">
            <div>
              <span class="field" id="operation-label">Operação</span>
              <div class="segmented" role="radiogroup" aria-labelledby="operation-label">
                <label><input type="radio" name="operation" value="Debit" [(ngModel)]="operation" /> Débito</label>
                <label><input type="radio" name="operation" value="Credit" [(ngModel)]="operation" /> Crédito</label>
              </div>
            </div>
            <div>
              <label class="field" for="month">Atalho: mês inteiro</label>
              <input id="month" name="month" type="month" [ngModel]="month()" (ngModelChange)="applyMonth($event)" />
            </div>
            <div>
              <label class="field" for="start">Pagamentos de</label>
              <input id="start" name="start" type="date" required [(ngModel)]="startDate" />
            </div>
            <div>
              <label class="field" for="end">até</label>
              <input id="end" name="end" type="date" required [(ngModel)]="endDate" />
            </div>
          </div>
          <p class="muted" style="margin: 0.75rem 0 0">
            O período é o dia em que o dinheiro cai na conta, não o dia da venda.
          </p>
        </section>

        <section class="card">
          <h2>2. Planilhas</h2>
          <div class="sides">
            @for (side of sides; track side.key) {
              <div>
                <span class="field">{{ side.title }}</span>
                <div
                  class="drop"
                  [class.over]="dragOver() === side.key"
                  (dragover)="onDragOver($event, side.key)"
                  (dragleave)="dragOver.set(null)"
                  (drop)="onDrop($event, side.key)"
                >
                  <input
                    type="file"
                    multiple
                    accept=".xlsx"
                    [attr.aria-label]="side.title"
                    (change)="onPicked($event, side.key)"
                  />
                  <p class="muted" style="margin: 0.4rem 0 0; font-size: 0.85rem">{{ side.hint }}</p>
                  @if (files(side.key).length > 0) {
                    <ul class="files">
                      @for (file of files(side.key); track file.name) {
                        <li>
                          <span class="name">{{ file.name }}</span>
                          <button type="button" class="remove" (click)="remove(side.key, file)" [attr.aria-label]="'Remover ' + file.name">
                            Remover
                          </button>
                        </li>
                      }
                    </ul>
                  }
                </div>
              </div>
            }
          </div>
        </section>

        @if (error(); as message) {
          <div class="alert error" role="alert"><strong>Não foi possível conciliar</strong>{{ message }}</div>
        }

        <div class="actions">
          <button class="btn" type="submit" [disabled]="loading()">
            @if (loading()) {
              <span class="spinner" aria-hidden="true"></span> Conciliando…
            } @else {
              Conciliar
            }
          </button>
          @if (loading()) {
            <span class="muted" aria-live="polite">Lendo as planilhas. Arquivos grandes levam alguns segundos.</span>
          } @else if (pending(); as message) {
            <span class="muted">{{ message }}</span>
          }
        </div>
      </form>
    </div>
  `,
})
export class NewReconciliationComponent {
  private readonly service = inject(ReconciliationService);
  private readonly router = inject(Router);

  protected readonly sides: { key: Side; title: string; hint: string }[] = [
    {
      key: 'sales',
      title: 'Relatórios de vendas da maquininha',
      hint: 'Envie também o mês anterior: o crédito de um mês vem das vendas do mês passado.',
    },
    { key: 'statement', title: 'Extratos bancários', hint: 'Um ou mais extratos que cubram o período escolhido.' },
  ];

  protected readonly operation = signal<OperationType>('Debit');
  protected readonly month = signal('');
  protected readonly startDate = signal('');
  protected readonly endDate = signal('');
  protected readonly salesFiles = signal<File[]>([]);
  protected readonly statementFiles = signal<File[]>([]);
  protected readonly dragOver = signal<Side | null>(null);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  // Primeiro item que ainda falta preencher; nulo quando o formulario esta pronto
  protected readonly pending = computed(() => {
    const start = this.startDate();
    const end = this.endDate();

    if (!start || !end) return 'Informe o período de pagamento.';
    if (end < start) return 'A data final não pode ser anterior à data inicial.';
    if (daysBetween(start, end) > MAX_RANGE_DAYS) return `O intervalo não pode passar de ${MAX_RANGE_DAYS} dias.`;
    if (this.salesFiles().length === 0) return 'Envie pelo menos um relatório de vendas.';
    if (this.statementFiles().length === 0) return 'Envie pelo menos um extrato.';
    return null;
  });

  protected files(side: Side): File[] {
    return side === 'sales' ? this.salesFiles() : this.statementFiles();
  }

  protected applyMonth(value: string): void {
    this.month.set(value);
    const match = /^(\d{4})-(\d{2})$/.exec(value);
    if (!match) return;

    const year = Number(match[1]);
    const monthNumber = Number(match[2]);
    const lastDay = new Date(year, monthNumber, 0).getDate();
    this.startDate.set(`${match[1]}-${match[2]}-01`);
    this.endDate.set(`${match[1]}-${match[2]}-${String(lastDay).padStart(2, '0')}`);
  }

  protected onPicked(event: Event, side: Side): void {
    const input = event.target as HTMLInputElement;
    this.addFiles(side, Array.from(input.files ?? []));
    // Limpa o campo para permitir escolher o mesmo arquivo de novo depois de remover
    input.value = '';
  }

  protected onDragOver(event: DragEvent, side: Side): void {
    event.preventDefault();
    this.dragOver.set(side);
  }

  protected onDrop(event: DragEvent, side: Side): void {
    event.preventDefault();
    this.dragOver.set(null);
    this.addFiles(side, Array.from(event.dataTransfer?.files ?? []));
  }

  protected remove(side: Side, file: File): void {
    const target = side === 'sales' ? this.salesFiles : this.statementFiles;
    target.update((current) => current.filter((f) => f !== file));
  }

  protected submit(): void {
    if (this.loading()) return;

    const pending = this.pending();
    if (pending) {
      this.error.set(pending);
      return;
    }

    this.loading.set(true);
    this.error.set(null);

    this.service
      .run({
        operation: this.operation(),
        startDate: this.startDate(),
        endDate: this.endDate(),
        salesFiles: this.salesFiles(),
        statementFiles: this.statementFiles(),
      })
      .subscribe({
        next: (result) => void this.router.navigate(['/conciliacoes', result.summary.id]),
        error: (err: unknown) => {
          this.error.set(problemMessage(err));
          this.loading.set(false);
        },
      });
  }

  private addFiles(side: Side, incoming: File[]): void {
    const target = side === 'sales' ? this.salesFiles : this.statementFiles;
    const current = target();
    const rejected: string[] = [];
    const accepted = [...current];

    for (const file of incoming) {
      if (!file.name.toLowerCase().endsWith('.xlsx')) rejected.push(`"${file.name}" não é .xlsx`);
      else if (file.size > MAX_FILE_SIZE) rejected.push(`"${file.name}" passa de 20 MB`);
      else if (accepted.some((f) => f.name === file.name)) rejected.push(`"${file.name}" já foi adicionado`);
      else if (accepted.length >= MAX_FILES_PER_SIDE) rejected.push(`"${file.name}" passa do limite de ${MAX_FILES_PER_SIDE} arquivos`);
      else accepted.push(file);
    }

    target.set(accepted);
    this.error.set(rejected.length > 0 ? `Arquivo(s) ignorado(s): ${rejected.join('; ')}.` : null);
  }
}

function daysBetween(start: string, end: string): number {
  return Math.round((Date.parse(end) - Date.parse(start)) / 86_400_000) + 1;
}
