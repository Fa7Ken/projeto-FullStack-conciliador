import { Routes } from '@angular/router';
import { authGuard } from './core/auth.guard';

export const routes: Routes = [
  { path: 'login', title: 'Entrar · Conciliador', loadComponent: () => import('./pages/login.component').then((m) => m.LoginComponent) },
  {
    path: 'conciliacoes/nova',
    title: 'Nova conciliação · Conciliador',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/new-reconciliation.component').then((m) => m.NewReconciliationComponent),
  },
  {
    path: 'conciliacoes',
    title: 'Histórico · Conciliador',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/history.component').then((m) => m.HistoryComponent),
  },
  {
    path: 'conciliacoes/:id',
    title: 'Resultado · Conciliador',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/reconciliation-detail.component').then((m) => m.ReconciliationDetailComponent),
  },
  { path: '', pathMatch: 'full', redirectTo: 'conciliacoes/nova' },
  { path: '**', redirectTo: 'conciliacoes/nova' },
];
