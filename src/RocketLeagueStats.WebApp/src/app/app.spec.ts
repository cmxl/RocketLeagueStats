import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { App } from './app';
import { routes } from './app.routes';
import { StatsHubClient } from './core/api/stats-hub.client';
import { signal } from '@angular/core';

describe('App', () => {
  it('creates', async () => {
    const hubMock: Partial<StatsHubClient> = {
      state: signal('idle' as const),
      connect: () => Promise.resolve(),
      onGoal: () => void 0,
      onStatfeed: () => void 0,
      onClockTick: () => void 0,
      onPlayerStatsTick: () => void 0,
      onPhaseChanged: () => void 0,
      onConnectionState: () => void 0,
      onMatchInitialized: () => void 0,
      onMatchEnded: () => void 0,
      onReconnected: () => void 0,
    };

    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter(routes),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: StatsHubClient, useValue: hubMock },
      ],
    }).compileComponents();
    const fixture = TestBed.createComponent(App);
    expect(fixture.componentInstance).toBeTruthy();
  });
});
