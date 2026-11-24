import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE, Paged } from './services.api';

export interface ApplicationSummary {
  id: string;
  referenceNumber: string | null;
  serviceCode: string;
  state: string;
  createdAt: string;
  updatedAt: string;
}

export interface HistoryEntry {
  transition: string;
  fromState: string;
  toState: string;
  actorId: string;
  note: string | null;
  occurredAt: string;
}

export interface ApplicationDetail extends ApplicationSummary {
  payload: Record<string, unknown>;
  isEditable: boolean;
  availableTransitions: string[];
  history: HistoryEntry[];
}

export interface ListApplicationsParams {
  state: string;
  page: number;
  pageSize: number;
}

@Injectable({ providedIn: 'root' })
export class ApplicationsApi {
  private readonly http = inject(HttpClient);

  list(query: ListApplicationsParams): Observable<Paged<ApplicationSummary>> {
    let params = new HttpParams().set('page', query.page).set('pageSize', query.pageSize);
    if (query.state) {
      params = params.set('state', query.state);
    }
    return this.http.get<Paged<ApplicationSummary>>(`${API_BASE}/applications`, { params });
  }

  get(id: string): Observable<ApplicationDetail> {
    return this.http.get<ApplicationDetail>(`${API_BASE}/applications/${encodeURIComponent(id)}`);
  }

  create(serviceCode: string): Observable<ApplicationDetail> {
    return this.http.post<ApplicationDetail>(`${API_BASE}/applications`, { serviceCode });
  }

  savePayload(id: string, payload: Record<string, unknown>): Observable<ApplicationDetail> {
    return this.http.put<ApplicationDetail>(`${API_BASE}/applications/${encodeURIComponent(id)}/payload`, { payload });
  }

  transition(id: string, transition: string, note: string | null): Observable<ApplicationDetail> {
    return this.http.post<ApplicationDetail>(`${API_BASE}/applications/${encodeURIComponent(id)}/transitions`, {
      transition,
      note,
    });
  }
}
