import { Component, ChangeDetectionStrategy, input } from '@angular/core';

@Component({
  selector: 'rls-team-stripe',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="team-stripe" [class]="'team-stripe--' + team()"></div>
  `,
  styles: [`
    .team-stripe {
      width: 6px;
      align-self: stretch;
      border-radius: 1px;
      flex-shrink: 0;
    }
    .team-stripe--blue   { background: var(--team-blue); }
    .team-stripe--orange { background: var(--team-orange); }
  `],
})
export class TeamStripeComponent {
  readonly team = input.required<'blue' | 'orange'>();
}
