import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export type Lang = 'en' | 'am' | 'om';
export const SUPPORTED_LANGS: readonly Lang[] = ['en', 'am', 'om'];
export const LANG_LABELS: Readonly<Record<Lang, string>> = {
  en: 'English',
  am: 'አማርኛ',
  om: 'Afaan Oromoo',
};

interface Dictionary {
  [key: string]: string | Dictionary;
}

const STORAGE_KEY = 'onegov.lang';

/**
 * Runtime translations loaded from /i18n/{lang}.json. Only the active language (plus English as fallback)
 * is downloaded, which keeps first load small on slow connections. Templates call t(); because it reads
 * signals, views update automatically when the language changes.
 */
@Injectable({ providedIn: 'root' })
export class I18nService {
  private readonly http = inject(HttpClient);
  private readonly dictionaries = signal<Partial<Record<Lang, Dictionary>>>({});

  readonly lang = signal<Lang>('en');

  async init(): Promise<void> {
    await this.use(this.initialLang());
  }

  async use(lang: Lang): Promise<void> {
    await this.load('en');
    if (lang !== 'en') {
      await this.load(lang);
    }
    this.lang.set(lang);
    document.documentElement.lang = lang;
    this.remember(lang);
  }

  t(key: string, params?: Record<string, string | number>): string {
    const dictionaries = this.dictionaries();
    const value = this.lookup(dictionaries[this.lang()], key) ?? this.lookup(dictionaries.en, key);
    if (value === undefined) {
      return key;
    }
    return params
      ? value.replace(/\{(\w+)\}/g, (match, name: string) => String(params[name] ?? match))
      : value;
  }

  private async load(lang: Lang): Promise<void> {
    if (this.dictionaries()[lang]) {
      return;
    }
    try {
      const dictionary = await firstValueFrom(this.http.get<Dictionary>(`i18n/${lang}.json`));
      this.dictionaries.update((current) => ({ ...current, [lang]: dictionary }));
    } catch {
      // Keep working with English if a language file cannot be loaded.
    }
  }

  private lookup(dictionary: Dictionary | undefined, key: string): string | undefined {
    let node: string | Dictionary | undefined = dictionary;
    for (const part of key.split('.')) {
      if (node === undefined || typeof node === 'string') {
        return undefined;
      }
      node = node[part];
    }
    return typeof node === 'string' ? node : undefined;
  }

  private initialLang(): Lang {
    try {
      const stored = localStorage.getItem(STORAGE_KEY);
      if (stored && (SUPPORTED_LANGS as readonly string[]).includes(stored)) {
        return stored as Lang;
      }
    } catch {
      // Storage can be unavailable (private mode, shared device policy).
    }
    const browser = (navigator.language ?? 'en').toLowerCase();
    if (browser.startsWith('am')) {
      return 'am';
    }
    if (browser.startsWith('om')) {
      return 'om';
    }
    return 'en';
  }

  private remember(lang: Lang): void {
    try {
      localStorage.setItem(STORAGE_KEY, lang); // non-sensitive preference only
    } catch {
      // Ignore.
    }
  }
}
