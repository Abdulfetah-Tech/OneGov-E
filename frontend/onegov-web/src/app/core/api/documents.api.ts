import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE } from './services.api';

export interface DocumentItem {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  uploadedAt: string;
}

@Injectable({ providedIn: 'root' })
export class DocumentsApi {
  private readonly http = inject(HttpClient);

  private url(applicationId: string): string {
    return `${API_BASE}/applications/${encodeURIComponent(applicationId)}/documents`;
  }

  list(applicationId: string): Observable<DocumentItem[]> {
    return this.http.get<DocumentItem[]>(this.url(applicationId));
  }

  upload(applicationId: string, file: File): Observable<DocumentItem> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<DocumentItem>(this.url(applicationId), form);
  }

  download(applicationId: string, documentId: string): Observable<Blob> {
    return this.http.get(`${this.url(applicationId)}/${encodeURIComponent(documentId)}`, { responseType: 'blob' });
  }

  remove(applicationId: string, documentId: string): Observable<void> {
    return this.http.delete<void>(`${this.url(applicationId)}/${encodeURIComponent(documentId)}`);
  }
}
