import { Pipe, PipeTransform } from '@angular/core';

/**
 * Format a Rocket League goal speed as a km/h string.
 *
 * Despite the wire field being named GoalSpeed-UuPerSec, Psyonix's Stats API
 * reports GoalSpeed values that are already in km/h units (1:1 mapping, not
 * Unreal cm/s). Confirmed empirically against real captures — typical goals
 * land in the 40–130 range, which matches realistic RL goal speeds in km/h.
 * No arithmetic conversion needed — just round and label.
 */
@Pipe({ name: 'kmh', standalone: true })
export class KmhPipe implements PipeTransform {
  transform(speed: number | null | undefined): string {
    if (speed == null) return '—';
    return `${Math.round(speed)} km/h`;
  }
}
