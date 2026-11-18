import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, debounceTime, map, of, startWith, switchMap } from 'rxjs';
import { Paged, ServiceSummary, ServicesApi } from '../../core/api/services.api';
import { I18nService } from '../../core/i18n/i18n.service';

export const CATEGORIES = [
  'IdentityAndCivilRegistration',
  'RevenueAndTax',
  'Immigration',
  'Education',
  'Healthcare',
  'BusinessRegistration',
  'Transportation',
  'LandAndProperty',
  'LocalGovernment',
] as const;

type DirectoryState =
  | { status: 'loading' }
  | { status: 'error' }
  | { status: 'ready'; data: Paged<ServiceSummary> };

@Component({
  selector: 'app-service-directory',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>{{ i18n.t('services.title') }}</h1>

    <div class="filters">
      <label for="search">{{ i18n.t('services.search') }}</label>
      <input
        id="search"
        type="search"
        autocomplete="off"
        maxlength="100"
        [placeholder]="i18n.t('services.searchPlaceholder')"
        [value]="search()"
        (input)="onSearch($any($event.target).value)"
      />

      <label for="category">{{ i18n.t('services.category') }}</label>
      <select id="category" [value]="category()" (change)="onCategory($any($event.target).value)">
        <option value="">{{ i18n.t('services.allCategories') }}</option>
        @for (item of categories; track item) {
          <option [value]="item" [selected]="item === category()">{{ i18n.t('category.' + item) }}</option>
        }
      </select>
    </div>

    <div aria-live="polite">
      @let current = state();
      @if (current.status === 'loading') {
        <p>{{ i18n.t('services.loading') }}</p>
      } @else if (current.status === 'error') {
        <p role="alert">{{ i18n.t('services.error') }}</p>
        <button type="button" (click)="retry()">{{ i18n.t('services.retry') }}</button>
      } @else if (current.status === 'ready') {
        @if (current.data.items.length === 0) {
          <p>{{ i18n.t('services.empty') }}</p>
        } @else {
          <ul class="grid">
            @for (item of current.data.items; track item.id) {
              <li class="card">
                <span class="badge" [class.available]="item.status === 'Available'">{{
                  i18n.t('status.' + item.status)
                }}</span>
                <h2>
                  <a [routerLink]="['/services', item.code]">{{ item.name }}</a>
                </h2>
                <p>{{ item.description }}</p>
                <p class="meta">{{ item.agencyName }}</p>
              </li>
            }
          </ul>

          @if (current.data.totalPages > 1) {
            <nav class="pager" [attr.aria-label]="i18n.t('services.pagination')">
              <button type="button" [disabled]="page() <= 1" (click)="go(page() - 1)">
                {{ i18n.t('services.prev') }}
              </button>
              <span>{{ i18n.t('services.page', { page: page(), total: current.data.totalPages }) }}</span>
              <button type="button" [disabled]="page() >= current.data.totalPages" (click)="go(page() + 1)">
                {{ i18n.t('services.next') }}
              </button>
            </nav>
          }
        }
      }
    </div>
  `,
})
export class ServiceDirectoryPage {
  private readonly api = inject(ServicesApi);
  protected readonly i18n = inject(I18nService);
  protected readonly categories = CATEGORIES;

  protected readonly search = signal('');
  protected readonly category = signal('');
  protected readonly page = signal(1);
  private readonly attempt = signal(0);

  private readonly query = computed(() => ({
    search: this.search().trim(),
    category: this.category(),
    page: this.page(),
    pageSize: 12,
    language: this.i18n.lang(),
    attempt: this.attempt(),
  }));

  protected readonly state = toSignal(
    toObservable(this.query).pipe(
      debounceTime(250),
      switchMap(({ attempt: _attempt, ...query }) =>
        this.api.list(query).pipe(
          map((data): DirectoryState => ({ status: 'ready', data })),
          startWith<DirectoryState>({ status: 'loading' }),
          catchError(() => of<DirectoryState>({ status: 'error' })),
        ),
      ),
    ),
    { initialValue: { status: 'loading' } as DirectoryState },
  );

  protected onSearch(value: string): void {
    this.search.set(value);
    this.page.set(1);
  }

  protected onCategory(value: string): void {
    this.category.set(value);
    this.page.set(1);
  }

  protected go(page: number): void {
    this.page.set(page);
  }

  protected retry(): void {
    this.attempt.update((n) => n + 1);
  }
}
