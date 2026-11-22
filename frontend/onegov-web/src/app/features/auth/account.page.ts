import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthApi } from '../../core/auth/auth.api';
import { MfaEnrollment } from '../../core/auth/auth.models';
import { AuthService } from '../../core/auth/auth.service';
import { errorKey, problemCode } from '../../core/auth/auth.utils';
import { I18nService } from '../../core/i18n/i18n.service';

type MfaStep = 'idle' | 'password' | 'scan' | 'recovery';

@Component({
  selector: 'app-account',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (auth.user(); as user) {
      <h1>{{ i18n.t('account.title') }}</h1>

      <section class="card" aria-labelledby="profile-heading">
        <h2 id="profile-heading">{{ user.displayName }}</h2>
        <dl class="details">
          <dt>{{ i18n.t('account.email') }}</dt>
          <dd>{{ hasRealEmail(user.email) ? user.email : i18n.t('nationalId.emailNone') }}</dd>
          <dt>{{ i18n.t('account.roles') }}</dt>
          <dd>
            @for (role of user.roles; track role) {
              <span class="badge available">{{ roleLabel(role) }}</span>
            }
          </dd>
        </dl>
        <button type="button" (click)="signOut()">{{ i18n.t('auth.signOut') }}</button>
      </section>

      <section class="card" aria-labelledby="mfa-heading">
        <h2 id="mfa-heading">{{ i18n.t('account.mfaTitle') }}</h2>

        @if (user.mfaEnabled && step() !== 'recovery') {
          <p>{{ i18n.t('account.mfaOn') }}</p>
        }

        @if (!user.mfaEnabled && step() === 'idle') {
          <p>{{ i18n.t('account.mfaOff') }}</p>
          @if (isStaff()) {
            <p class="notice" role="note">{{ i18n.t('account.mfaStaffWarning') }}</p>
          }
          <button type="button" class="button" (click)="startMfa()">{{ i18n.t('account.mfaStart') }}</button>
        }

        @if (step() === 'password') {
          <form class="form" (submit)="submitPassword($event)" novalidate>
            <div class="field">
              <label for="mfa-password">{{ i18n.t('account.mfaPasswordPrompt') }}</label>
              <input
                id="mfa-password"
                type="password"
                autocomplete="current-password"
                maxlength="128"
                [value]="password()"
                (input)="password.set($any($event.target).value)"
              />
            </div>
            @if (error(); as message) {
              <p class="error" role="alert">{{ message }}</p>
            }
            <button type="submit" class="button" [disabled]="busy()">{{ i18n.t('account.mfaContinue') }}</button>
          </form>
        }

        @if (step() === 'scan') {
          <h3>{{ i18n.t('account.mfaScanTitle') }}</h3>
          <p>{{ i18n.t('account.mfaKeyHelp') }}</p>
          <p>
            <strong>{{ i18n.t('account.mfaKeyLabel') }}:</strong>
            <code class="key" translate="no">{{ groupedKey() }}</code>
          </p>
          <form class="form" (submit)="submitCode($event)" novalidate>
            <div class="field">
              <label for="mfa-code">{{ i18n.t('account.mfaCodePrompt') }}</label>
              <input
                id="mfa-code"
                type="text"
                inputmode="numeric"
                autocomplete="one-time-code"
                maxlength="16"
                [value]="code()"
                (input)="code.set($any($event.target).value)"
              />
            </div>
            @if (error(); as message) {
              <p class="error" role="alert">{{ message }}</p>
            }
            <button type="submit" class="button" [disabled]="busy()">{{ i18n.t('account.mfaConfirm') }}</button>
          </form>
        }

        @if (step() === 'recovery') {
          <h3>{{ i18n.t('account.mfaRecoveryTitle') }}</h3>
          <p class="notice" role="note">{{ i18n.t('account.mfaRecoveryWarning') }}</p>
          <ul class="codes" translate="no">
            @for (item of recoveryCodes(); track item) {
              <li>
                <code>{{ item }}</code>
              </li>
            }
          </ul>
          <button type="button" class="button" (click)="finishMfa()">{{ i18n.t('account.mfaDone') }}</button>
        }
      </section>
    }
  `,
})
export class AccountPage {
  private readonly api = inject(AuthApi);
  private readonly router = inject(Router);
  protected readonly auth = inject(AuthService);
  protected readonly i18n = inject(I18nService);

  protected readonly step = signal<MfaStep>('idle');
  protected readonly password = signal('');
  protected readonly code = signal('');
  protected readonly enrollment = signal<MfaEnrollment | null>(null);
  protected readonly recoveryCodes = signal<string[]>([]);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly isStaff = computed(() => (this.auth.user()?.roles ?? []).some((role) => role !== 'citizen'));

  /** The setup key in groups of four, which is easier to type by hand. */
  protected readonly groupedKey = computed(() =>
    (this.enrollment()?.sharedKey ?? '').replace(/(.{4})/g, '$1 ').trim(),
  );

  /** National ID accounts start with a placeholder address that ends in .invalid. */
  protected hasRealEmail(email: string): boolean {
    return !email.endsWith('.invalid');
  }

  protected roleLabel(role: string): string {
    const key = `role.${role}`;
    const label = this.i18n.t(key);
    return label === key ? role : label;
  }

  protected startMfa(): void {
    this.error.set(null);
    this.password.set('');
    this.step.set('password');
  }

  protected async submitPassword(event: Event): Promise<void> {
    event.preventDefault();
    if (this.busy()) {
      return;
    }
    if (!this.password()) {
      this.error.set(this.i18n.t('error.required'));
      return;
    }

    this.busy.set(true);
    this.error.set(null);
    try {
      this.enrollment.set(await firstValueFrom(this.api.beginMfa(this.password())));
      this.password.set('');
      this.code.set('');
      this.step.set('scan');
    } catch (error) {
      this.error.set(this.i18n.t(errorKey(problemCode(error))));
    } finally {
      this.busy.set(false);
    }
  }

  protected async submitCode(event: Event): Promise<void> {
    event.preventDefault();
    if (this.busy()) {
      return;
    }
    if (!this.code().trim()) {
      this.error.set(this.i18n.t('error.required'));
      return;
    }

    this.busy.set(true);
    this.error.set(null);
    try {
      const confirmed = await firstValueFrom(this.api.confirmMfa(this.code().trim()));
      this.recoveryCodes.set(confirmed.recoveryCodes);
      this.enrollment.set(null);
      this.step.set('recovery');
      await this.auth.reloadProfile();
    } catch (error) {
      this.error.set(this.i18n.t(errorKey(problemCode(error))));
    } finally {
      this.busy.set(false);
    }
  }

  protected finishMfa(): void {
    this.recoveryCodes.set([]);
    this.step.set('idle');
  }

  protected async signOut(): Promise<void> {
    await this.auth.logout();
    await this.router.navigateByUrl('/');
  }
}
