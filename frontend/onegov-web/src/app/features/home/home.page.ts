import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { todayEthiopian } from '../../core/calendar/ethiopian-date';
import { I18nService } from '../../core/i18n/i18n.service';

@Component({
  selector: 'app-home',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="hero">
      <h1>{{ i18n.t('home.title') }}</h1>
      <p class="lead">{{ i18n.t('home.lead') }}</p>
      <a class="button" routerLink="/services">{{ i18n.t('home.cta') }}</a>
    </section>

    <section class="card" aria-labelledby="today-heading">
      <h2 id="today-heading">{{ i18n.t('home.today') }}</h2>
      <p>
        <strong>{{ i18n.t('home.ethiopianCalendar') }}:</strong> {{ ethiopian() }}
      </p>
      <p>
        <strong>{{ i18n.t('home.gregorianCalendar') }}:</strong> {{ gregorian() }}
      </p>
    </section>
  `,
})
export class HomePage {
  protected readonly i18n = inject(I18nService);

  protected readonly ethiopian = computed(() => {
    const date = todayEthiopian();
    return `${date.day} ${this.i18n.t('calendar.month.' + date.month)} ${date.year} ${this.i18n.t('calendar.era')}`;
  });

  protected readonly gregorian = computed(() =>
    new Intl.DateTimeFormat(this.i18n.lang(), { dateStyle: 'long' }).format(new Date()),
  );
}
