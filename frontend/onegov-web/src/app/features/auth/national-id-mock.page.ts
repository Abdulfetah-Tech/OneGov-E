import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { Router } from '@angular/router';
import { mockNationalIdCode } from '../../core/auth/auth.utils';
import { I18nService } from '../../core/i18n/i18n.service';

/**
 * Stands in for the national ID provider's own sign-in page. Development only: the API accepts the identity typed here
 * without any check. It is only useful when the API runs with NationalId:Provider=mock.
 */
@Component({
  selector: 'app-national-id-mock',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>{{ i18n.t('nationalId.mockTitle') }}</h1>
    <p class="notice" role="note">{{ i18n.t('nationalId.mockBanner') }}</p>

    <form class="form" (submit)="submit($event)" novalidate>
      <div class="field">
        <label for="idNumber">{{ i18n.t('nationalId.idNumber') }}</label>
        <input
          id="idNumber"
          name="idNumber"
          type="text"
          inputmode="numeric"
          maxlength="32"
          autocomplete="off"
          [value]="idNumber()"
          (input)="idNumber.set($any($event.target).value)"
        />
      </div>

      <div class="field">
        <label for="fullName">{{ i18n.t('auth.displayName') }}</label>
        <input
          id="fullName"
          name="fullName"
          type="text"
          maxlength="100"
          autocomplete="off"
          [value]="fullName()"
          (input)="fullName.set($any($event.target).value)"
        />
      </div>

      @if (error(); as message) {
        <p class="error" role="alert">{{ message }}</p>
      }

      <button type="submit" class="button">{{ i18n.t('nationalId.mockSubmit') }}</button>
    </form>
  `,
})
export class NationalIdMockPage {
  private readonly router = inject(Router);
  protected readonly i18n = inject(I18nService);

  /** Bound from the ?state= query parameter by the router. */
  readonly state = input<string | undefined>();

  protected readonly idNumber = signal('');
  protected readonly fullName = signal('');
  protected readonly error = signal<string | null>(null);

  protected async submit(event: Event): Promise<void> {
    event.preventDefault();
    const state = this.state();
    if (!state || !this.idNumber().trim() || !this.fullName().trim()) {
      this.error.set(this.i18n.t('error.required'));
      return;
    }

    // The real provider sends the browser back to the callback page with a code and the same state.
    const code = mockNationalIdCode(this.idNumber().trim(), this.fullName().trim());
    await this.router.navigate(['/auth/national-id/callback'], { queryParams: { code, state } });
  }
}
