import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { NotificationsApi } from '../api/notifications.api';
import { NotificationsService } from './notifications.service';

describe('NotificationsService', () => {
  let api: {
    unreadCount: ReturnType<typeof vi.fn>;
    markRead: ReturnType<typeof vi.fn>;
    markAllRead: ReturnType<typeof vi.fn>;
  };

  function create(): NotificationsService {
    TestBed.configureTestingModule({ providers: [{ provide: NotificationsApi, useValue: api as unknown as NotificationsApi }] });
    return TestBed.inject(NotificationsService);
  }

  beforeEach(() => {
    TestBed.resetTestingModule();
    api = {
      unreadCount: vi.fn(() => of({ count: 3 })),
      markRead: vi.fn(() => of(undefined)),
      markAllRead: vi.fn(() => of(undefined)),
    };
  });

  it('loads the unread count', async () => {
    const service = create();

    await service.refresh();

    expect(service.unread()).toBe(3);
  });

  it('keeps the last known count when the server cannot be reached', async () => {
    const service = create();
    await service.refresh();
    api.unreadCount.mockReturnValue(throwError(() => new Error('offline')));

    await service.refresh();

    expect(service.unread()).toBe(3);
  });

  it('refreshes the count after one notification is marked read', async () => {
    const service = create();
    await service.refresh();
    api.unreadCount.mockReturnValue(of({ count: 2 }));

    await service.markRead('abc');

    expect(api.markRead).toHaveBeenCalledWith('abc');
    expect(service.unread()).toBe(2);
  });

  it('clears the count when everything is marked read, and on reset', async () => {
    const service = create();
    await service.refresh();

    await service.markAllRead();
    expect(service.unread()).toBe(0);

    await service.refresh();
    service.reset();
    expect(service.unread()).toBe(0);
  });
});
