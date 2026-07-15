import { Component, inject, effect, OnInit } from '@angular/core';
import { Router, RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { Auth } from './auth';
import { TasksState } from './shared/tasks-state';
import { LogoMark } from './shared/logo';
import { ToastHost } from './shared/toast';
import { hasSeenOnboarding } from './shared/onboarding-state';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, LogoMark, ToastHost],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App implements OnInit {
  auth = inject(Auth);
  tasks = inject(TasksState);
  private router = inject(Router);

  constructor() {
    // Første-gangs-onboarding: send nye, indloggede brugere til guiden én gang.
    // Effekten reagerer på login (også ved bootstrap hvis man allerede er logget ind);
    // localStorage-flaget (sat når guiden afsluttes/springes over) stopper gentagelser.
    effect(() => {
      if (this.auth.isLoggedIn() && !hasSeenOnboarding()) {
        this.router.navigateByUrl('/velkommen');
      }
    });
  }

  ngOnInit() {
    // Hent badge-tallet for Hjem-fanen (forfaldne pligter + åbne opgaver).
    if (this.auth.isLoggedIn()) this.tasks.refresh();
  }
}
