> ⚠️ **LEGACY** — Historical reference only. Do not use for planning or development.

# Sprint 9: Analytics + Dashboard

> **Status**: NOT STARTED
> **Goal**: KPIs with real calculated data, charts, and production insights.
> **Depends on**: Sprints 3-8 (real production data flowing through the system)

---

## Tasks

```
[ ] 9.1  Dashboard — machine utilization bar chart (real data from Jobs)
[ ] 9.2  Dashboard — parts-in-stage pipeline with real counts from StageExecutions
[ ] 9.3  Dashboard — active jobs by machine mini-Gantt
[ ] 9.4  Dashboard — on-time delivery rate (last 30/90 days)
[ ] 9.5  Dashboard — OEE (Overall Equipment Effectiveness) calculation
[ ] 9.6  Analytics — estimated vs actual build time scatter plot (by part, by machine)
[ ] 9.7  Analytics — machine utilization heatmap (by day/week)
[ ] 9.8  Analytics — OEE breakdown: Availability × Performance × Quality
[ ] 9.9  Analytics — stage throughput: parts per day per stage
[ ] 9.10 Analytics — cost analysis: per part, per WO, margins (quoted vs actual)
[ ] 9.11 Analytics — learning curve visualization (EMA improving over time)
[ ] 9.12 Analytics — scrap rate trends
[ ] 9.13 Analytics — on-time delivery trend line
[ ] 9.14 Verify: with 10+ completed jobs, dashboard shows meaningful KPIs
```

---

## Acceptance Criteria

- Dashboard KPIs reflect real data (not zeros)
- Machine utilization calculated from job hours vs available hours
- OEE = Availability × Performance × Quality
- Cost analysis shows quoted price vs actual cost with margin
- Learning curves show EMA estimates improving
- Charts are readable on both desktop and iPad

## Technical Approach

- Pure CSS charts (bar charts, progress bars) — no external charting library
- Or use lightweight JS charting if needed (Chart.js is already Bootstrap-compatible)
- AnalyticsService provides all computed data
- Dashboard polls or uses Blazor re-render

## Files to Touch

- `Components/Pages/Home.razor` — dashboard rewrite with charts
- `Components/Pages/Analytics/Index.razor` — full analytics page
- `Services/AnalyticsService.cs` — ensure all KPI methods return real data
