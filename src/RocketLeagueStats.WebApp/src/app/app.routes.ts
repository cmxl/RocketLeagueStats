import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./features/landing/landing-page.component').then(m => m.LandingPageComponent),
    title: 'Rocket League Stats',
  },
  {
    path: 'live',
    loadComponent: () => import('./features/live/live-view.component').then(m => m.LiveViewComponent),
    title: 'Live Match',
  },
  {
    path: 'history',
    loadComponent: () => import('./features/history/history-view.component').then(m => m.HistoryViewComponent),
    title: 'Match History',
  },
  {
    path: 'recap/:matchId',
    loadComponent: () => import('./features/recap/recap-view.component').then(m => m.RecapViewComponent),
    title: 'Match Recap',
  },
  {
    path: 'settings',
    loadComponent: () => import('./features/settings/settings-page.component').then(m => m.SettingsPageComponent),
    title: 'Settings',
  },
  { path: '**', redirectTo: '' },
];
