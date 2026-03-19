# CHUNK-02: Custom Fields Integration

> **Size**: M (Medium) — ~8-10 file edits
> **ROADMAP tasks**: H6.5, H6.6, H6.7, H6.8, H6.9, H6.10
> **Prerequisites**: H1-H5 complete

---

## Scope

Add the `<CustomFieldsEditor>` component to all major entity create/edit forms,
and display custom field values on their detail/read pages. The component and
service already exist — this is purely wiring work.

---

## Files to Read First

| File | Why |
|------|-----|
| `Components/Shared/CustomFieldsEditor.razor` | Understand the component API |
| `Services/CustomFieldService.cs` | Understand `GetFieldDefinitionsAsync(entityType)` |
| `Components/Pages/Parts/Edit.razor` | Already has custom fields tab — use as pattern |
| `Components/Pages/Quotes/Edit.razor` | Needs custom fields added |
| `Components/Pages/WorkOrders/Create.razor` | Needs custom fields added |
| `Components/Pages/Quality/Ncr.razor` | Needs custom fields added |
| `Components/Pages/Inventory/Items.razor` | Needs custom fields added |

---

## Tasks

### Pattern (from Parts/Edit.razor — already implemented)

Each form needs:
1. `@inject ICustomFieldService CustomFieldService`
2. State fields:
   ```csharp
   private List<CustomFieldDefinition> _customFieldDefs = new();
   private Dictionary<string, string> _customFieldValues = new();
   ```
3. In `OnInitializedAsync`:
   ```csharp
   _customFieldDefs = await CustomFieldService.GetFieldDefinitionsAsync("EntityType");
   // Load existing values from entity.CustomFieldValues JSON
   ```
4. A "Custom Fields" tab or section (only shown when `_customFieldDefs.Count > 0`):
   ```razor
   <CustomFieldsEditor FieldDefinitions="_customFieldDefs"
                       Values="_customFieldValues"
                       ValuesChanged="OnCustomFieldsChanged" />
   ```
5. On save, serialize back:
   ```csharp
   entity.CustomFieldValues = JsonSerializer.Serialize(_customFieldValues);
   ```

### 1. Add to Quotes/Edit.razor (H6.5)
- Entity type: `"Quote"`
- Add tab button + tab panel after existing tabs
- Load/save custom field values with the quote

### 2. Add to WorkOrders/Create.razor (H6.6)
- Entity type: `"WorkOrder"`
- Add section below existing form fields
- Save custom field values with the work order

### 3. Verify Parts/Edit.razor (H6.7)
- Entity type: `"Part"` — already implemented
- Verify it works: create a custom field for "Part" entity, check it renders

### 4. Add to Quality/Ncr.razor (H6.8)
- Entity type: `"NCR"`
- Add section in the NCR creation form
- Save custom field values with the NCR

### 5. Add to Inventory/Items.razor (H6.9)
- Entity type: `"InventoryItem"`
- Add section in the item create/edit form
- Save custom field values with the item

### 6. Display on detail/read pages (H6.10)
For each entity that has a detail view, add a read-only custom fields display.
Create a simple `<CustomFieldsDisplay>` component or inline the rendering:
```razor
@if (_customFieldValues.Any())
{
    <div class="card" style="margin-top: 12px;">
        <h3 class="card-title">Custom Fields</h3>
        @foreach (var kvp in _customFieldValues)
        {
            <div style="margin-bottom: 8px;">
                <span class="form-label">@kvp.Key</span>
                <div>@kvp.Value</div>
            </div>
        }
    </div>
}
```

Add to:
- `Quotes/Details.razor`
- `WorkOrders/Details.razor`
- `Parts/Detail.razor` (may already have it — verify)

---

## Verification

1. Build passes
2. Create a custom field definition for "Quote" entity in Admin → Custom Fields
3. Go to Quotes → Edit → see the custom field renders
4. Save → go to Quote Details → see the value displayed
5. Repeat for WorkOrder, NCR, InventoryItem

---

## Files Modified (fill in after completion)

- `Models/NonConformanceReport.cs` — Added `CustomFieldValues` property
- `Models/InventoryItem.cs` — Added `CustomFieldValues` property
- `Components/Pages/Quotes/Edit.razor` — Added ICustomFieldService inject, custom fields state, CustomFieldsEditor section, load/save wiring
- `Components/Pages/WorkOrders/Create.razor` — Added ICustomFieldService inject, custom fields state, CustomFieldsEditor section, load/save wiring
- `Components/Pages/Quality/Ncr.razor` — Added ICustomFieldService inject, custom fields state, CustomFieldsEditor section in modal, load/save wiring
- `Components/Pages/Inventory/Items.razor` — Added ICustomFieldService inject, custom fields state, CustomFieldsEditor section in modal, load/save wiring
- `Components/Pages/Quotes/Details.razor` — Added ICustomFieldService inject, custom fields display in summary tab
- `Components/Pages/WorkOrders/Details.razor` — Added ICustomFieldService inject, custom fields display in info tab
- `Components/Pages/Parts/Detail.razor` — Added custom fields display in overview tab, load logic in OnInitializedAsync
