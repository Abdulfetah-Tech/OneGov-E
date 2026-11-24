import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApplicationDetail, ApplicationsApi } from '../../core/api/applications.api';
import { errorKey, problemCode } from '../../core/auth/auth.utils';
import { formatDualDate } from '../../core/format/dates';
import { I18nService } from '../../core/i18n/i18n.service';

/** Actions taken by staff, which can come with a note. */
const STAFF_ACTIONS = ['start_review', 'request_documents', 'verify_identity', 'approve', 'reject'];

/** Applicant actions that should save the form first. */
const SAVES_FIRST = ['submit', 'provide_documents'];

@Component({
  selector: 'app-application-detail',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <a routerLink="/applications">&larr; {{ i18n.t('applications.nav') }}</a>

    @switch (view()) {
      @case ('loading') {
        <p role="status">{{ i18n.t('applications.loading') }}</p>
      }
      @case ('notFound') {
        <p class="error" role="alert">{{ i18n.t('applications.notFound') }}</p>
      }
      @case ('error') {
        <p class="error" role="alert">{{ i18n.t('applications.loadError') }}</p>
      }
      @case ('ready') {
        @if (application(); as app) {
          <article>
            <span class="badge">{{ i18n.t('applications.state.' + app.state) }}</span>
            <h1>{{ app.referenceNumber ?? i18n.t('applications.draft') }}</h1>
            <p class="meta">
              <strong>{{ i18n.t('applications.service') }}:</strong> {{ app.serviceCode }}<br />
              <strong>{{ i18n.t('applications.updated') }}:</strong> {{ date(app.updatedAt) }}
            </p>

            <section class="card" aria-labelledby="form-heading">
              <h2 id="form-heading">{{ i18n.t('applications.formTitle') }}</h2>
              @if (app.isEditable) {
                <div class="form">
                  <div class="field">
                    <label for="details">{{ i18n.t('applications.detailsLabel') }}</label>
                    <p id="details-hint" class="hint">{{ i18n.t('applications.detailsHint') }}</p>
                    <textarea
                      id="details"
                      rows="6"
                      maxlength="2000"
                      aria-describedby="details-hint"
                      [value]="details()"
                      (input)="details.set($any($event.target).value)"
                    ></textarea>
                  </div>
                  <button type="button" class="button" [disabled]="busy() || !dirty()" (click)="save()">
                    {{ i18n.t('applications.save') }}
                  </button>
                </div>
              } @else {
                <p class="hint">{{ i18n.t('applications.locked') }}</p>
                @if (savedDetails()) {
                  <p class="preserve">{{ savedDetails() }}</p>
                }
              }
            </section>

            @if (message(); as text) {
              <p class="notice" role="status">{{ text }}</p>
            }
            @if (error(); as text) {
              <p class="error" role="alert">{{ text }}</p>
            }

            @if (app.availableTransitions.length > 0) {
              <section class="card" aria-labelledby="actions-heading">
                <h2 id="actions-heading">{{ i18n.t('applications.actionsTitle') }}</h2>
                @if (showNote()) {
                  <div class="form">
                    <div class="field">
                      <label for="note">{{ i18n.t('applications.noteLabel') }}</label>
                      <p id="note-hint" class="hint">{{ i18n.t('applications.noteHint') }}</p>
                      <textarea
                        id="note"
                        rows="3"
                        maxlength="2000"
                        aria-describedby="note-hint"
                        [value]="note()"
                        (input)="note.set($any($event.target).value)"
                      ></textarea>
                    </div>
                  </div>
                }
                <div class="actions">
                  @for (name of app.availableTransitions; track name) {
                    <button type="button" class="button" [disabled]="busy()" (click)="run(name)">
                      {{ i18n.t('applications.transition.' + name) }}
                    </button>
                  }
                </div>
              </section>
            }

            <section class="card" aria-labelledby="history-heading">
              <h2 id="history-heading">{{ i18n.t('applications.historyTitle') }}</h2>
              @if (app.history.length === 0) {
                <p>{{ i18n.t('applications.noHistory') }}</p>
              } @else {
                <ol class="history">
                  @for (entry of app.history; track $index) {
                    <li>
                      <strong>{{ i18n.t('applications.transition.' + entry.transition) }}</strong>
                      <span class="hint"> &rarr; {{ i18n.t('applications.state.' + entry.toState) }}</span>
                      <br />
                      <span class="hint">{{ date(entry.occurredAt) }}</span>
                      @if (entry.note) {
                        <p class="preserve">{{ entry.note }}</p>
                      }
                    </li>
                  }
                </ol>
              }
            </section>
          </article>
        }
      }
    }
  `,
})
export class ApplicationDetailPage {
  private readonly api = inject(ApplicationsApi);
  protected readonly i18n = inject(I18nService);

  /** Bound from the route parameter :id by the router. */
  readonly id = input.required<string>();

  protected readonly view = signal<'loading' | 'notFound' | 'error' | 'ready'>('loading');
  protected readonly application = signal<ApplicationDetail | null>(null);
  protected readonly details = signal('');
  protected readonly savedDetails = signal('');
  protected readonly note = signal('');
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly message = signal<string | null>(null);

  protected readonly dirty = computed(() => this.details() !== this.savedDetails());
  protected readonly showNote = computed(() =>
    (this.application()?.availableTransitions ?? []).some((name) => STAFF_ACTIONS.includes(name)),
  );

  constructor() {
    effect(() => {
      const id = this.id();
      untracked(() => void this.load(id));
    });
  }

  protected date(iso: string): string {
    return formatDualDate(iso, (key) => this.i18n.t(key), this.i18n.lang());
  }

  protected async save(): Promise<void> {
    if (this.busy()) {
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    this.message.set(null);
    try {
      await this.persistDetails();
      this.message.set(this.i18n.t('applications.saved'));
    } catch (error) {
      this.error.set(this.i18n.t(errorKey(problemCode(error))));
    } finally {
      this.busy.set(false);
    }
  }

  protected async run(name: string): Promise<void> {
    const current = this.application();
    if (!current || this.busy()) {
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    this.message.set(null);
    try {
      // What the applicant typed is saved first, so submitting never loses it.
      if (current.isEditable && this.dirty() && SAVES_FIRST.includes(name)) {
        await this.persistDetails();
      }
      this.adopt(await firstValueFrom(this.api.transition(current.id, name, this.note().trim() || null)));
      this.note.set('');
    } catch (error) {
      this.error.set(this.i18n.t(errorKey(problemCode(error))));
    } finally {
      this.busy.set(false);
    }
  }

  private async persistDetails(): Promise<void> {
    const current = this.application();
    if (!current) {
      return;
    }
    this.adopt(await firstValueFrom(this.api.savePayload(current.id, { ...current.payload, details: this.details() })));
  }

  private async load(id: string): Promise<void> {
    this.view.set('loading');
    this.error.set(null);
    this.message.set(null);
    try {
      this.adopt(await firstValueFrom(this.api.get(id)));
      this.view.set('ready');
    } catch (error) {
      this.view.set(error instanceof HttpErrorResponse && error.status === 404 ? 'notFound' : 'error');
    }
  }

  private adopt(detail: ApplicationDetail): void {
    this.application.set(detail);
    const details = detail.payload['details'];
    const text = typeof details === 'string' ? details : '';
    this.details.set(text);
    this.savedDetails.set(text);
  }
}
