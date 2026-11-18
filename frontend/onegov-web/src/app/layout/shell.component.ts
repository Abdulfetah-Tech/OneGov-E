import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { I18nService, LANG_LABELS, Lang, SUPPORTED_LANGS } from '../core/i18n/i18n.service';

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <a class="skip-link" href="#main">{{ i18n.t('nav.skip') }}</a>

    <p class="notice" role="note">{{ i18n.t('app.prototypeNotice') }}</p>

    <header class="header">
      <a class="brand" routerLink="/" aria-label="OneGov">OneGov</a>

      <nav aria-label="Main">
        <a routerLink="/" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }">{{
          i18n.t('nav.home')
        }}</a>
        <a routerLink="/services" routerLinkActive="active">{{ i18n.t('nav.services') }}</a>
      </nav>

      <div class="languages" role="group" [attr.aria-label]="i18n.t('lang.label')">
        @for (code of langs; track code) {
          <button
            type="button"
            [attr.lang]="code"
            [attr.aria-pressed]="i18n.lang() === code"
            (click)="choose(code)"
          >
            {{ labels[code] }}
          </button>
        }
      </div>
    </header>

    <main id="main" tabindex="-1">
      <router-outlet />
    </main>

    <footer class="footer">{{ i18n.t('app.tagline') }}</footer>
  `,
})
export class ShellComponent {
  protected readonly i18n = inject(I18nService);
  protected readonly langs = SUPPORTED_LANGS;
  protected readonly labels = LANG_LABELS;

  protected choose(lang: Lang): void {
    void this.i18n.use(lang);
  }
}
