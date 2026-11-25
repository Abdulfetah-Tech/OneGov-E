import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { NotificationItem, NotificationsApi } from '../../core/api/notifications.api';
import { formatDualDate } from '../../core/format/dates';
import { I18nService } from '../../core/i18n/i18n.service';
import { NotificationsService } from '../../core/notifications/notifications.service';

const PAGE_SIZE = 10;

@Component({
  selector: 'app-notifications',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>{{ i18n.t('notifications.title') }}</h1>

    @switch (status()) {
      @case ('loading') {
        <p role="status">{{ i18n.t('applications.loading') }}</p>
      }
      @case ('error') {
        <p class="error" role="alert">{{ i18n.t('notifications.loadError') }}</p>
      }
      @case ('ready') {
        @if (items().length === 0) {
          <p>{{ i18n.t('notifications.empty') }}</p>
        } @else {
          @if (unread.unread() > 0) {
            <p>
              <button type="button" (click)="markAll()">{{ i18n.t('notifications.markAll') }}</button>
            </p>
          }

          <ul class="grid">
            @for (item of items(); track item.id) {
              <li class="card" [class.unread]="!item.isRead">
                @if (!item.isRead) {
                  <span class="badge available">{{ i18n.t('notifications.new') }}</span>
                }
                <h2>
                  {{ i18n.t('notifications.application') }} {{ item.referenceNumber ?? '' }}:
                  {{ i18n.t('applications.state.' + item.toState) }}
                </h2>
                @if (item.note) {
                  <p class="preserve">{{ item.note }}</p>
                }
                <p class="meta">{{ date(item.createdAt) }}</p>
                <button type="button" class="button" (click)="open(item)">{{ i18n.t('notifications.open') }}</button>
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
export class NotificationsPage implements OnInit {
  private readonly api = inject(NotificationsApi);
  private readonly router = inject(Router);
  protected readonly unread = inject(NotificationsService);
  protected readonly i18n = inject(I18nService);

  protected readonly status = signal<'loading' | 'ready' | 'error'>('loading');
  protected readonly items = signal<NotificationItem[]>([]);
  protected readonly page = signal(1);
  protected readonly totalPages = signal(1);

  ngOnInit(): void {
    void this.load();
  }

  protected date(iso: string): string {
    return formatDualDate(iso, (key) => this.i18n.t(key), this.i18n.lang());
  }

  protected go(page: number): void {
    this.page.set(page);
    void this.load();
  }

  protected async markAll(): Promise<void> {
    try {
      await this.unread.markAllRead();
      await this.load();
    } catch {
      this.status.set('error');
    }
  }

  protected async open(item: NotificationItem): Promise<void> {
    if (!item.isRead) {
      try {
        await this.unread.markRead(item.id);
      } catch {
        // Opening the application matters more than the read mark.
      }
    }
    await this.router.navigate(['/applications', item.applicationId]);
  }

  private async load(): Promise<void> {
    this.status.set('loading');
    try {
      const result = await firstValueFrom(this.api.list(this.page(), PAGE_SIZE));
      this.items.set(result.items);
      this.totalPages.set(Math.max(1, result.totalPages));
      this.status.set('ready');
      void this.unread.refresh();
    } catch {
      this.status.set('error');
    }
  }
}
