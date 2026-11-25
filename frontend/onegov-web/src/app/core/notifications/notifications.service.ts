import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { NotificationsApi } from '../api/notifications.api';

/** Keeps the unread count shown on the bell. Failures keep the last known count, so a brief network problem never shows a wrong zero. */
@Injectable({ providedIn: 'root' })
export class NotificationsService {
  private readonly api = inject(NotificationsApi);

  readonly unread = signal(0);

  async refresh(): Promise<void> {
    try {
      this.unread.set((await firstValueFrom(this.api.unreadCount())).count);
    } catch {
      // Keep the last known count.
    }
  }

  reset(): void {
    this.unread.set(0);
  }

  async markRead(id: string): Promise<void> {
    await firstValueFrom(this.api.markRead(id));
    await this.refresh();
  }

  async markAllRead(): Promise<void> {
    await firstValueFrom(this.api.markAllRead());
    this.unread.set(0);
  }
}
