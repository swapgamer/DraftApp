import { ChangeDetectionStrategy, Component, ElementRef, ViewChild, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { ChatResponse, ChatService } from '../../core/chat/chat.service';

interface ConversationEntry { message: string; response: ChatResponse; sentAt: Date; }

@Component({
  standalone: true,
  imports: [DatePipe, FormsModule, RouterLink, MatButtonModule, MatCardModule, MatIconModule],
  templateUrl: './assistant.component.html',
  styleUrl: './assistant.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AssistantComponent {
  @ViewChild('conversation') private conversation?: ElementRef<HTMLElement>;

  readonly message = signal('');
  readonly loading = signal(false);
  readonly error = signal('');
  readonly history = signal<ConversationEntry[]>([]);
  readonly prompts = ['Top centre backs', 'Rank 1 striker', 'Highest goal credit', 'Tell me about Messi'];
  private readonly chat = inject(ChatService);
  private readonly historyKey = 'draft-datastore.chat-history';

  constructor() {
    try {
      const stored = localStorage.getItem(this.historyKey);
      if (!stored) return;
      const entries = JSON.parse(stored) as Array<Omit<ConversationEntry, 'sentAt'> & { sentAt: string }>;
      this.history.set(entries.map((entry) => ({ ...entry, sentAt: new Date(entry.sentAt) })).filter((entry) => !Number.isNaN(entry.sentAt.getTime())));
    } catch { localStorage.removeItem(this.historyKey); }
  }

  send(value = this.message()): void {
    const message = value.trim();
    if (!message || this.loading()) return;
    this.message.set('');
    this.loading.set(true);
    this.error.set('');
    this.chat.query(message).subscribe({
      next: (response) => {
        this.history.update((items) => {
          const updated = [...items, { message, response, sentAt: new Date() }].slice(-30);
          this.persistHistory(updated);
          return updated;
        });
        setTimeout(() => this.scrollToLatest());
      },
      error: () => { this.error.set('The assistant is unavailable. Please try again.'); this.loading.set(false); },
      complete: () => this.loading.set(false),
    });
  }

  clearHistory(): void { this.history.set([]); localStorage.removeItem(this.historyKey); this.error.set(''); }

  private persistHistory(entries: ConversationEntry[]): void {
    try { localStorage.setItem(this.historyKey, JSON.stringify(entries)); } catch { /* Storage is optional. */ }
  }

  private scrollToLatest(): void {
    const element = this.conversation?.nativeElement;
    if (element) element.scrollTo({ top: element.scrollHeight, behavior: 'smooth' });
  }
}

