# DLMS Integration & Customer Customization Architecture

> **Purpose**: This document defines how Opcentrix V3 integrates with Defense
> Logistics Management Standards (DLMS) workflows and provides per-tenant
> customization tools so each customer can tailor the system to their shop
> without forking the codebase.

---

## Part 1: DLMS Integration

### What Is DLMS?

DLMS (Defense Logistics Management Standards) governs how defense contractors
and the DoD exchange logistics data — requisitions, shipment status, receipts,
invoices, financial transactions, and property accountability. For precision CNC
shops selling to defense primes (Lockheed, Raytheon, Northrop, etc.) or directly
to the DoD, DLMS compliance is a contract requirement.

### DLMS Transaction Sets We Must Support

| DLMS Transaction | EDI Equivalent | Where It Touches Opcentrix | Module |
|------------------|---------------|---------------------------|--------|
| **Requisition** (MILSTRIP) | 511 | Work Order creation from customer EDI feed | M02 |
| **Shipment Status** (DLMS 856) | 856/ASN | Shipping — generate ASN with IUID marking | M15 |
| **Receipt Acknowledgment** | 527 | Receiving — confirm inbound material receipt | M06 |
| **Material Inspection/Receiving** | 842 | Quality — incoming inspection results | M05 |
| **Invoice** (WAWF integration) | 810 | Job Costing — invoice submission via WAWF | M09 |
| **Contract Data (CDRL)** | N/A | Document Control — CDRL deliverables tracking | M14 |
| **Property Accountability** | 527R | Inventory — Government Furnished Material (GFM/GFE) tracking | M06 |
| **First Article Test Report** | N/A | Quality — AS9102 FAIR with DLMS traceability | M05 |

### Key DLMS Concepts That Affect Our Models

#### 1. IUID (Item Unique Identification)
DoD requires unique item identification for items above $5,000 or mission-critical.
IUID uses a 2D Data Matrix barcode with a Unique Item Identifier (UII) constructed
from enterprise identifier + part number + serial number.

**Impact**: `PartInstance` model needs:
- `UiiCode` — Unique Item Identifier (Construct 1 or 2)
- `IuidRegistered` — whether registered in the DoD IUID Registry
- `IuidRegisteredAt` — timestamp of registration
- `DataMatrixBarcode` — generated 2D barcode data string

#### 2. Government Furnished Material/Equipment (GFM/GFE)
Defense contracts often supply raw material or tooling that must be tracked
separately with full chain-of-custody accountability.

**Impact**: `InventoryItem` needs:
- `IsGovernmentFurnished` — GFM/GFE flag
- `ContractNumber` — the contract under which it was furnished
- `AccountabilityCode` — DoD accountability coding
- `CustodianUserId` — assigned custodian

#### 3. Wide Area Workflow (WAWF)
Electronic invoicing portal required for all DoD contracts. Our system should
generate WAWF-ready invoice data packages.

**Impact**: `Shipment` model needs:
- `WawfDocumentNumber` — WAWF receiving report number
- `ContractNumber` — DFARS contract reference
- `ContractLineItem` (CLIN) — contract line item number
- `DoDAAC` — DoD Activity Address Code for ship-to

#### 4. Contract Data Requirements Lists (CDRLs)
Every defense contract has required deliverables (inspection reports, test data,
material certs, process records). These are due at specific milestones.

**Impact**: `ControlledDocument` needs:
- `IsCdrl` — flag for CDRL deliverables
- `CdrlNumber` — DI-XXXX-XXXXX number
- `ContractNumber` — linked contract
- `DueDate` — contractual due date
- `DeliveryStatus` — Pending, Submitted, Accepted, Rejected

#### 5. AS9102 First Article Inspection (FAIR)
Standard format for first article reports in aerospace/defense. Requires
Forms 1 (Part Number Accountability), 2 (Product Accountability — Raw Material),
and 3 (Characteristic Accountability — Measurements).

**Impact**: Already partially covered in Module 05 `IsFair` flag, but needs:
- Structured AS9102 Form 1/2/3 data models
- Auto-population from part routing + material + measurements
- PDF export in standard AS9102 format

---

## Part 2: Customer Customization Architecture

### The Problem
Every CNC shop runs differently. One shop tracks 6 production stages, another
tracks 15. One needs ITAR compliance, another needs ISO 13485 for medical.
Hardcoding workflows kills adoptability.

### Customization Layers

We provide 5 layers of customization, from lightest to deepest:

#### Layer 1: System Settings (Already Exists ✅)
`SystemSetting` key-value store with categories. Tenants configure behavior
without code changes.

**New settings keys needed across modules:**

| Key | Category | Default | Purpose |
|-----|----------|---------|---------|
| `company.name` | Branding | "" | Company name on all documents |
| `company.logo_url` | Branding | "" | Logo for reports/packing lists |
| `company.address` | Branding | "" | Address for documents |
| `company.cage_code` | Defense | "" | CAGE code for DLMS transactions |
| `company.dodaac` | Defense | "" | DoD Activity Address Code |
| `company.duns` | Defense | "" | DUNS/SAM UEI number |
| `numbering.wo_prefix` | Numbering | "WO" | Work order number prefix |
| `numbering.wo_digits` | Numbering | "5" | Digits in WO number |
| `numbering.quote_prefix` | Numbering | "QT" | Quote number prefix |
| `numbering.shipment_prefix` | Numbering | "SHP" | Shipment number prefix |
| `numbering.ncr_prefix` | Numbering | "NCR" | NCR number prefix |
| `numbering.po_prefix` | Numbering | "PO" | Purchase order prefix |
| `numbering.part_auto` | Numbering | "false" | Auto-generate part numbers |
| `numbering.serial_format` | Numbering | "{PartNumber}-{Seq:0000}" | Serial number template |
| `quality.require_fair` | Quality | "false" | Require FAIR on first articles |
| `quality.spc_default_subgroup` | Quality | "5" | Default SPC subgroup size |
| `quality.ncr_require_approval` | Quality | "true" | NCR needs manager approval |
| `shipping.require_coc` | Shipping | "true" | Require Certificate of Conformance |
| `shipping.default_carrier` | Shipping | "" | Default carrier name |
| `shipping.generate_asn` | Shipping | "false" | Auto-generate ASN (DLMS 856) |
| `inventory.track_lots` | Inventory | "true" | Enable lot tracking |
| `inventory.track_gfm` | Inventory | "false" | Enable GFM/GFE tracking |
| `costing.overhead_method` | Costing | "percentage" | "percentage" or "activity" |
| `costing.default_margin_pct` | Costing | "30" | Default quote margin |
| `dlms.enabled` | Defense | "false" | Enable DLMS transaction features |
| `dlms.iuid_construct` | Defense | "1" | IUID construct type (1 or 2) |
| `dlms.wawf_enabled` | Defense | "false" | Enable WAWF invoice generation |
| `compliance.frameworks` | Compliance | "[]" | Active framework codes JSON |
| `workflow.wo_auto_release` | Workflow | "false" | Auto-release WOs from quotes |
| `workflow.require_job_approval` | Workflow | "false" | Jobs need approval before start |
| `workflow.stage_pause_allowed` | Workflow | "true" | Allow operators to pause stages |

#### Layer 2: Custom Fields (Already Partially Built ✅)
`ProductionStage.CustomFieldsConfig` stores JSON field definitions. Operators
fill `StageExecution.CustomFieldValues` at runtime.

**Extend to all major entities:**

Every major model gets a `CustomFieldValues` JSON column and corresponding
`CustomFieldsConfig` on its "type" record:

| Entity | Config Location | Runtime Values |
|--------|----------------|---------------|
| `StageExecution` | `ProductionStage.CustomFieldsConfig` | `StageExecution.CustomFieldValues` ✅ |
| `WorkOrder` | `SystemSetting[custom_fields.work_order]` | `WorkOrder.CustomFieldValues` (new) |
| `Quote` | `SystemSetting[custom_fields.quote]` | `Quote.CustomFieldValues` (new) |
| `Part` | `SystemSetting[custom_fields.part]` | `Part.CustomFieldValues` (new) |
| `InventoryItem` | `SystemSetting[custom_fields.inventory]` | `InventoryItem.CustomFieldValues` (new) |
| `QCInspection` | `SystemSetting[custom_fields.inspection]` | `QCInspection.CustomFieldValues` (new) |
| `PurchaseOrder` | `SystemSetting[custom_fields.purchase_order]` | `PurchaseOrder.CustomFieldValues` (new) |
| `Vendor` | `SystemSetting[custom_fields.vendor]` | `Vendor.CustomFieldValues` (new) |
| `Shipment` | `SystemSetting[custom_fields.shipment]` | `Shipment.CustomFieldValues` (new) |

**Admin UI**: `/admin/custom-fields` — visual field designer per entity type.
Uses the existing `CustomFieldDefinition` record type. Fields support:
- `text`, `number`, `decimal`, `date`, `select`, `multiselect`, `checkbox`, `textarea`
- Required/optional flag
- Validation rules (min, max, regex)
- Conditional visibility (show field X only when field Y = value Z)

**Shared Razor Component**: `<CustomFieldsEditor>` — auto-renders fields from
config JSON, binds values to the entity's `CustomFieldValues` column.

#### Layer 3: Configurable Workflows (New)
Allow tenants to define their own approval chains, status transitions, and
automated triggers without code changes.

**New Model: `WorkflowDefinition`**
```
WorkflowDefinition
├── Id, EntityType (WorkOrder, Quote, NCR, PO, Document)
├── TriggerEvent (StatusChange, Create, FieldUpdate)
├── Conditions (JSON — field comparisons)
├── Steps: List<WorkflowStep>
│   ├── StepOrder
│   ├── ActionType (RequireApproval, SendNotification, SetField, CreateTask)
│   ├── AssignToRole / AssignToUserId
│   ├── Config (JSON — action-specific settings)
│   └── TimeoutHours (escalation)
└── IsActive
```

**Built-in workflow templates** (customers can modify):
- Quote approval chain (Estimator → Engineering → Manager)
- Work order release approval
- NCR disposition approval (Quality → Engineering → Customer)
- PO approval by dollar threshold
- Document revision approval (Author → Reviewer → Approver)
- FAIR review and acceptance

**Admin UI**: `/admin/workflows` — visual workflow builder:
- Drag-drop step ordering
- Role/user assignment per step
- Condition builder (if amount > $10,000, require VP approval)
- Notification templates per step
- Test mode (simulate a workflow run)

#### Layer 4: Configurable Document Templates (New)
Customers need to brand and customize printed documents (quotes, packing lists,
BOL, CoC, inspection reports, FAIR forms).

**New Model: `DocumentTemplate`**
```
DocumentTemplate
├── Id, Name, EntityType (Quote, PackingList, BOL, CoC, FAIR, NCR, PO)
├── TemplateHtml (Handlebars-style HTML with {{merge_fields}})
├── HeaderHtml, FooterHtml
├── CssOverrides
├── PageSize (Letter, A4, Custom)
├── Orientation (Portrait, Landscape)
├── IsDefault
├── Version
└── LastModifiedBy, LastModifiedAt
```

**Merge fields** are auto-populated from entity data:
```
{{company.name}}, {{company.logo_url}}, {{company.cage_code}}
{{workorder.number}}, {{workorder.customer_name}}, {{workorder.customer_po}}
{{shipment.tracking}}, {{shipment.lines[].part_number}}
{{#each lines}}...{{/each}}
```

**Admin UI**: `/admin/templates` — template editor with:
- Live preview pane
- Merge field picker (shows all available fields per entity type)
- HTML editor with syntax highlighting
- CSS customization panel
- Default template library (professional starting templates)

#### Layer 5: Tenant Feature Flags (New)
Allow enabling/disabling entire modules per tenant. Not every shop needs
every module. Smaller shops might only use Quotes + Work Orders + Shop Floor.

**New Model: `TenantFeatureFlag`**
```
TenantFeatureFlag
├── Id, TenantCode (or on Tenant model as JSON)
├── FeatureKey (e.g., "module.quality", "module.inventory", "dlms", "spc")
├── IsEnabled
├── EnabledAt, EnabledByUserId
```

**Or simpler**: Add `EnabledFeatures` JSON column to `Tenant` model in
PlatformDbContext, with a `TenantFeatureService` that checks flags.

```csharp
public class TenantFeatureService : ITenantFeatureService
{
    public bool IsEnabled(string featureKey);    // check current tenant
    public List<string> GetEnabledFeatures();
}
```

**Features that can be toggled:**
| Feature Key | Default | Controls |
|-------------|---------|----------|
| `module.quoting` | on | Quotes section in nav |
| `module.quality` | on | Quality pages + inspection |
| `module.inventory` | on | Inventory tracking |
| `module.spc` | off | SPC charts (advanced) |
| `module.toolcrib` | off | Tool management |
| `module.purchasing` | on | PO management |
| `module.documents` | on | Document control |
| `module.shipping` | on | Shipping module |
| `module.crm` | off | CRM module |
| `module.compliance` | off | Compliance frameworks |
| `module.training` | off | LMS module |
| `dlms` | off | DLMS transaction features |
| `dlms.iuid` | off | IUID marking and registry |
| `dlms.wawf` | off | WAWF invoice generation |
| `dlms.gfm` | off | GFM/GFE tracking |
| `dlms.cdrl` | off | CDRL deliverable tracking |
| `portal` | off | Customer portal |
| `api` | off | REST API access |

**NavMenu integration**: NavMenu checks feature flags to show/hide sections:
```razor
@if (Features.IsEnabled("module.quality"))
{
    <NavLink href="quality">Quality</NavLink>
}
```

---

## Part 3: Module-Specific DLMS & Customization Additions

### Module 02 — Work Orders
- Add `ContractNumber`, `ContractLineItem` (CLIN) fields for defense contracts
- Add `IsDefenseContract` flag to toggle DLMS-specific fields
- Add `CustomFieldValues` JSON column
- Number format configurable via `SystemSetting`

### Module 05 — Quality
- Add structured AS9102 FAIR data (Forms 1/2/3)
- FAIR auto-populates from part routing + material certs + measurements
- PDF export in standard AS9102 format
- Add `CustomFieldValues` to `QCInspection`
- Inspection plan templates are per-tenant customizable

### Module 06 — Inventory
- Add `IsGovernmentFurnished`, `ContractNumber`, `AccountabilityCode` to `InventoryItem`
- GFM/GFE items have separate accountability tracking
- Lot tracking toggleable via feature flag
- Add `CustomFieldValues` to `InventoryItem`

### Module 09 — Job Costing
- WAWF invoice data package generation
- Cost rollup format configurable (DoD vs. commercial)
- Overhead method selectable per tenant (percentage vs. activity-based)

### Module 14 — Document Control
- Add CDRL tracking fields (`IsCdrl`, `CdrlNumber`, `ContractNumber`, `DueDate`)
- CDRL deliverable dashboard — what's due, what's submitted, what's overdue
- Document templates customizable per tenant

### Module 15 — Shipping
- ASN (Advanced Shipment Notice) generation for DLMS 856
- WAWF receiving report number field
- DoDAAC ship-to codes
- Certificate of Conformance (CoC) auto-generation
- Packing list / BOL templates customizable per tenant
- IUID data matrix barcode printing on labels

### Module 17 — Compliance
- DFARS clause tracking (252.204-7012, etc.)
- ITAR/EAR classification per part
- CUI marking enforcement
- Framework templates pre-seeded but customizable

### PartInstance (Cross-Module)
- Add IUID fields (`UiiCode`, `IuidRegistered`, `DataMatrixBarcode`)
- Auto-generate UII from enterprise identifier + part + serial
- Barcode rendering for labels

---

## Part 4: New Admin Pages for Customization

These pages give tenant admins self-service configuration tools:

| Page | Route | Purpose |
|------|-------|---------|
| Custom Fields Designer | `/admin/custom-fields` | Define custom fields per entity type |
| Workflow Builder | `/admin/workflows` | Configure approval chains and automation |
| Template Editor | `/admin/templates` | Customize document templates |
| Number Sequences | `/admin/numbering` | Configure number formats and prefixes |
| Feature Flags | `/admin/features` | Enable/disable modules and DLMS features |
| DLMS Settings | `/admin/dlms` | CAGE code, DoDAAC, IUID config, WAWF setup |
| Branding | `/admin/branding` | Company logo, colors, report headers |

---

## Part 5: New Models Summary

| Model | Purpose | DbContext | Added In |
|-------|---------|-----------|----------|
| `WorkflowDefinition` | Configurable approval/automation workflows | Tenant | Stage 1 (foundation) |
| `WorkflowStep` | Steps within a workflow | Tenant | Stage 1 (foundation) |
| `WorkflowInstance` | Runtime workflow execution tracker | Tenant | Stage 1 (foundation) |
| `DocumentTemplate` | Customizable print templates | Tenant | Stage 1 (foundation) |
| `TenantFeatureFlag` | Per-tenant feature toggling | Platform | Stage 1 (foundation) |
| `CustomFieldConfig` | Custom field definitions per entity type | Tenant | Stage 1 (foundation) |
| `FairForm1` | AS9102 Part Number Accountability | Tenant | Stage 6 (Quality) |
| `FairForm2` | AS9102 Product Accountability (materials) | Tenant | Stage 6 (Quality) |
| `FairForm3` | AS9102 Characteristic Accountability (dims) | Tenant | Stage 6 (Quality) |

---

## Part 6: New Services Summary

| Service | Purpose |
|---------|---------|
| `ITenantFeatureService` / `TenantFeatureService` | Check feature flags for current tenant |
| `IWorkflowEngine` / `WorkflowEngine` | Execute configurable workflows |
| `IDocumentTemplateService` / `DocumentTemplateService` | Render templates with merge fields |
| `ICustomFieldService` / `CustomFieldService` | Manage custom field configs, validate values |
| `INumberSequenceService` / `NumberSequenceService` | Generate configurable sequential numbers |
| `IDlmsService` / `DlmsService` | DLMS transaction generation (ASN, WAWF, IUID) |
| `IIuidService` / `IuidService` | IUID code generation and registry integration |

---

## Part 7: Implementation Timing

These customization features should be built **before or alongside** Stage 1,
because every module downstream benefits from them:

| Item | Build When | Rationale |
|------|-----------|-----------|
| `TenantFeatureFlag` + `TenantFeatureService` | Stage 0.5 (pre-Stage 1) | NavMenu and every page needs it |
| `CustomFieldConfig` + `CustomFieldService` + `<CustomFieldsEditor>` | Stage 0.5 | Used in every entity from Stage 1 onward |
| `NumberSequenceService` | Stage 0.5 | WO, Quote, NCR, PO numbers from Stage 1 |
| System settings seeding (all keys above) | Stage 0.5 | Needed by all modules |
| `DocumentTemplate` + `DocumentTemplateService` | Stage 2 (Quoting) | First document output is quote PDF |
| `WorkflowDefinition` + `WorkflowEngine` | Stage 3 (Work Orders) | First approval workflow is WO release |
| DLMS fields on models | Alongside each module | Add DLMS-specific columns when building each module |
| `DlmsService` + `IuidService` | Stage 16 (Shipping) | First DLMS transaction output is the ASN |
| FAIR structured data | Stage 6 (Quality) | First FAIR generation happens during inspection |

---

## Part 8: Customization UX Philosophy

1. **Smart Defaults** — System works out of the box with sensible defaults.
   A shop should be able to start using Opcentrix without configuring anything.

2. **Progressive Disclosure** — Advanced features (DLMS, SPC, workflows) are
   hidden behind feature flags. Only show complexity when the customer opts in.

3. **No Code Required** — All customization is UI-driven. Custom fields,
   workflows, templates, and feature flags are managed via admin pages.

4. **Template Library** — Ship with professional default templates for all
   document types. Customers clone and modify, never start from blank.

5. **Import/Export Configs** — Customers can export their settings, custom fields,
   workflows, and templates as JSON. This allows:
   - Backup/restore of configuration
   - Sharing configs between tenants
   - Our support team can send a customer a config package

---

*This document is referenced by MASTER_CONTEXT.md and the STAGED-IMPLEMENTATION-PLAN.md.
Every module implementation should check this document for DLMS fields and
customization columns to add to each model.*
