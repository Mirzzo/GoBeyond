import { Component, computed, inject } from '@angular/core';
import { NgIf } from '@angular/common';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-top-navbar',
  standalone: true,
  imports: [NgIf],
  templateUrl: './top-navbar.component.html',
  styleUrl: './top-navbar.component.scss'
})
export class TopNavbarComponent {
  private readonly authService = inject(AuthService);

  readonly userName = computed(() => this.authService.user()?.name ?? 'Unknown');
  readonly role = computed(() => this.authService.user()?.role ?? 'Guest');

  logout(): void {
    this.authService.logout();
  }
}
