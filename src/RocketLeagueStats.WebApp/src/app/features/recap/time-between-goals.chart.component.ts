import { Component, ChangeDetectionStrategy, input, computed } from '@angular/core';
import { NgxEchartsDirective, provideEchartsCore } from 'ngx-echarts';
import * as echarts from 'echarts/core';
import { BarChart } from 'echarts/charts';
import { GridComponent, TooltipComponent } from 'echarts/components';
import { CanvasRenderer } from 'echarts/renderers';
import { MatchRecap } from '../../core/models/match';

echarts.use([BarChart, GridComponent, TooltipComponent, CanvasRenderer]);

@Component({
  selector: 'rls-time-between-goals-chart',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgxEchartsDirective],
  providers: [provideEchartsCore({ echarts })],
  template: `
    <div class="chart-card">
      <h4 class="chart-title">Time Between Goals (s)</h4>
      <div echarts [options]="options()" class="chart"></div>
    </div>
  `,
  styles: [`
    .chart-card { padding: 1rem; }
    .chart-title { font-family: var(--font-header); font-size: var(--text-sm); color: var(--text-secondary); text-transform: uppercase; letter-spacing: 0.08em; margin: 0 0 0.5rem; }
    .chart { height: 200px; width: 100%; }
  `],
})
export class TimeBetweenGoalsChartComponent {
  readonly recap = input.required<MatchRecap>();

  protected readonly options = computed(() => {
    const times = this.recap().timeBetweenGoalsSeconds;
    return {
      backgroundColor: 'transparent',
      tooltip: { trigger: 'axis' as const },
      xAxis: {
        type: 'category' as const,
        data: times.map((_, i) => `Goal ${i + 2}`),
        axisLabel: { color: '#7A8AA8' },
        axisLine: { lineStyle: { color: '#4A5A78' } },
      },
      yAxis: {
        type: 'value' as const,
        axisLabel: { color: '#7A8AA8' },
        splitLine: { lineStyle: { color: '#13182A' } },
      },
      series: [{
        type: 'bar' as const,
        data: times,
        itemStyle: { color: '#00E5FF', borderRadius: [2, 2, 0, 0] },
      }],
    };
  });
}
