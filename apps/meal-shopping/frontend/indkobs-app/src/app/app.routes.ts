import { Routes } from '@angular/router';
import { authGuard } from './auth-guard';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./pages/login').then(m => m.LoginPage) },
  // Selvbetjent brugerkonti (T2) — bevidst UDEN auth-guard (tilgås før login).
  { path: 'opret', loadComponent: () => import('./pages/signup').then(m => m.SignupPage) },
  { path: 'glemt-kode', loadComponent: () => import('./pages/forgot-password').then(m => m.ForgotPasswordPage) },
  { path: 'nulstil-kode', loadComponent: () => import('./pages/reset-password').then(m => m.ResetPasswordPage) },
  { path: 'bekraeft-email', loadComponent: () => import('./pages/confirm-email').then(m => m.ConfirmEmailPage) },
  // Offentlig delt indkøbsliste (via token-link) — bevidst UDEN auth-guard.
  { path: 'del/:token', loadComponent: () => import('./pages/shared-list').then(m => m.SharedListPage) },
  // Juridiske sider — bevidst UDEN auth-guard, så de kan tilgås før login/signup.
  { path: 'privatliv', loadComponent: () => import('./pages/legal-privacy').then(m => m.LegalPrivacyPage) },
  { path: 'handelsbetingelser', loadComponent: () => import('./pages/legal-terms').then(m => m.LegalTermsPage) },
  { path: '', pathMatch: 'full', redirectTo: 'uge' },
  // Første-gangs-onboarding + hjælp/FAQ
  { path: 'velkommen', canActivate: [authGuard], loadComponent: () => import('./pages/onboarding').then(m => m.OnboardingPage) },
  { path: 'hjaelp', canActivate: [authGuard], loadComponent: () => import('./pages/faq').then(m => m.FaqPage) },
  { path: 'uge', canActivate: [authGuard], loadComponent: () => import('./pages/week-plan').then(m => m.WeekPlanPage) },
  { path: 'indkob', canActivate: [authGuard], loadComponent: () => import('./pages/shopping-list').then(m => m.ShoppingListPage) },
  { path: 'retter', canActivate: [authGuard], loadComponent: () => import('./pages/recipes').then(m => m.RecipesPage) },
  { path: 'hjem', canActivate: [authGuard], loadComponent: () => import('./pages/home-tasks').then(m => m.HomeTasksPage) },
  { path: 'varegrupper', canActivate: [authGuard], loadComponent: () => import('./pages/item-groups').then(m => m.ItemGroupsPage) },
  { path: 'varer', canActivate: [authGuard], loadComponent: () => import('./pages/admin').then(m => m.AdminPage) },
  { path: 'admin', redirectTo: 'varer' }, // gammelt navn -> nyt
  { path: '**', redirectTo: 'uge' }
];
