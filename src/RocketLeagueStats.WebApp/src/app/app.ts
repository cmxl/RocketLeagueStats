import { Component, ChangeDetectionStrategy, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { LiveMatchStore } from './core/state/live-match.store';
import { NavBarComponent } from './shared/components/nav-bar.component';
import { ConnectionBannerComponent } from './shared/components/connection-banner.component';
import { MatchEndToastComponent } from './shared/components/match-end-toast.component';
import { AppUpdateBannerComponent } from './shared/components/app-update-banner.component';

@Component({
  selector: 'app-root',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterOutlet,
    NavBarComponent,
    ConnectionBannerComponent,
    MatchEndToastComponent,
    AppUpdateBannerComponent,
  ],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App {
  // Injecting LiveMatchStore forces it to instantiate and start the SignalR hub at app startup
  private readonly _live = inject(LiveMatchStore);
}
