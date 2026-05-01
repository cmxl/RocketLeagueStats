import { Component, ChangeDetectionStrategy, input, computed } from '@angular/core';
import { NgxEchartsDirective, provideEchartsCore } from 'ngx-echarts';
import * as echarts from 'echarts/core';
import { ScatterChart } from 'echarts/charts';
import { GridComponent, TooltipComponent, LegendComponent } from 'echarts/components';
import { CanvasRenderer } from 'echarts/renderers';
import { MatchRecap } from '../../core/models/match';

echarts.use([ScatterChart, GridComponent, TooltipComponent, LegendComponent, CanvasRenderer]);

@Component({
  selector: 'rls-goal-timeline-chart',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgxEchartsDirective],
  providers: [provideEchartsCore({ echarts })],
  template: `
    <div class="chart-card">
      <h4 class="chart-title">Goal Timeline</h4>
      <div echarts [options]="options()" class="chart"></div>
    </div>
  `,
  styles: [`
    .chart-card { padding: 1rem; }
    .chart-title { font-family: var(--font-header); font-size: var(--text-sm); color: var(--text-secondary); text-transform: uppercase; letter-spacing: 0.08em; margin: 0 0 0.5rem; }
    .chart { height: 200px; width: 100%; }
  `],
})
export class GoalTimelineChartComponent {
  readonly recap = input.required<MatchRecap>();

  protected readonly options = computed(() => ({
    backgroundColor: 'transparent',
    tooltip: { trigger: 'item' as const },
    xAxis: {
      type: 'value' as const,
      name: 'time (s)',
      max: this.recap().summary.durationSeconds,
      axisLabel: { color: '#7A8AA8' },
      axisLine: { lineStyle: { color: '#4A5A78' } },
      splitLine: { lineStyle: { color: '#13182A' } },
    },
    yAxis: {
      show: false,
      type: 'category' as const,
      data: ['orange', 'blue'],
    },
    series: [{
      type: 'scatter' as const,
      data: this.recap().goals.map(g => [g.matchClockSeconds, g.scorer.team === 'blue' ? 1 : 0]),
      symbolSize: 16,
      itemStyle: {
        color: (params: { value: number[] }) => params.value[1] === 1 ? '#00B7FF' : '#FF8500',
      },
    }],
  }));
}
