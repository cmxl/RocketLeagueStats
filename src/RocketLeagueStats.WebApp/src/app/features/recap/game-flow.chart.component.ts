import { Component, ChangeDetectionStrategy, input, computed } from '@angular/core';
import { NgxEchartsDirective, provideEchartsCore } from 'ngx-echarts';
import * as echarts from 'echarts/core';
import { LineChart } from 'echarts/charts';
import { GridComponent, TooltipComponent, LegendComponent } from 'echarts/components';
import { CanvasRenderer } from 'echarts/renderers';
import { MatchRecap } from '../../core/models/match';

echarts.use([LineChart, GridComponent, TooltipComponent, LegendComponent, CanvasRenderer]);

@Component({
  selector: 'rls-game-flow-chart',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgxEchartsDirective],
  providers: [provideEchartsCore({ echarts })],
  template: `
    <div class="chart-card">
      <h4 class="chart-title">Game Flow</h4>
      <div echarts [options]="options()" class="chart"></div>
    </div>
  `,
  styles: [`
    .chart-card { padding: 1rem; }
    .chart-title { font-family: var(--font-header); font-size: var(--text-sm); color: var(--text-secondary); text-transform: uppercase; letter-spacing: 0.08em; margin: 0 0 0.5rem; }
    .chart { height: 200px; width: 100%; }
  `],
})
export class GameFlowChartComponent {
  readonly recap = input.required<MatchRecap>();

  protected readonly options = computed(() => {
    const flow = this.recap().flow;
    return {
      backgroundColor: 'transparent',
      tooltip: { trigger: 'axis' as const },
      legend: {
        data: ['Blue', 'Orange'],
        textStyle: { color: '#7A8AA8' },
      },
      xAxis: {
        type: 'category' as const,
        data: flow.timestampSeconds.map(s => `${Math.floor(s / 60)}:${String(s % 60).padStart(2, '0')}`),
        axisLabel: { color: '#7A8AA8' },
        axisLine: { lineStyle: { color: '#4A5A78' } },
      },
      yAxis: {
        type: 'value' as const,
        minInterval: 1,
        axisLabel: { color: '#7A8AA8' },
        splitLine: { lineStyle: { color: '#13182A' } },
      },
      series: [
        {
          name: 'Blue',
          type: 'line' as const,
          data: flow.blueScoreAtStep,
          lineStyle: { color: '#00B7FF', width: 2 },
          itemStyle: { color: '#00B7FF' },
          step: 'end' as const,
        },
        {
          name: 'Orange',
          type: 'line' as const,
          data: flow.orangeScoreAtStep,
          lineStyle: { color: '#FF8500', width: 2 },
          itemStyle: { color: '#FF8500' },
          step: 'end' as const,
        },
      ],
    };
  });
}
