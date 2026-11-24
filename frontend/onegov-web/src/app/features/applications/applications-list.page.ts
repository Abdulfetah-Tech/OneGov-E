import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApplicationSummary, ApplicationsApi } from '../../core/api/applications.api';
import { formatDualDate } from '../../core/format/dates';
import { I18nService } from '../../core/i18n/i18n.service';

const STATES = [
  'draft',
  'submitted',
  'under_review',
  'documents_requested',
  'identity_verified',
  'approved',
  'rejected',
  'withdrawn',
] as const;

const PAGE_SIZE = 10;

@Component({
  selector: 'app-applications-list',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>{{ i18n.t('applications.title') }}</h1>

    <div class="filters">
      <label for="state-filter">{{ i18n.t('applications.filterState') }}</label>
      <select id="state-filter" (change)="setState($any($event.target).value)">
        <option value="" [selected]="stateFilter() === ''">{{ i18n.t('applications.allStates') }}</option>
        @for (state of states; track state) {
          <option [value]="state" [selected]="stateFilter() === state">
            {{ i18n.t('applications.state.' + state) }}
          </option>
        }
      </select>
    </div>

    @switch (status()) {
      @case ('loading') {
        <p role="status">{{ i18n.t('applications.loading') }}</p>
      }
      @case ('error') {
        <p class="error" role="alert">{{ i18n.t('applications.loadError') }}</p>
      }
      @case ('ready') {
        @if (items().length === 0) {
          <p>{{ i18n.t('applications.empty') }}</p>
          <a class="button" routerLink="/services">{{ i18n.t('applications.browse') }}</a>
        } @else {
          <ul class="grid">
            @for (item of items(); track item.id) {
              <li class="card">
                <span class="badge">{{ i18n.t('applications.state.' + item.state) }}</span>
                <h2>
                  <a [routerLink]="['/applications', item.id]">{{
                    item.referenceNumber ?? i18n.t('applications.draft')
                  }}</a>
                </h2>
                <p class="meta">
                  <strong>{{ i18n.t('applications.service') }}:</strong> {{ item.serviceCode }}<br />
                  <strong>{{ i18n.t('applications.updated') }}:</strong> {{ date(item.updatedAt) }}
                </p>
              </li>
            }
          </ul>

          @if (totalPages() > 1) {
            <nav class="pager" [attr.aria-label]="i18n.t('applications.page')">
              <button type="button" [disabled]="page() <= 1" (click)="go(page() - 1)">
                {{ i18n.t('applications.previous') }}
              </button>
              <span>{{ i18n.t('applications.page') }} {{ page() }} / {{ totalPages() }}</span>
              <button type="button" [disabled]="page() >= totalPages()" (click)="go(page() + 1)">
                {{ i18n.t('applications.next') }}
              </button>
            </nav>
          }
        }
      }
    }
  `,
})
export class ApplicationsListPage implements OnInit {
  private readonly api = inject(ApplicationsApi);
  protected readonly i18n = inject(I18nService);
  protected readonly states = STATES;

  protected readonly status = signal<'loading' | 'ready' | 'error'>('loading');
  protected readonly items = signal<ApplicationSummary[]>([]);
  protected readonly page = signal(1);
  protected readonly totalPages = signal(1);
  protected readonly stateFilter = signal('');

  ngOnInit(): void {
    void this.load();
  }

  protected setState(value: string): void {
    this.stateFilter.set(value);
    this.page.set(1);
    void this.load();
  }

  protected go(page: number): void {
    this.page.set(page);
    void this.load();
  }

  protected date(iso: string): string {
    return formatDualDate(iso, (key) => this.i18n.t(key), this.i18n.lang());
  }

  private async load(): Promise<void> {
    this.status.set('loading');
    try {
      const result = await firstValueFrom(
        this.api.list({ state: this.stateFilter(), page: this.page(), pageSize: PAGE_SIZE }),
      );
      this.items.set(result.items);
      this.totalPages.set(Math.max(1, result.totalPages));
      this.status.set('ready');
    } catch {
      this.status.set('error');
    }
  }
}
