import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'uge' },
  { path: 'uge', loadComponent: () => import('./pages/week-plan').then(m => m.WeekPlanPage) },
  { path: 'indkob', loadComponent: () => import('./pages/shopping-list').then(m => m.ShoppingListPage) },
  { path: 'retter', loadComponent: () => import('./pages/recipes').then(m => m.RecipesPage) },
  { path: 'varegrupper', loadComponent: () => import('./pages/item-groups').then(m => m.ItemGroupsPage) },
  { path: 'admin', loadComponent: () => import('./pages/admin').then(m => m.AdminPage) },
  { path: '**', redirectTo: 'uge' }
];
