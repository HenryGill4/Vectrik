> ⚠️ **LEGACY** — Historical reference only. Do not use for planning or development.

# Sprint 10: Machine Integration + SignalR

> **Status**: NOT STARTED
> **Goal**: Live machine status with real-time updates and mock telemetry.
> **Depends on**: Sprint 2 (machines configured), Sprint 4 (shop floor running)

---

## Tasks

```
[ ] 10.1  Machine Status page — card per machine with status, current job, utilization %
[ ] 10.2  SignalR connection — MachineStateHub pushes updates to page
[ ] 10.3  MockMachineProvider — generates realistic spoof telemetry (temp, progress, layer)
[ ] 10.4  MachineSyncService — polls providers and pushes state via SignalR
[ ] 10.5  Machine card — live telemetry: bed temp, chamber temp, build progress bar
[ ] 10.6  Machine card — current layer / total layers display
[ ] 10.7  SLS Printing stage partial — live telemetry panel from SignalR
[ ] 10.8  Machine status quick actions — mark idle, start maintenance
[ ] 10.9  Machine state history — record MachineStateRecord snapshots
[ ] 10.10 Machine connection settings — admin page for OPC UA config
[ ] 10.11 Verify: machine cards update in real-time, SLS printing shows live data
```

---

## Acceptance Criteria

- Machine cards show live status without page refresh
- MockMachineProvider generates realistic SLS telemetry
- SignalR pushes updates every 5 seconds
- SLS Printing stage shows telemetry panel with live data
- Machine state history is persisted for later analysis
- Works on iPad (SignalR over WebSocket)

## Files to Touch

- `Components/Pages/Machines/Index.razor` — live cards
- `Components/Pages/ShopFloor/Partials/SLSPrinting.razor` — telemetry panel
- `wwwroot/js/machine-state-client.js` — SignalR client
- `Services/MachineProviders/MockMachineProvider.cs` — realistic data
- `Services/MachineProviders/MachineSyncService.cs` — polling + push
- `Hubs/MachineStateHub.cs` — already exists
