import { Component, ChangeDetectionStrategy, input } from '@angular/core';

export type PanelTeam = 'blue' | 'orange' | 'neutral' | 'mvp';

@Component({
  selector: 'rls-panel',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="panel" [class]="'panel--' + team()" [class.panel--glow]="glow()">
    <ng-content />
  </div>`,
  styleUrl: './panel.component.css',
})
export class PanelComponent {
  readonly team = input<PanelTeam>('neutral');
  readonly glow = input<boolean>(false);
}
