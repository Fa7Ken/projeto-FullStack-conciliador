import { HttpErrorResponse } from '@angular/common/http';

// Extrai a mensagem do ProblemDetails devolvido pela API; cai em um texto generico quando nao ha
export function problemMessage(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    if (error.status === 0) {
      return 'Não foi possível falar com o servidor. Verifique se a API está no ar e tente de novo.';
    }

    const detail = error.error?.detail;
    if (typeof detail === 'string' && detail.trim().length > 0) {
      return detail;
    }

    if (error.status === 413) {
      return 'Os arquivos enviados são grandes demais. Envie menos arquivos por vez.';
    }

    if (error.status === 429) {
      return 'Muitas tentativas em pouco tempo. Aguarde um minuto e tente de novo.';
    }
  }

  return 'Ocorreu um erro inesperado. Tente novamente em instantes.';
}
