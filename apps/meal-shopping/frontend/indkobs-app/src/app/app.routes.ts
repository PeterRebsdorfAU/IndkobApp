import { Routes } from '@angular/router';
import { authGuard } from './auth-guard';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./pages/login').then(m => m.LoginPage) },
  // Offentlig delt indkøbsliste (via token-link) — bevidst UDEN auth-guard.
  { path: 'del/:token', loadComponent: () => import('./pages/shared-list').then(m => m.SharedListPage) },
  // Butiks-demo-side — egen adgang (butiks-nøgle), IKKE husstands-login.
  { path: 'butik', loadComponent: () => import('./pages/store').then(m => m.StorePage) },
  { path: '', pathMatch: 'full', redirectTo: 'uge' },
  { path: 'uge', canActivate: [authGuard], loadComponent: () => import('./pages/week-plan').then(m => m.WeekPlanPage) },
  { path: 'indkob', canActivate: [authGuard], loadComponent: () => import('./pages/shopping-list').then(m => m.ShoppingListPage) },
  { path: 'retter', canActivate: [authGuard], loadComponent: () => import('./pages/recipes').then(m => m.RecipesPage) },
  { path: 'lager', canActivate: [authGuard], loadComponent: () => import('./pages/pantry').then(m => m.PantryPage) },
  { path: 'hjem', canActivate: [authGuard], loadComponent: () => import('./pages/home-tasks').then(m => m.HomeTasksPage) },
  { path: 'varegrupper', canActivate: [authGuard], loadComponent: () => import('./pages/item-groups').then(m => m.ItemGroupsPage) },
  { path: 'varer', canActivate: [authGuard], loadComponent: () => import('./pages/admin').then(m => m.AdminPage) },
  { path: 'admin', redirectTo: 'varer' }, // gammelt navn -> nyt
  { path: '**', redirectTo: 'uge' }
];
