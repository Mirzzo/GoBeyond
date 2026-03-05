import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AuthResponse, AuthUser } from '../models/user.model';

interface LoginRequest {
  email: string;
  password: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly accessTokenSignal = signal<string | null>(localStorage.getItem('gb_access_token'));
  private readonly userSignal = signal<AuthUser | null>(this.readUserFromStorage());

  readonly isAuthenticated = computed(() => !!this.accessTokenSignal());
  readonly user = computed(() => this.userSignal());

  login(request: LoginRequest) {
    return this.http.post<AuthResponse>('/api/auth/login', request);
  }

  applySession(response: AuthResponse): void {
    this.accessTokenSignal.set(response.accessToken);
    this.userSignal.set(response.user);

    localStorage.setItem('gb_access_token', response.accessToken);
    localStorage.setItem('gb_refresh_token', response.refreshToken);
    localStorage.setItem('gb_user', JSON.stringify(response.user));
  }

  getAccessToken(): string | null {
    return this.accessTokenSignal();
  }

  hasRole(roles: string[]): boolean {
    const user = this.userSignal();
    return !!user && roles.includes(user.role);
  }

  logout(): void {
    this.accessTokenSignal.set(null);
    this.userSignal.set(null);

    localStorage.removeItem('gb_access_token');
    localStorage.removeItem('gb_refresh_token');
    localStorage.removeItem('gb_user');

    this.router.navigateByUrl('/login');
  }

  private readUserFromStorage(): AuthUser | null {
    const rawValue = localStorage.getItem('gb_user');
    if (!rawValue) {
      return null;
    }

    try {
      return JSON.parse(rawValue) as AuthUser;
    } catch {
      return null;
    }
  }
}
