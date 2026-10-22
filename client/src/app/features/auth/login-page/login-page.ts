import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { toApiError } from '../../../core/api/api-error';
import { AuthService } from '../../../core/auth/auth-service';

@Component({
  selector: 'app-login-page',
  imports: [ReactiveFormsModule],
  templateUrl: './login-page.html',
  styleUrl: './login-page.css',
})
export class LoginPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  // Reactive form: same rules as the API's LoginCommand validator (required email + password)
  protected readonly form = inject(FormBuilder).nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);

  protected submit(): void {
    if (this.form.invalid) {
      // Show the field messages instead of calling the API with data it will reject
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.error.set(null);
    const { email, password } = this.form.getRawValue();

    this.auth.login(email, password).subscribe({
      next: () => {
        // Return to the page that sent the user here (expired token), otherwise start at gate-in
        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') || '/gate-in';
        void this.router.navigateByUrl(returnUrl);
      },
      error: (err: unknown) => {
        this.error.set(toApiError(err).message);
        this.submitting.set(false);
      },
    });
  }

  // True when a control was touched and is invalid, used for the red border + message
  protected showError(name: 'email' | 'password'): boolean {
    const control = this.form.controls[name];
    return control.invalid && control.touched;
  }
}
