import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { errorKey, safeReturnUrl } from '../../core/auth/auth.utils';
import { I18nService } from '../../core/i18n/i18n.service';

@Component({
  selector: 'app-sign-in',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>{{ i18n.t('auth.signIn.title') }}</h1>

    <form class="form" (submit)="submit($event)" novalidate>
      <div class="field">
        <label for="email">{{ i18n.t('auth.email') }}</label>
        <input
          id="email"
          name="email"
          type="email"
          autocomplete="username"
          maxlength="254"
          required
          [value]="email()"
          (input)="email.set($any($event.target).value)"
        />
      </div>

      <div class="field">
        <label for="password">{{ i18n.t('auth.password') }}</label>
        <input
          id="password"
          name="password"
          type="password"
          autocomplete="current-password"
          maxlength="128"
          required
          [value]="password()"
          (input)="password.set($any($event.target).value)"
        />
      </div>

      @if (needsCode()) {
        <div class="field">
          <label for="otp">{{ i18n.t('auth.otp') }}</label>
          <p id="otp-hint" class="hint">{{ i18n.t('auth.otpHint') }}</p>
          <input
            id="otp"
            name="otp"
            type="text"
            inputmode="numeric"
            autocomplete="one-time-code"
            maxlength="32"
            aria-describedby="otp-hint"
            [value]="otp()"
            (input)="otp.set($any($event.target).value)"
          />
        </div>
      }

      @if (error(); as message) {
        <p class="error" role="alert">{{ message }}</p>
      }

      <button type="submit" class="button" [disabled]="busy()">{{ i18n.t('auth.signIn.submit') }}</button>
    </form>

    <p>
      {{ i18n.t('auth.noAccount') }}
      <a routerLink="/register">{{ i18n.t('auth.register.title') }}</a>
    </p>
  `,
})
export class SignInPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  protected readonly i18n = inject(I18nService);

  /** Bound from the ?returnUrl= query parameter by the router. */
  readonly returnUrl = input<string | undefined>();

  protected readonly email = signal('');
  protected readonly password = signal('');
  protected readonly otp = signal('');
  protected readonly needsCode = signal(false);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected async submit(event: Event): Promise<void> {
    event.preventDefault();
    if (this.busy()) {
      return;
    }
    this.error.set(null);

    if (!this.email().trim() || !this.password()) {
      this.error.set(this.i18n.t('error.required'));
      return;
    }

    this.busy.set(true);
    const outcome = await this.auth.login(
      this.email().trim(),
      this.password(),
      this.needsCode() ? this.otp().trim() : undefined,
    );
    this.busy.set(false);

    switch (outcome.kind) {
      case 'signedIn':
        await this.router.navigateByUrl(safeReturnUrl(this.returnUrl()));
        break;
      case 'mfaRequired':
        this.needsCode.set(true);
        break;
      case 'error':
        this.error.set(this.i18n.t(errorKey(outcome.code)));
        break;
    }
  }
}
