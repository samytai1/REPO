import { Injectable } from '@angular/core';

/**
 * Session-storage backed persistence for list-page filters, sort and paging.
 *
 * Convention keys: `{table}-list-filters`, `{table}-list-sort`, `{table}-list-page`.
 * Every access is guarded — session storage can be unavailable or throw in private modes.
 */
@Injectable({ providedIn: 'root' })
export class ListStateService {
  read<T>(key: string): T | null {
    try {
      const raw = sessionStorage.getItem(key);
      return raw ? (JSON.parse(raw) as T) : null;
    } catch {
      return null;
    }
  }

  write(key: string, value: unknown): void {
    try {
      sessionStorage.setItem(key, JSON.stringify(value));
    } catch {
      /* storage unavailable — filters simply do not persist */
    }
  }

  clear(key: string): void {
    try {
      sessionStorage.removeItem(key);
    } catch {
      /* no-op */
    }
  }
}
