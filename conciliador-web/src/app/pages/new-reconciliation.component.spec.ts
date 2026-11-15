import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { NewReconciliationComponent } from './new-reconciliation.component';

describe('NewReconciliationComponent', () => {
  let fixture: ComponentFixture<NewReconciliationComponent>;
  let http: HttpTestingController;
  let element: HTMLElement;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      imports: [NewReconciliationComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
    fixture = TestBed.createComponent(NewReconciliationComponent);
    http = TestBed.inject(HttpTestingController);
    element = fixture.nativeElement as HTMLElement;
    await fixture.whenStable();
  });

  afterEach(() => http.verify());

  function setInput(selector: string, value: string): void {
    const input = element.querySelector<HTMLInputElement>(selector)!;
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }

  function pick(index: number, ...names: string[]): void {
    const input = element.querySelectorAll<HTMLInputElement>('input[type="file"]')[index];
    Object.defineProperty(input, 'files', { value: names.map((name) => new File(['x'], name)), configurable: true });
    input.dispatchEvent(new Event('change'));
  }

  async function submit(): Promise<void> {
    element.querySelector('form')!.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
  }

  it('explains what is missing instead of calling the API', async () => {
    await submit();

    expect(element.querySelector('[role="alert"]')?.textContent).toContain('Informe o período de pagamento.');
  });

  it('fills the whole month from the shortcut', async () => {
    setInput('#month', '2026-02');
    await fixture.whenStable();

    expect(element.querySelector<HTMLInputElement>('#start')!.value).toBe('2026-02-01');
    expect(element.querySelector<HTMLInputElement>('#end')!.value).toBe('2026-02-28');
  });

  it('accepts several files per side and ignores the ones that are not .xlsx', async () => {
    pick(0, 'vendas-02.xlsx', 'vendas-03.xlsx', 'notas.pdf');
    await fixture.whenStable();

    expect(element.querySelectorAll('.files li').length).toBe(2);
    expect(element.querySelector('[role="alert"]')?.textContent).toContain('"notas.pdf" não é .xlsx');
  });

  it('sends everything as multipart and opens the result', async () => {
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    setInput('#month', '2026-03');
    pick(0, 'vendas-02.xlsx', 'vendas-03.xlsx');
    pick(1, 'extrato-03.xlsx');
    await fixture.whenStable();

    await submit();
    expect(element.querySelector<HTMLButtonElement>('button[type="submit"]')!.disabled).toBe(true);

    const request = http.expectOne('/api/reconciliations');
    const body = request.request.body as FormData;
    expect(body.get('operation')).toBe('Debit');
    expect(body.get('startDate')).toBe('2026-03-01');
    expect(body.get('endDate')).toBe('2026-03-31');
    expect(body.getAll('salesFiles').length).toBe(2);
    expect(body.getAll('statementFiles').length).toBe(1);

    request.flush({ summary: { id: 'abc' } });
    expect(navigate).toHaveBeenCalledWith(['/conciliacoes', 'abc']);
  });

  it('shows the message returned by the API and lets the user try again', async () => {
    setInput('#month', '2026-03');
    pick(0, 'vendas-03.xlsx');
    pick(1, 'extrato-03.xlsx');
    await fixture.whenStable();
    await submit();

    http.expectOne('/api/reconciliations').flush(
      { detail: '"vendas-03.xlsx" não parece ser um relatório de vendas.' },
      { status: 422, statusText: 'Unprocessable Entity' },
    );
    await fixture.whenStable();

    expect(element.querySelector('[role="alert"]')?.textContent).toContain('não parece ser um relatório de vendas');
    expect(element.querySelector<HTMLButtonElement>('button[type="submit"]')!.disabled).toBe(false);
  });
});
