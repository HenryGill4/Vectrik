# Sprint 4: Shop Floor (Operator Workflow)

> **Status**: NOT STARTED
> **Goal**: Operators can process parts through stages with real forms, serial numbers, and delay logging.
> **Depends on**: Sprint 3 (WO released → StageExecutions exist)

---

## Tasks

```
[ ] 4.1  Stage.razor — render built-in partial based on slug match (sls-printing → SLSPrinting.razor)
[ ] 4.2  Stage.razor — render GenericStage.razor for custom stages (reads CustomFieldsConfig)
[ ] 4.3  GenericStage.razor — form inputs from CustomFieldsConfig (text, number, dropdown, checkbox)
[ ] 4.4  Stage.razor — Start action: sets StageExecution status to InProgress, records operator + timestamp
[ ] 4.5  Stage.razor — Complete action: saves CustomFieldValues JSON, calculates ActualHours, updates status
[ ] 4.6  Stage.razor — Fail action: marks execution as Failed with notes
[ ] 4.7  LaserEngraving partial — serial number input per part (calls SerialNumberService)
[ ] 4.8  LaserEngraving partial — creates PartInstance records on completion
[ ] 4.9  QualityControl partial — inspection form with pass/fail per serial number
[ ] 4.10 QualityControl partial — creates QCInspection + QCChecklistItem records
[ ] 4.11 Shipping partial — packing list (serial numbers), carrier, tracking number
[ ] 4.12 Shipping partial — updates WorkOrderLine.ShippedQuantity on completion
[ ] 4.13 Batch stage handling — group multiple executions for batch stages (depowdering, heat treatment)
[ ] 4.14 Delay logging — modal with reason code dropdown + minutes + notes
[ ] 4.15 Stage.razor — operator notes field on completion form
[ ] 4.16 Stage.razor — history tab shows recent completions with duration + operator
[ ] 4.17 LearningService integration — after stage completion, update EMA on PartStageRequirement
[ ] 4.18 Verify: process a part through all 9 stages → serial assigned at engraving → QC → shipped
```

---

## Acceptance Criteria

- Built-in stages render their dedicated partial with stage-specific fields
- Custom stages render form dynamically from CustomFieldsConfig JSON
- Start/Complete/Fail actions update StageExecution correctly
- CustomFieldValues JSON is saved on completion
- Serial numbers are assigned at Laser Engraving stage
- QC creates inspection records linked to PartInstance
- Shipping updates WO line fulfillment
- Batch stages allow grouping multiple parts
- Delays are logged with reason codes from SystemSettings
- LearningService updates EMA after each completion
- Full 13-step production flow works end-to-end

## Files to Touch

- `Components/Pages/ShopFloor/Stage.razor` — major rewrite
- All 10 partials in `Components/Pages/ShopFloor/Partials/`
- `Services/StageService.cs` — completion logic + custom field saving
- `Services/SerialNumberService.cs` — serial assignment
- `Services/LearningService.cs` — EMA update on completion
- `Services/WorkOrderService.cs` — fulfillment tracking
