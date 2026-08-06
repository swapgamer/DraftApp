import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';
import { NotificationService } from '../../../core/notification.service';

function matchingPasswords(group: AbstractControl): ValidationErrors | null {
  return group.get('password')?.value === group.get('confirmPassword')?.value ? null : { passwordMismatch: true };
}

@Component({
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule],
  templateUrl: './register.component.html',
  styleUrl: './register.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RegisterComponent {
  readonly form = new FormGroup({
    displayName: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(120)] }),
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    password: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(12), Validators.pattern(/(?=.*[a-z])(?=.*[A-Z])(?=.*\d)/)] }),
    confirmPassword: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  }, { validators: matchingPasswords });
  readonly submitting = signal(false);
  readonly error = signal('');
  readonly hidePassword = signal(true);
  passwordStrength = 0;

  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly notifications = inject(NotificationService);

  updatePasswordStrength(): void {
    const password = this.form.controls.password.value;
    this.passwordStrength = [password.length >= 12, /[A-Z]/.test(password), /[a-z]/.test(password), /\d/.test(password)].filter(Boolean).length;
  }

  submit(): void {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      if (!this.submitting()) {
        const message = this.validationMessage();
        this.error.set(message);
        this.notifications.showError(message);
      }
      return;
    }
    this.error.set(''); this.submitting.set(true);
    const { confirmPassword: _confirmPassword, ...request } = this.form.getRawValue();
    this.auth.register(request).subscribe({
      next: () => this.router.navigateByUrl('/hall-of-fame'),
      error: (error: HttpErrorResponse) => {
        this.error.set(error.status === 409 ? 'An account with this email already exists.' : 'We could not create your account. Please review your details and try again.');
        this.submitting.set(false);
      },
    });
  }

  private validationMessage(): string {
    const controls = this.form.controls;
    if (controls.password.invalid) return 'Password must be at least 12 characters and include uppercase, lowercase, and a number.';
    if (controls.confirmPassword.invalid || this.form.hasError('passwordMismatch')) return 'Confirm password must match your password.';
    if (controls.email.invalid) return 'Enter a valid email address.';
    if (controls.displayName.invalid) return 'Enter a display name of up to 120 characters.';
    return 'Please correct the highlighted fields.';
  }
}
