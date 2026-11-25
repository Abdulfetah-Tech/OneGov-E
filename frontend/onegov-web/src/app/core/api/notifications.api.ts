import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE, Paged } from './services.api';

export interface NotificationItem {
  id: string;
  applicationId: string;
  referenceNumber: string | null;
  transition: string;
  toState: string;
  note: string | null;
  createdAt: string;
  isRead: boolean;
}

@Injectable({ providedIn: 'root' })
export class NotificationsApi {
  private readonly http = inject(HttpClient);

  list(page: number, pageSize: number): Observable<Paged<NotificationItem>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<Paged<NotificationItem>>(`${API_BASE}/notifications`, { params });
  }

  unreadCount(): Observable<{ count: number }> {
    return this.http.get<{ count: number }>(`${API_BASE}/notifications/unread-count`);
  }

  markRead(id: string): Observable<void> {
    return this.http.post<void>(`${API_BASE}/notifications/${encodeURIComponent(id)}/read`, {});
  }

  markAllRead(): Observable<void> {
    return this.http.post<void>(`${API_BASE}/notifications/read-all`, {});
  }
}
