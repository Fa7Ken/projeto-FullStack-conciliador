import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { registerLocaleData } from '@angular/common';
import localePt from '@angular/common/locales/pt';
import { LOCALE_ID } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ReconciliationDetail } from '../core/api.models';
import { ReconciliationDetailComponent } from './reconciliation-detail.component';

const DETAIL: ReconciliationDetail = {
  summary: {
    id: 'abc',
    createdAtUtc: '2026-04-10T12:00:00Z',
    createdBy: 'admin@test.local',
    operation: 'Debit',
    startDate: '2026-03-01',
    endDate: '2026-03-31',
    totalSales: 350,
    totalStatement: 200,
    totalDifference: 150,
    matchedLines: 1,
    divergentLines: 1,
    isFullyMatched: false,
  },
  brands: [
    { brand: 'Mastercard', salesAmount: 250, statementAmount: 100, difference: 150, matchedLines: 0, divergentLines: 1 },
    { brand: 'Visa', salesAmount: 100, statementAmount: 100, difference: 0, matchedLines: 1, divergentLines: 0 },
  ],
  lines: [
    { date: '2026-03-02', brand: 'Mastercard', salesAmount: 250, statementAmount: 100, salesCount: 4, difference: 150, status: 'SalesAboveStatement' },
    { date: '2026-03-02', brand: 'Visa', salesAmount: 100, statementAmount: 100, salesCount: 2, difference: 0, status: 'Matched' },
  ],
  issues: [{ severity: 'Warning', code: 'SALES_COVERAGE_GAP', message: 'O relatório do período anterior parece estar faltando.' }],
  sources: [{ fileName: 'extrato-03.xlsx', kind: 'BankStatement', periodStart: '2026-03-01', periodEnd: '2026-03-31', entryCount: 2 }],
};

describe('ReconciliationDetailComponent', () => {
  let fixture: ComponentFixture<ReconciliationDetailComponent>;
  let http: HttpTestingController;
  let element: HTMLElement;

  beforeEach(async () => {
    registerLocaleData(localePt);
    TestBed.configureTestingModule({
      imports: [ReconciliationDetailComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting(), { provide: LOCALE_ID, useValue: 'pt-BR' }],
    });
    fixture = TestBed.createComponent(ReconciliationDetailComponent);
    fixture.componentRef.setInput('id', 'abc');
    http = TestBed.inject(HttpTestingController);
    element = fixture.nativeElement as HTMLElement;
    await fixture.whenStable();
  });

  afterEach(() => http.verify());

  // Regressao do defeito antigo: os dados chegavam, mas a tela nao era redesenhada
  it('renders the result as soon as the response arrives', async () => {
    expect(element.textContent).toContain('Carregando a conciliação');

    http.expectOne('/api/reconciliations/abc').flush(DETAIL);
    await fixture.whenStable();

    expect(element.querySelector('h1')?.textContent).toContain('Débito · 01/03/2026 a 31/03/2026');
    expect(element.textContent).toContain('O relatório do período anterior parece estar faltando.');
    expect(element.querySelectorAll('section.card')[1].querySelectorAll('tbody tr').length).toBe(2);
    expect(element.textContent).toContain('Vendas a maior');
    expect(element.textContent).toMatch(/R\$\s150,00/);
  });

  it('filters down to the divergent days', async () => {
    http.expectOne('/api/reconciliations/abc').flush(DETAIL);
    await fixture.whenStable();

    const checkbox = element.querySelector<HTMLInputElement>('input[type="checkbox"]')!;
    checkbox.checked = true;
    checkbox.dispatchEvent(new Event('change'));
    await fixture.whenStable();

    const rows = element.querySelectorAll('section.card')[1].querySelectorAll('tbody tr');
    expect(rows.length).toBe(1);
    expect(rows[0].textContent).toContain('Mastercard');
  });

  it('shows the API message when the reconciliation does not exist', async () => {
    http.expectOne('/api/reconciliations/abc').flush({ detail: 'Conciliação não encontrada.' }, { status: 404, statusText: 'Not Found' });
    await fixture.whenStable();

    expect(element.querySelector('[role="alert"]')?.textContent).toContain('Conciliação não encontrada.');
  });
});
