import { Component, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ConciliacaoService, ResultadoConciliacao } from './services/conciliacao.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './app.component.html',
  styleUrl: './app.css'
})
export class AppComponent {
  arquivoVendas: File | null = null;
  arquivoExtrato: File | null = null;
  tipoOperacao: string = 'CREDITO';
  resultados: ResultadoConciliacao[] = [];
  carregando: boolean = false;
  processado: boolean = false; // Indica que a conciliação foi executada

  constructor(
    private service: ConciliacaoService,
    private cdr: ChangeDetectorRef
  ) {}

  onVendasSelected(event: any) { this.arquivoVendas = event.target.files[0]; }
  onExtratoSelected(event: any) { this.arquivoExtrato = event.target.files[0]; }

  executar() {
    if (!this.arquivoVendas || !this.arquivoExtrato) {
      alert('Selecione ambos os arquivos!');
      return;
    }

    this.carregando = true;
    this.processado = false;

    this.service.processar(this.arquivoVendas, this.arquivoExtrato, this.tipoOperacao)
      .subscribe({
        next: (data) => {
          console.log('Dados recebidos do Java:', data);
          this.resultados = data || [];
          this.carregando = false;
          this.processado = true; // Marca como processado
          this.cdr.detectChanges();
        },
        error: (err) => {
          alert('Erro no processamento: ' + (err.error?.message || err.message));
          this.carregando = false;
          this.processado = false;
          this.cdr.detectChanges();
        }
      });
  }
}