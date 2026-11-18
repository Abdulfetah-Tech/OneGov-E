import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, map, of, startWith, switchMap } from 'rxjs';
import { ServiceDetail, ServicesApi } from '../../core/api/services.api';
import { I18nService } from '../../core/i18n/i18n.service';

type DetailState =
  | { status: 'loading' }
  | { status: 'notFound' }
  | { status: 'error' }
  | { status: 'ready'; service: ServiceDetail };

const KINDS = ['Eligibility', 'Document', 'Step'] as const;

@Component({
  selector: 'app-service-detail',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <a routerLink="/services">&larr; {{ i18n.t('service.back') }}</a>

    @let current = state();
    @if (current.status === 'loading') {
      <p>{{ i18n.t('services.loading') }}</p>
    } @else if (current.status === 'notFound') {
      <p role="alert">{{ i18n.t('service.notFound') }}</p>
    } @else if (current.status === 'error') {
      <p role="alert">{{ i18n.t('services.error') }}</p>
    } @else if (current.status === 'ready') {
      <article>
        <span class="badge" [class.available]="current.service.status === 'Available'">{{
          i18n.t('status.' + current.service.status)
        }}</span>
        <h1>{{ current.service.name }}</h1>
        <p class="lead">{{ current.service.description }}</p>
        <p class="meta">
          <strong>{{ i18n.t('service.agency') }}:</strong> {{ current.service.agencyName }}
        </p>

        @if (current.service.status === 'Planned') {
          <p class="notice" role="note">{{ i18n.t('service.plannedNotice') }}</p>
        } @else {
          <p class="notice" role="note">{{ i18n.t('service.applyNotYet') }}</p>
        }

        @for (kind of kinds; track kind) {
          @if (byKind(current.service, kind).length > 0) {
            <h2>{{ i18n.t('kind.' + kind) }}</h2>
            <ul>
              @for (item of byKind(current.service, kind); track item.order) {
                <li>{{ item.text }}</li>
              }
            </ul>
          }
        }
      </article>
    }
  `,
})
export class ServiceDetailPage {
  private readonly api = inject(ServicesApi);
  protected readonly i18n = inject(I18nService);
  protected readonly kinds = KINDS;

  /** Bound from the route parameter :code (withComponentInputBinding). */
  readonly code = input.required<string>();

  private readonly request = computed(() => ({ code: this.code(), language: this.i18n.lang() }));

  protected readonly state = toSignal(
    toObservable(this.request).pipe(
      switchMap(({ code, language }) =>
        this.api.get(code, language).pipe(
          map((service): DetailState => ({ status: 'ready', service })),
          startWith<DetailState>({ status: 'loading' }),
          catchError((error: { status?: number }) =>
            of<DetailState>({ status: error.status === 404 ? 'notFound' : 'error' }),
          ),
        ),
      ),
    ),
    { initialValue: { status: 'loading' } as DetailState },
  );

  protected byKind(service: ServiceDetail, kind: string) {
    return service.requirements.filter((r) => r.kind === kind);
  }
}
