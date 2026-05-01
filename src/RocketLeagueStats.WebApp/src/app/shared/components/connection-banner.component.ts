import { Component, ChangeDetectionStrategy, inject, computed } from '@angular/core';
import { LiveMatchStore } from '../../core/state/live-match.store';
import { StatsHubClient } from '../../core/api/stats-hub.client';

@Component({
  selector: 'rls-connection-banner',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (bannerText()) {
      <div class="connection-banner" [class]="bannerClass()">
        {{ bannerText() }}
      </div>
    }
  `,
  styles: [`
    .connection-banner {
      position: fixed;
      top: 0;
      left: 0;
      right: 0;
      height: 32px;
      display: flex;
      align-items: center;
      justify-content: center;
      font-family: var(--font-header);
      font-size: var(--text-sm);
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: 0.08em;
      z-index: 1000;
      animation: rls-slide-down 300ms ease;
    }
    .connection-banner--disconnected {
      background: var(--accent-danger);
      color: var(--bg-base);
    }
    .connection-banner--reconnecting {
      background: var(--accent-mvp);
      color: var(--bg-base);
    }
    .connection-banner--no-game {
      background: var(--text-muted);
      color: var(--text-primary);
    }
  `],
})
export class ConnectionBannerComponent {
  private readonly liveMatch = inject(LiveMatchStore);
  private readonly hub = inject(StatsHubClient);

  protected readonly bannerText = computed(() => {
    const hubState = this.hub.state();
    const gameConnected = this.liveMatch.gameConnected();

    if (hubState === 'disconnected') return 'Disconnected from server — retrying…';
    if (hubState === 'reconnecting') return 'Reconnecting to server…';
    if (!gameConnected) return 'Rocket League not detected';
    return null;
  });

  protected readonly bannerClass = computed(() => {
    const hubState = this.hub.state();
    if (hubState === 'disconnected') return 'connection-banner--disconnected';
    if (hubState === 'reconnecting') return 'connection-banner--reconnecting';
    return 'connection-banner--no-game';
  });
}
