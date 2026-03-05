import { Component, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { NgFor } from '@angular/common';
import { AuthService } from '../../../core/services/auth.service';

type NavItem = { label: string; path: string };

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, NgFor],
  templateUrl: './sidebar.component.html',
  styleUrl: './sidebar.component.scss'
})
export class SidebarComponent {
  private readonly authService = inject(AuthService);

  readonly adminItems: NavItem[] = [
    { label: 'Mentor Requests', path: '/admin/mentor-requests' },
    { label: 'Mentors', path: '/admin/mentors' },
    { label: 'Manage Subscriptions', path: '/admin/subscriptions' },
    { label: 'Clients', path: '/admin/clients' },
    { label: 'Dashboard', path: '/admin/dashboard' }
  ];

  readonly mentorItems: NavItem[] = [
    { label: 'Collaboration Requests', path: '/mentor/collaboration-requests' },
    { label: 'Published Plans', path: '/mentor/published-plans' },
    { label: 'Create Plan', path: '/mentor/create-plan' },
    { label: 'Subscribers', path: '/mentor/subscribers' }
  ];

  readonly items = computed(() => {
    const role = this.authService.user()?.role;
    return role === 'Admin' ? this.adminItems : this.mentorItems;
  });
}
