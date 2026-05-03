import { Pipe, PipeTransform } from '@angular/core';
import { StatfeedType } from '../../core/models/enums';

/**
 * Maps a StatfeedType to a single emoji glyph used in the live action feed and recap timeline.
 *
 * Single source of truth — adding a new statfeed type means adding one case here, no template
 * changes elsewhere. Default branch covers any future server-side StatfeedType the frontend
 * hasn't shipped yet.
 */
@Pipe({ name: 'statfeedIcon', standalone: true })
export class StatfeedIconPipe implements PipeTransform {
  transform(type: StatfeedType): string {
    switch (type) {
      case 'save': return '🧤';
      case 'epicSave': return '✨';
      case 'demolish': return '💥';
      case 'damage': return '💢';
      case 'ultraDamage': return '⚡';
      case 'savior': return '🛡️';
      case 'bicycleHit': return '🚴';
      case 'bicycleGoal': return '🤸';
      case 'aerialGoal': return '✈️';
      case 'backwardsGoal': return '🔄';
      case 'overtimeGoal': return '⏱️';
      case 'longGoal': return '🏌️';
      case 'poolShot': return '🎱';
      case 'hattrick': return '🎩';
      case 'mvpHattrick': return '👑';
      case 'mvp': return '🏅';
      case 'win': return '🏆';
      case 'other':
      default: return '🎮';
    }
  }
}
