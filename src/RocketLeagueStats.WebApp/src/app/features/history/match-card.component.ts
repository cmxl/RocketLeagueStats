import { Component, ChangeDetectionStrategy, input, output, signal, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { MatchSummary } from '../../core/models/match';
import { PanelComponent } from '../../shared/components/panel.component';
import { MatchTypeBadgeComponent } from '../../shared/components/match-type-badge.component';
import { ConfirmDialogComponent } from '../../shared/components/confirm-dialog.component';
import { DurationPipe } from '../../shared/pipes/duration.pipe';
import { ApiClient } from '../../core/api/api-client.service';

@Component({
  selector: 'rls-match-card',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, PanelComponent, MatchTypeBadgeComponent, DurationPipe, DatePipe, ConfirmDialogComponent],
  template: `
    <rls-panel>
      <!-- Card body and recap link no longer wrap the whole card — the delete button needs to
           live as a sibling so its click doesn't navigate to the recap. The header row stays
           outside the link, the score / duration / mvp block is the actual link target. -->
      <div class="match-card">
        <header class="match-card__header">
          <rls-match-type-badge [type]="match().type" />
          <button type="button"
                  class="match-card__delete"
                  title="Delete match"
                  aria-label="Delete match"
                  (click)="openConfirm()">×</button>
        </header>
        <a class="match-card__body" [routerLink]="['/recap', match().matchId]">
          <p class="match-card__score">
            <span class="blue">{{ match().blueScore }}</span>
            <span class="match-card__dash"> — </span>
            <span class="orange">{{ match().orangeScore }}</span>
          </p>
          <p class="match-card__duration">{{ match().durationSeconds | duration }}</p>
          <time class="match-card__time">{{ match().endedAt | date:'short' }}</time>
          @if (match().mvp) {
            <p class="match-card__mvp">MVP: {{ match().mvp!.name }}</p>
          }
        </a>
      </div>
    </rls-panel>

    @if (showConfirm()) {
      <rls-confirm-dialog
        title="Delete match?"
        [message]="confirmMessage()"
        confirmLabel="Delete"
        variant="danger"
        (confirm)="onConfirmDelete()"
        (cancel)="closeConfirm()" />
    }
  `,
  styles: [`
    .match-card {
      padding: 1rem;
      position: relative;
    }
    .match-card__header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 0.5rem;
    }
    .match-card__delete {
      background: transparent;
      border: 1px solid var(--text-muted);
      color: var(--text-muted);
      width: 22px;
      height: 22px;
      line-height: 1;
      font-size: 16px;
      padding: 0;
      border-radius: 2px;
      cursor: pointer;
      display: flex;
      align-items: center;
      justify-content: center;
      transition: color 120ms, border-color 120ms, background 120ms;
    }
    .match-card__delete:hover {
      color: var(--accent-danger);
      border-color: var(--accent-danger);
      background: color-mix(in srgb, var(--accent-danger) 10%, transparent);
    }
    .match-card__body {
      display: block;
      text-decoration: none;
      color: inherit;
    }
    .match-card__body:hover { opacity: 0.85; }
    .match-card__score { font-family: var(--font-display); font-size: var(--text-3xl); margin: 0 0 0.25rem; line-height: 1; }
    .blue { color: var(--team-blue); }
    .orange { color: var(--team-orange); }
    .match-card__dash { color: var(--text-muted); font-size: var(--text-2xl); }
    .match-card__duration { font-family: var(--font-header); font-size: var(--text-sm); color: var(--text-secondary); margin: 0; }
    .match-card__time { display: block; font-size: var(--text-xs); color: var(--text-muted); font-family: var(--font-body); margin-top: 0.25rem; }
    .match-card__mvp { font-family: var(--font-header); font-size: var(--text-xs); color: var(--accent-mvp); margin: 0.25rem 0 0; text-transform: uppercase; letter-spacing: 0.05em; }
  `],
})
export class MatchCardComponent {
  readonly match = input.required<MatchSummary>();
  readonly deleted = output<string>();

  private readonly api = inject(ApiClient);

  protected readonly showConfirm = signal(false);
  protected readonly deleting = signal(false);

  protected confirmMessage(): string {
    const m = this.match();
    return `This will permanently remove the ${m.blueScore}–${m.orangeScore} match and all of its events from the database. This cannot be undone.`;
  }

  protected openConfirm(): void {
    this.showConfirm.set(true);
  }

  protected closeConfirm(): void {
    if (this.deleting()) {
      // Don't let ESC / backdrop click yank the dialog away while a delete is mid-flight.
      return;
    }
    this.showConfirm.set(false);
  }

  protected async onConfirmDelete(): Promise<void> {
    if (this.deleting()) {
      return;
    }
    this.deleting.set(true);
    try {
      await this.api.deleteMatch(this.match().matchId);
      this.deleted.emit(this.match().matchId);
    } finally {
      this.deleting.set(false);
      this.showConfirm.set(false);
    }
  }
}
