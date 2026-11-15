export type OperationType = 'Debit' | 'Credit';
export type LineStatus = 'Matched' | 'SalesAboveStatement' | 'StatementAboveSales' | 'MissingStatement' | 'MissingSales';
export type IssueSeverity = 'Info' | 'Warning' | 'Error';
export type SourceKind = 'SalesReport' | 'BankStatement';

export interface ReconciliationSummary {
  id: string;
  createdAtUtc: string;
  createdBy: string;
  operation: OperationType;
  startDate: string;
  endDate: string;
  totalSales: number;
  totalStatement: number;
  totalDifference: number;
  matchedLines: number;
  divergentLines: number;
  isFullyMatched: boolean;
}

export interface ReconciliationLine {
  date: string;
  brand: string;
  salesAmount: number;
  statementAmount: number;
  salesCount: number;
  difference: number;
  status: LineStatus;
}

export interface BrandSubtotal {
  brand: string;
  salesAmount: number;
  statementAmount: number;
  difference: number;
  matchedLines: number;
  divergentLines: number;
}

export interface ReconciliationIssue {
  severity: IssueSeverity;
  code: string;
  message: string;
}

export interface SourceDocument {
  fileName: string;
  kind: SourceKind;
  periodStart: string | null;
  periodEnd: string | null;
  entryCount: number;
}

export interface ReconciliationDetail {
  summary: ReconciliationSummary;
  brands: BrandSubtotal[];
  lines: ReconciliationLine[];
  issues: ReconciliationIssue[];
  sources: SourceDocument[];
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
}

export interface LoginResponse {
  token: string;
  expiresAtUtc: string;
  name: string;
  email: string;
}

export const OPERATION_LABELS: Record<OperationType, string> = { Debit: 'Débito', Credit: 'Crédito' };

export const STATUS_LABELS: Record<LineStatus, string> = {
  Matched: 'Bate',
  SalesAboveStatement: 'Vendas a maior',
  StatementAboveSales: 'Extrato a maior',
  MissingStatement: 'Sem repasse no extrato',
  MissingSales: 'Sem vendas no relatório',
};

export const SEVERITY_LABELS: Record<IssueSeverity, string> = { Error: 'Problema', Warning: 'Atenção', Info: 'Observação' };
