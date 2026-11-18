import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { I18nService } from '../core/i18n/i18n.service';

@Component({
  selector: 'app-not-found',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>{{ i18n.t('notFound.title') }}</h1>
    <p>{{ i18n.t('notFound.body') }}</p>
    <a routerLink="/">{{ i18n.t('nav.home') }}</a>
  `,
})
export class NotFoundPage {
  protected readonly i18n = inject(I18nService);
}
