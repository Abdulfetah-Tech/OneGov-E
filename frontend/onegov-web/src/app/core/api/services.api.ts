import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Lang } from '../i18n/i18n.service';

export const API_BASE = '/api/v1';

export interface ServiceSummary {
  id: string;
  code: string;
  name: string;
  description: string;
  category: string;
  status: string;
  agencyCode: string;
  agencyName: string;
}

export interface Requirement {
  kind: string;
  text: string;
  order: number;
}

export interface ServiceDetail extends ServiceSummary {
  requirements: Requirement[];
}

export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface ListServicesParams {
  search: string;
  category: string;
  page: number;
  pageSize: number;
  language: Lang;
}

@Injectable({ providedIn: 'root' })
export class ServicesApi {
  private readonly http = inject(HttpClient);

  list(query: ListServicesParams): Observable<Paged<ServiceSummary>> {
    let params = new HttpParams()
      .set('language', query.language)
      .set('page', query.page)
      .set('pageSize', query.pageSize);
    if (query.search) {
      params = params.set('search', query.search);
    }
    if (query.category) {
      params = params.set('category', query.category);
    }
    return this.http.get<Paged<ServiceSummary>>(`${API_BASE}/services`, { params });
  }

  get(code: string, language: Lang): Observable<ServiceDetail> {
    return this.http.get<ServiceDetail>(`${API_BASE}/services/${encodeURIComponent(code)}`, {
      params: new HttpParams().set('language', language),
    });
  }
}
