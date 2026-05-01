import { Component, ChangeDetectionStrategy } from '@angular/core';
import { LiveTileComponent } from './live-tile.component';
import { HistoryTileComponent } from './history-tile.component';

@Component({
  selector: 'rls-landing',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [LiveTileComponent, HistoryTileComponent],
  template: `
    <div class="landing">
      <header class="landing__header">
        <h1 class="landing__title">ROCKET LEAGUE STATS</h1>
        <p class="landing__sub">Pick a view</p>
      </header>
      <div class="landing__tiles">
        <rls-live-tile />
        <rls-history-tile />
      </div>
    </div>
  `,
  styles: [`
    .landing { padding: 2rem 1.5rem; max-width: 900px; margin: 0 auto; }
    .landing__header { text-align: center; margin-bottom: 2.5rem; }
    .landing__title { font-family: var(--font-display); font-size: var(--text-display-md); color: var(--text-primary); margin: 0 0 0.5rem; letter-spacing: 0.04em; }
    .landing__sub { font-family: var(--font-header); font-size: var(--text-lg); color: var(--text-secondary); text-transform: uppercase; letter-spacing: 0.1em; margin: 0; }
    .landing__tiles { display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 1.5rem; }
  `],
})
export class LandingPageComponent {}
