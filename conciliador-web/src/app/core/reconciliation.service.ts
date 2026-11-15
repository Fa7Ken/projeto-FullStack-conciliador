import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { OperationType, PagedResult, ReconciliationDetail, ReconciliationSummary } from './api.models';

export interface RunReconciliationInput {
  operation: OperationType;
  startDate: string;
  endDate: string;
  salesFiles: File[];
  statementFiles: File[];
}

@Injectable({ providedIn: 'root' })
export class ReconciliationService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/reconciliations';

  run(input: RunReconciliationInput): Observable<ReconciliationDetail> {
    const form = new FormData();
    form.append('operation', input.operation);
    form.append('startDate', input.startDate);
    form.append('endDate', input.endDate);
    input.salesFiles.forEach((file) => form.append('salesFiles', file, file.name));
    input.statementFiles.forEach((file) => form.append('statementFiles', file, file.name));

    return this.http.post<ReconciliationDetail>(this.baseUrl, form);
  }

  getById(id: string): Observable<ReconciliationDetail> {
    return this.http.get<ReconciliationDetail>(`${this.baseUrl}/${encodeURIComponent(id)}`);
  }

  list(page: number, pageSize: number): Observable<PagedResult<ReconciliationSummary>> {
    return this.http.get<PagedResult<ReconciliationSummary>>(this.baseUrl, { params: { page, pageSize } });
  }
}
