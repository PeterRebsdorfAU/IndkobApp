import { Component, inject, OnInit } from '@angular/core';
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

  ngOnInit() {
    // Hent badge-tallet for Hjem-fanen (forfaldne pligter + åbne opgaver).
    if (this.auth.isLoggedIn()) this.tasks.refresh();
  }
}
