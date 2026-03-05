export type AppRole = 'Admin' | 'Mentor' | 'Client';

export interface AuthUser {
  id: number;
  name: string;
  email: string;
  role: AppRole;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  user: AuthUser;
}
