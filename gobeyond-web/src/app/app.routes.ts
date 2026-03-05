import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { roleGuard } from './core/guards/role.guard';
import { ClientsComponent } from './features/admin/clients/clients.component';
import { DashboardComponent } from './features/admin/dashboard/dashboard.component';
import { MentorRequestsComponent } from './features/admin/mentor-requests/mentor-requests.component';
import { MentorsComponent } from './features/admin/mentors/mentors.component';
import { SubscriptionsComponent } from './features/admin/subscriptions/subscriptions.component';
import { LoginComponent } from './features/auth/login/login.component';
import { ShellComponent } from './features/layout/shell/shell.component';
import { CollaborationRequestsComponent } from './features/mentor/collaboration-requests/collaboration-requests.component';
import { CreatePlanComponent } from './features/mentor/create-plan/create-plan.component';
import { PublishedPlansComponent } from './features/mentor/published-plans/published-plans.component';
import { SubscribersComponent } from './features/mentor/subscribers/subscribers.component';

export const routes: Routes = [
  {
    path: 'login',
    component: LoginComponent
  },
  {
    path: '',
    component: ShellComponent,
    canActivate: [authGuard],
    children: [
      {
        path: 'admin/mentor-requests',
        component: MentorRequestsComponent,
        canActivate: [roleGuard],
        data: { roles: ['Admin'] }
      },
      {
        path: 'admin/mentors',
        component: MentorsComponent,
        canActivate: [roleGuard],
        data: { roles: ['Admin'] }
      },
      {
        path: 'admin/subscriptions',
        component: SubscriptionsComponent,
        canActivate: [roleGuard],
        data: { roles: ['Admin'] }
      },
      {
        path: 'admin/clients',
        component: ClientsComponent,
        canActivate: [roleGuard],
        data: { roles: ['Admin'] }
      },
      {
        path: 'admin/dashboard',
        component: DashboardComponent,
        canActivate: [roleGuard],
        data: { roles: ['Admin'] }
      },
      {
        path: 'mentor/collaboration-requests',
        component: CollaborationRequestsComponent,
        canActivate: [roleGuard],
        data: { roles: ['Mentor'] }
      },
      {
        path: 'mentor/create-plan',
        component: CreatePlanComponent,
        canActivate: [roleGuard],
        data: { roles: ['Mentor'] }
      },
      {
        path: 'mentor/published-plans',
        component: PublishedPlansComponent,
        canActivate: [roleGuard],
        data: { roles: ['Mentor'] }
      },
      {
        path: 'mentor/subscribers',
        component: SubscribersComponent,
        canActivate: [roleGuard],
        data: { roles: ['Mentor'] }
      },
      {
        path: '',
        pathMatch: 'full',
        redirectTo: 'admin/dashboard'
      }
    ]
  },
  {
    path: '**',
    redirectTo: ''
  }
];
