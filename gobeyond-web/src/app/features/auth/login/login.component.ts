import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss'
})
export class LoginComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  readonly form = new FormGroup({
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    password: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(6)] })
  });

  readonly errorMessage = signal<string | null>(null);
  readonly isSubmitting = signal(false);

  submit(): void {
    if (this.form.invalid || this.isSubmitting()) {
      this.form.markAllAsTouched();
      return;
    }

    this.errorMessage.set(null);
    this.isSubmitting.set(true);

    this.authService.login(this.form.getRawValue()).subscribe({
      next: response => {
        this.authService.applySession(response);
        this.redirectByRole();
      },
      error: (error: HttpErrorResponse) => {
        this.errorMessage.set(error.error?.message ?? 'Login failed.');
        this.isSubmitting.set(false);
      },
      complete: () => this.isSubmitting.set(false)
    });
  }

  private redirectByRole(): void {
    const role = this.authService.user()?.role;
    if (role === 'Admin') {
      this.router.navigateByUrl('/admin/dashboard');
      return;
    }

    if (role === 'Mentor') {
      this.router.navigateByUrl('/mentor/collaboration-requests');
      return;
    }

    this.router.navigateByUrl('/');
  }
}
