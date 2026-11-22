import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { errorKey } from '../../core/auth/auth.utils';
import { I18nService, LANG_LABELS, Lang, SUPPORTED_LANGS } from '../../core/i18n/i18n.service';

@Component({
  selector: 'app-register',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>{{ i18n.t('auth.register.title') }}</h1>

    <form class="form" (submit)="submit($event)" novalidate>
      <div class="field">
        <label for="displayName">{{ i18n.t('auth.displayName') }}</label>
        <input
          id="displayName"
          name="displayName"
          type="text"
          autocomplete="name"
          maxlength="100"
          required
          [value]="displayName()"
          (input)="displayName.set($any($event.target).value)"
        />
      </div>

      <div class="field">
        <label for="email">{{ i18n.t('auth.email') }}</label>
        <input
          id="email"
          name="email"
          type="email"
          autocomplete="email"
          maxlength="254"
          required
          [value]="email()"
          (input)="email.set($any($event.target).value)"
        />
      </div>

      <div class="field">
        <label for="password">{{ i18n.t('auth.password') }}</label>
        <p id="password-hint" class="hint">{{ i18n.t('auth.passwordHint') }}</p>
        <input
          id="password"
          name="password"
          type="password"
          autocomplete="new-password"
          maxlength="128"
          required
          aria-describedby="password-hint"
          [value]="password()"
          (input)="password.set($any($event.target).value)"
        />
      </div>

      <div class="field">
        <label for="confirm">{{ i18n.t('auth.confirmPassword') }}</label>
        <input
          id="confirm"
          name="confirm"
          type="password"
          autocomplete="new-password"
          maxlength="128"
          required
          [value]="confirm()"
          (input)="confirm.set($any($event.target).value)"
        />
      </div>

      <div class="field">
        <label for="language">{{ i18n.t('auth.language') }}</label>
        <select id="language" name="language" (change)="language.set($any($event.target).value)">
          @for (code of langs; track code) {
            <option [value]="code" [selected]="code === language()">{{ labels[code] }}</option>
          }
        </select>
      </div>

      @if (error(); as message) {
        <p class="error" role="alert">{{ message }}</p>
      }

      <button type="submit" class="button" [disabled]="busy()">{{ i18n.t('auth.register.submit') }}</button>
    </form>

    <p>
      {{ i18n.t('auth.haveAccount') }}
      <a routerLink="/sign-in">{{ i18n.t('auth.signIn.title') }}</a>
    </p>
  `,
})
export class RegisterPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  protected readonly i18n = inject(I18nService);
  protected readonly langs = SUPPORTED_LANGS;
  protected readonly labels = LANG_LABELS;

  protected readonly displayName = signal('');
  protected readonly email = signal('');
  protected readonly password = signal('');
  protected readonly confirm = signal('');
  protected readonly language = signal<Lang>(this.i18n.lang());
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected async submit(event: Event): Promise<void> {
    event.preventDefault();
    if (this.busy()) {
      return;
    }
    this.error.set(null);

    const email = this.email().trim();
    const displayName = this.displayName().trim();
    if (!displayName || !email || !this.password()) {
      this.error.set(this.i18n.t('error.required'));
      return;
    }
    if (this.password() !== this.confirm()) {
      this.error.set(this.i18n.t('error.passwordMismatch'));
      return;
    }

    this.busy.set(true);
    const created = await this.auth.register({
      email,
      password: this.password(),
      displayName,
      language: this.language(),
    });
    if (!created.ok) {
      this.busy.set(false);
      this.error.set(this.i18n.t(errorKey(created.code)));
      return;
    }

    // The account exists now, so sign straight in. If that fails, the sign-in page is the fallback.
    const outcome = await this.auth.login(email, this.password());
    this.busy.set(false);
    await this.router.navigateByUrl(outcome.kind === 'signedIn' ? '/account' : '/sign-in');
  }
}
