import { Component, ChangeDetectionStrategy, input, output, signal, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { MatchSummary } from '../../core/models/match';
import { PanelComponent } from '../../shared/components/panel.component';
import { MatchTypeBadgeComponent } from '../../shared/components/match-type-badge.component';
import { ConfirmDialogComponent } from '../../shared/components/confirm-dialog.component';
import { DurationPipe } from '../../shared/pipes/duration.pipe';
import { ApiClient } from '../../core/api/api-client.service';
import { SettingsStore } from '../../core/state/settings.store';

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
      <div class="match-card"
           [class.match-card--win]="outcome() === 'win'"
           [class.match-card--loss]="outcome() === 'loss'"
           [style.--card-blue]="blueColor()"
           [style.--card-orange]="orangeColor()">
        <header class="match-card__header">
          <rls-match-type-badge [type]="match().type" />
          @if (match().arenaName; as arena) {
            <span class="match-card__arena">{{ arena }}</span>
          }
          <button type="button"
                  class="match-card__delete"
                  title="Delete match"
                  aria-label="Delete match"
                  (click)="openConfirm()">×</button>
        </header>
        <a class="match-card__body" [routerLink]="['/recap', match().matchId]">
          <p class="match-card__teams">
            <span class="match-card__team match-card__team--blue">{{ blueLabel() }}</span>
            <span class="match-card__team-spacer"></span>
            <span class="match-card__team match-card__team--orange">{{ orangeLabel() }}</span>
          </p>
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
      padding: 1rem 1rem 1rem 1.25rem;
      position: relative;
    }
    /* 4px left-edge stripe — green if the configured player won this match, red if they lost,
       transparent if there's no configured player or they weren't on either roster. ::before
       riding on position:absolute keeps the stripe from contributing to the card's content
       width (would shove score/badge inward and cause a visible jitter when toggling). */
    .match-card::before {
      content: '';
      position: absolute;
      left: 0;
      top: 0;
      bottom: 0;
      width: 4px;
      background: transparent;
    }
    .match-card--win::before  { background: var(--accent-success); }
    .match-card--loss::before { background: var(--accent-danger); }
    .match-card__header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 0.5rem;
      margin-bottom: 0.5rem;
    }
    .match-card__arena {
      flex: 1;
      font-family: var(--font-header);
      font-size: var(--text-xs);
      color: var(--text-muted);
      text-transform: uppercase;
      letter-spacing: 0.06em;
      text-align: center;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
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
      flex-shrink: 0;
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
    .match-card__teams {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin: 0 0 0.25rem;
      font-family: var(--font-header);
      font-size: var(--text-xs);
      text-transform: uppercase;
      letter-spacing: 0.08em;
    }
    .match-card__team-spacer { flex: 1; }
    /* Per-match colors land on --card-blue / --card-orange (set inline from blueTeam.colorPrimary
       on the match summary). Falls through to the global --team-blue / --team-orange palette so
       legacy rows without team metadata still render with the default colors. */
    .match-card__team--blue  { color: var(--card-blue,  var(--team-blue));  }
    .match-card__team--orange { color: var(--card-orange, var(--team-orange)); }
    .match-card__score { font-family: var(--font-display); font-size: var(--text-3xl); margin: 0 0 0.25rem; line-height: 1; }
    .blue   { color: var(--card-blue,  var(--team-blue));  }
    .orange { color: var(--card-orange, var(--team-orange)); }
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
  private readonly settings = inject(SettingsStore);

  protected readonly showConfirm = signal(false);
  protected readonly deleting = signal(false);

  // Team color overrides — '#'-prefixed hex when the match has stored team metadata, 'unset'
  // otherwise so the inherited --team-blue / --team-orange theme variable shines through. Same
  // pattern as ScoreboardHeaderComponent uses for the live page; keeps both views consistent.
  protected readonly blueColor = computed(() => {
    const team = this.match().blueTeam;
    return team ? `#${team.colorPrimary}` : 'unset';
  });

  protected readonly orangeColor = computed(() => {
    const team = this.match().orangeTeam;
    return team ? `#${team.colorPrimary}` : 'unset';
  });

  protected readonly blueLabel = computed(() =>
    (this.match().blueTeam?.name ?? 'BLUE').toUpperCase());

  protected readonly orangeLabel = computed(() =>
    (this.match().orangeTeam?.name ?? 'ORANGE').toUpperCase());

  // Win/loss stripe: green if the configured player is on the winner's roster, red if on the
  // loser's. 'none' otherwise (no configured player, or configured player isn't in this match's
  // AllPlayers — common for matches played by a different account on the same machine).
  protected readonly outcome = computed<'win' | 'loss' | 'none'>(() => {
    const winner = this.match().winnerTeamNum;
    const playerName = this.settings.current().playerName?.trim();
    if (winner === null || winner === undefined || !playerName) {
      return 'none';
    }

    // Case-insensitive match because RL display names are unstable casing-wise across sessions
    // (some platforms uppercase legacy migrated names).
    const me = this.match().allPlayers.find(p => p.name.toLowerCase() === playerName.toLowerCase());
    if (!me) {
      return 'none';
    }

    const myTeamNum = me.team === 'blue' ? 0 : me.team === 'orange' ? 1 : -1;
    if (myTeamNum === -1) {
      return 'none';
    }

    return myTeamNum === winner ? 'win' : 'loss';
  });

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
