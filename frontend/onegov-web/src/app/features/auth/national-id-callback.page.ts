import { ChangeDetectionStrategy, Component, OnInit, inject, input, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { errorKey } from '../../core/auth/auth.utils';
import { I18nService } from '../../core/i18n/i18n.service';

/** Where the national ID provider sends the browser back to, with a code and the state we sent. */
@Component({
  selector: 'app-national-id-callback',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (error(); as message) {
      <h1>{{ i18n.t('auth.signIn.title') }}</h1>
      <p class="error" role="alert">{{ message }}</p>
      <a routerLink="/sign-in">{{ i18n.t('nationalId.back') }}</a>
    } @else {
      <p role="status">{{ i18n.t('nationalId.working') }}</p>
    }
  `,
})
export class NationalIdCallbackPage implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  protected readonly i18n = inject(I18nService);

  /** Bound from the query string by the router. */
  readonly code = input<string | undefined>();
  readonly state = input<string | undefined>();

  protected readonly error = signal<string | null>(null);

  ngOnInit(): void {
    void this.complete();
  }

  private async complete(): Promise<void> {
    const code = this.code();
    const state = this.state();
    if (!code || !state) {
      this.error.set(this.i18n.t('error.invalid_request'));
      return;
    }

    const outcome = await this.auth.completeNationalId(code, state);
    if (outcome.kind === 'signedIn') {
      await this.router.navigateByUrl('/account', { replaceUrl: true });
      return;
    }
    this.error.set(this.i18n.t(outcome.kind === 'error' ? errorKey(outcome.code) : 'error.unknown'));
  }
}
