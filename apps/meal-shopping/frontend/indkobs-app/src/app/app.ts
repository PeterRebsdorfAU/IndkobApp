import { Component, computed, inject, OnInit } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { Auth } from './auth';
import { TasksState } from './shared/tasks-state';
import { LogoMark } from './shared/logo';
import { ToastHost } from './shared/toast';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, LogoMark, ToastHost],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App implements OnInit {
  auth = inject(Auth);
  tasks = inject(TasksState);

  // Initialer til husstands-avataren i topbjælken (fx "Peters husstand" -> "PH").
  initials = computed(() => {
    const name = (this.auth.householdName() ?? '').trim();
    if (!name) return '·';
    const words = name.split(/\s+/).filter(Boolean);
    const letters = words.length >= 2 ? words[0][0] + words[1][0] : name.slice(0, 2);
    return letters.toUpperCase();
  });

  ngOnInit() {
    // Hent badge-tallet for Hjem-fanen (forfaldne pligter + åbne opgaver).
    if (this.auth.isLoggedIn()) this.tasks.refresh();
  }
}
