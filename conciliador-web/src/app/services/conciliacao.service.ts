import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface ResultadoConciliacao {
  dataEfetiva: string;
  bandeira: string;
  valorVendido: number;
  valorExtrato: number;
  diferenca: number;
}

@Injectable({
  providedIn: 'root'
})
export class ConciliacaoService {
  private apiUrl = 'http://localhost:8080/api/conciliacao/processar';

  constructor(private http: HttpClient) { }

  processar(vendas: File, extrato: File, tipo: string): Observable<ResultadoConciliacao[]> {
    const formData = new FormData();
    formData.append('vendas', vendas);
    formData.append('extrato', extrato);
    formData.append('tipo', tipo);

    return this.http.post<ResultadoConciliacao[]>(this.apiUrl, formData);
  }
}