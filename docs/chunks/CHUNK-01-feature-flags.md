# CHUNK-01: Feature Flag Gating

> **Size**: M (Medium) — ~8-12 file edits
> **ROADMAP tasks**: H6.1, H6.2, H6.3, H6.4
> **Prerequisites**: H1-H5 complete

---

## Scope

Wire `ITenantFeatureService.IsEnabled()` into the NavMenu and all feature-area
pages so that disabled modules are hidden from navigation and show a "not enabled"
message if accessed directly by URL.

---

## Files to Read First

| File | Why |
|------|-----|
| `Components/Layout/NavMenu.razor` | Wrap each nav section with feature checks |
| `Services/TenantFeatureService.cs` | Understand available IsEnabled() keys |
| `Components/Pages/Admin/Features.razor` | See what feature keys exist |
| `Components/Pages/Builds/Index.razor` | Example SLS-gated page |
| `Components/Pages/Inventory/Dashboard.razor` | Example module page |

---

## Tasks

### 1. Create `FeatureGate.razor` shared component
**File**: `Components/Shared/FeatureGate.razor`

A wrapper component that checks a feature key and either renders child content
or shows a "module not enabled" card. Usage:
```razor
<FeatureGate Feature="inventory">
    @* inventory page content *@
</FeatureGate>
```

Parameters:
- `string Feature` — the feature key to check
- `string ModuleName` — display name (e.g., "Inventory Management")
- `RenderFragment ChildContent` — the page content

When disabled, show:
```
┌─────────────────────────────────┐
│  🔒 Module Not Enabled          │
│                                 │
│  Inventory Management is not    │
│  enabled for your organization. │
│                                 │
│  Contact your administrator to  │
│  enable this module.            │
│                                 │
│  [Go to Admin → Features]       │
└─────────────────────────────────┘
```

### 2. Wrap NavMenu sections with feature checks
**File**: `Components/Layout/NavMenu.razor`

Inject `ITenantFeatureService` and wrap each nav section:
- **SHOP FLOOR** section: always visible (core module)
- **SCHEDULING** section: always visible (core module)
- **WORK ORDERS** section: always visible (core module)
- **QUOTES** section: always visible (core module)
- **PARTS** section: always visible (core module)
- **QUALITY** section: `Features.IsEnabled("quality")`
- **INVENTORY** section: `Features.IsEnabled("inventory")`
- **BUILDS** section: `Features.IsEnabled("sls")`
- **TRACKING** section: `Features.IsEnabled("sls")` or always visible
- **MAINTENANCE** section: `Features.IsEnabled("maintenance")`
- **ANALYTICS** section: always visible (core module)
- **ADMIN** section: always visible (role-gated already)

### 3. Add FeatureGate to module pages
Add `<FeatureGate>` wrapper to:
- `Inventory/Dashboard.razor` — feature="inventory"
- `Inventory/Items.razor` — feature="inventory"
- `Inventory/Ledger.razor` — feature="inventory"
- `Inventory/Receive.razor` — feature="inventory"
- `Quality/Dashboard.razor` — feature="quality"
- `Quality/Ncr.razor` — feature="quality"
- `Quality/Capa.razor` — feature="quality"
- `Quality/Spc.razor` — feature="quality"
- `Builds/Index.razor` — feature="sls"
- `Maintenance/Index.razor` — feature="maintenance"
- `Maintenance/WorkOrders.razor` — feature="maintenance"
- `Maintenance/Rules.razor` — feature="maintenance"

### 4. Gate DLMS-specific fields (H6.3)
In pages that have defense/DLMS fields, wrap those sections with
`@if (Features.IsEnabled("dlms"))`. Currently applies to:
- `Parts/Edit.razor` — Defense/DLMS tab (already gated via `_showDlms` — verify)

### 5. Gate SLS-specific features (H6.4)
In pages that have SLS-specific features, verify gating:
- `Parts/Edit.razor` — Stacking + Batch tabs (already gated via `_showSlsFeatures` — verify)
- `Builds/Index.razor` — entire page (add FeatureGate)

---

## Verification

1. Build passes
2. Disable "inventory" feature → Inventory nav links hidden, `/inventory` shows guard
3. Disable "quality" feature → Quality nav links hidden, `/quality` shows guard
4. Disable "sls" feature → Builds nav hidden, SLS tabs hidden on part edit
5. Disable "dlms" feature → Defense tab hidden on part edit

---

## Files Modified (fill in after completion)

- `Components/Shared/FeatureGate.razor` — **Created** shared component
- `Components/Layout/NavMenu.razor` — Injected `ITenantFeatureService`, gated Builds (sls), Maintenance (module.maintenance), Inventory (module.inventory), Quality (module.quality) nav sections
- `Components/Pages/Inventory/Dashboard.razor` — Added `<FeatureGate Feature="module.inventory">`
- `Components/Pages/Inventory/Items.razor` — Added `<FeatureGate Feature="module.inventory">`
- `Components/Pages/Inventory/Ledger.razor` — Added `<FeatureGate Feature="module.inventory">`
- `Components/Pages/Inventory/Receive.razor` — Added `<FeatureGate Feature="module.inventory">`
- `Components/Pages/Quality/Dashboard.razor` — Added `<FeatureGate Feature="module.quality">`
- `Components/Pages/Quality/Ncr.razor` — Added `<FeatureGate Feature="module.quality">`
- `Components/Pages/Quality/Capa.razor` — Added `<FeatureGate Feature="module.quality">`
- `Components/Pages/Quality/Spc.razor` — Added `<FeatureGate Feature="module.quality">`
- `Components/Pages/Builds/Index.razor` — Added `<FeatureGate Feature="sls">`
- `Components/Pages/Maintenance/Index.razor` — Added `<FeatureGate Feature="module.maintenance">`
- `Components/Pages/Maintenance/WorkOrders.razor` — Added `<FeatureGate Feature="module.maintenance">`
- `Components/Pages/Maintenance/Rules.razor` — Added `<FeatureGate Feature="module.maintenance">`

### Notes
- H6.3 (DLMS gating) and H6.4 (SLS gating) already implemented in `Parts/Edit.razor` — verified `_showDlms` and `_showSlsFeatures` driven by `Features.IsEnabled("dlms")` and `Features.IsEnabled("sls")`
