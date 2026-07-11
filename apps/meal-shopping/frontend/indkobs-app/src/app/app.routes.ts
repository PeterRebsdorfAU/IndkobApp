import { Routes } from '@angular/router';
import { authGuard } from './auth-guard';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./pages/login').then(m => m.LoginPage) },
  // Offentlig delt indkøbsliste (via token-link) — bevidst UDEN auth-guard.
  { path: 'del/:token', loadComponent: () => import('./pages/shared-list').then(m => m.SharedListPage) },
  { path: '', pathMatch: 'full', redirectTo: 'uge' },
  { path: 'uge', canActivate: [authGuard], loadComponent: () => import('./pages/week-plan').then(m => m.WeekPlanPage) },
  { path: 'indkob', canActivate: [authGuard], loadComponent: () => import('./pages/shopping-list').then(m => m.ShoppingListPage) },
  { path: 'retter', canActivate: [authGuard], loadComponent: () => import('./pages/recipes').then(m => m.RecipesPage) },
  { path: 'lager', canActivate: [authGuard], loadComponent: () => import('./pages/pantry').then(m => m.PantryPage) },
  { path: 'tilbud', canActivate: [authGuard], loadComponent: () => import('./pages/offers').then(m => m.OffersPage) },
  { path: 'varegrupper', canActivate: [authGuard], loadComponent: () => import('./pages/item-groups').then(m => m.ItemGroupsPage) },
  { path: 'admin', canActivate: [authGuard], loadComponent: () => import('./pages/admin').then(m => m.AdminPage) },
  { path: '**', redirectTo: 'uge' }
];
