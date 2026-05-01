import { Component, ChangeDetectionStrategy, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { LiveMatchStore } from '../../core/state/live-match.store';

@Component({
  selector: 'rls-nav-bar',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, RouterLinkActive],
  template: `
    <header class="nav-bar">
      <span class="nav-bar__brand">RLS</span>
      <nav class="nav-bar__links">
        <a routerLink="/" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }">Home</a>
        <a routerLink="/live" routerLinkActive="active">
          Live
          @if (liveMatch.hasLiveMatch()) {
            <span class="nav-bar__live-dot" aria-label="live match active"></span>
          }
        </a>
        <a routerLink="/history" routerLinkActive="active">History</a>
        <a routerLink="/settings" routerLinkActive="active">Settings</a>
      </nav>
    </header>
  `,
  styles: [`
    .nav-bar {
      display: flex;
      align-items: center;
      gap: 2rem;
      padding: 0.75rem 1.5rem;
      background: var(--bg-elevated);
      border-bottom: 1px solid var(--accent-cyan);
    }
    .nav-bar__brand {
      font-family: var(--font-display);
      font-size: var(--text-xl);
      color: var(--accent-cyan);
    }
    .nav-bar__links {
      display: flex;
      gap: 1.5rem;
    }
    .nav-bar__links a {
      position: relative;
      color: var(--text-secondary);
      text-decoration: none;
      font-family: var(--font-header);
      font-weight: 600;
      font-size: var(--text-sm);
      text-transform: uppercase;
      letter-spacing: 0.05em;
      transition: color 150ms;
    }
    .nav-bar__links a:hover,
    .nav-bar__links a.active {
      color: var(--text-primary);
    }
    .nav-bar__live-dot {
      display: inline-block;
      width: 6px;
      height: 6px;
      border-radius: 50%;
      background: var(--accent-success);
      margin-left: 4px;
      vertical-align: middle;
      animation: rls-fade-in 400ms ease;
    }
  `],
})
export class NavBarComponent {
  protected readonly liveMatch = inject(LiveMatchStore);
}
