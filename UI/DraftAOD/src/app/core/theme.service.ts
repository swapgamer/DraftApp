import { DOCUMENT } from '@angular/common';
import { Injectable, computed, inject, signal } from '@angular/core';

export type Theme = 'dark' | 'light';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly document = inject(DOCUMENT);
  private readonly state = signal<Theme>(this.read());
  readonly theme = this.state.asReadonly();
  readonly isLight = computed(() => this.state() === 'light');

  constructor() { this.apply(this.state()); }

  toggle(): void { this.set(this.isLight() ? 'dark' : 'light'); }
  set(theme: Theme): void { this.state.set(theme); localStorage.setItem('draft-datastore.theme', theme); this.apply(theme); }

  private read(): Theme { return localStorage.getItem('draft-datastore.theme') === 'light' ? 'light' : 'dark'; }
  private apply(theme: Theme): void { this.document.documentElement.classList.toggle('light-theme', theme === 'light'); }
}
