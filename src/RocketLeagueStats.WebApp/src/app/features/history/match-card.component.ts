import { Component, ChangeDetectionStrategy, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { MatchSummary } from '../../core/models/match';
import { PanelComponent } from '../../shared/components/panel.component';
import { MatchTypeBadgeComponent } from '../../shared/components/match-type-badge.component';
import { DurationPipe } from '../../shared/pipes/duration.pipe';

@Component({
  selector: 'rls-match-card',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, PanelComponent, MatchTypeBadgeComponent, DurationPipe, DatePipe],
  template: `
    <rls-panel>
      <a class="match-card" [routerLink]="['/recap', match().matchId]">
        <header class="match-card__header">
          <rls-match-type-badge [type]="match().type" />
          <time class="match-card__time">{{ match().endedAt | date:'short' }}</time>
        </header>
        <p class="match-card__score">
          <span class="blue">{{ match().blueScore }}</span>
          <span class="match-card__dash"> — </span>
          <span class="orange">{{ match().orangeScore }}</span>
        </p>
        <p class="match-card__duration">{{ match().durationSeconds | duration }}</p>
        @if (match().mvp) {
          <p class="match-card__mvp">MVP: {{ match().mvp!.name }}</p>
        }
      </a>
    </rls-panel>
  `,
  styles: [`
    .match-card {
      display: block;
      padding: 1rem;
      text-decoration: none;
      color: inherit;
    }
    .match-card:hover { opacity: 0.85; }
    .match-card__header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 0.75rem; }
    .match-card__time { font-size: var(--text-xs); color: var(--text-muted); font-family: var(--font-body); }
    .match-card__score { font-family: var(--font-display); font-size: var(--text-3xl); margin: 0 0 0.25rem; line-height: 1; }
    .blue { color: var(--team-blue); }
    .orange { color: var(--team-orange); }
    .match-card__dash { color: var(--text-muted); font-size: var(--text-2xl); }
    .match-card__duration { font-family: var(--font-header); font-size: var(--text-sm); color: var(--text-secondary); margin: 0; }
    .match-card__mvp { font-family: var(--font-header); font-size: var(--text-xs); color: var(--accent-mvp); margin: 0.25rem 0 0; text-transform: uppercase; letter-spacing: 0.05em; }
  `],
})
export class MatchCardComponent {
  readonly match = input.required<MatchSummary>();
}
