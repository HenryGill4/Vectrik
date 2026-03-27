# Shop Floor Overhaul — Feature Plan

## Vision
Transform the operator shop floor from a basic work queue into a fully customizable, feature-rich production interface. Admins get granular control over what operators see and can do. Operators get richer data capture, better visibility, and smoother workflow.

---

## Phase 1: Stage Configuration Engine (Foundation)
**Goal**: Admins can configure what operators see and do per production stage.

### 1A. Stage UI Configuration (Admin → Stages)
Add a "Stage Configuration" tab to the existing Admin > Stages page:

- **Visible Sections**: Toggle which cards show on the stage page
  - Work Instructions panel (on/off)
  - Sign-off Checklist (on/off + configure items)
  - Machine Status card (on/off)
  - Part Drawing/Files panel (on/off)
  - Previous Stage Notes (on/off)
  - Timer/Duration display (on/off)
  - Material Lot Tracking (on/off)
  - Photo Capture (on/off)

- **Required Fields**: Mark which data fields are required before completion
  - Checklist all-signed (on/off)
  - Quality check (on/off)
  - Custom fields (per-field required flag)
  - Photo required (on/off)
  - Notes required (on/off)

- **Available Actions**: Toggle which buttons operators see
  - Pause (on/off, default on)
  - Fail/NCR (on/off, default on)
  - Batch Complete (on/off for build-level stages)
  - Transfer to Another Operator (on/off)
  - Log Delay (on/off)

Config stored as JSON in `ProductionStage.StageConfigJson` (new column).

### 1B. Operator Permission Overrides (Admin → Users)
Per-operator permission flags (stored on User model or separate table):

- `CanPauseWork` (default: true)
- `CanFailWork` (default: true)
- `CanCompleteWithoutChecklist` (default: false)
- `CanViewCostData` (default: false)
- `CanTransferWork` (default: false)
- `CanAccessAllStages` (default: false — uses AssignedStageIds)
- `MaxConcurrentJobs` (default: 1)

Admins configure these in the existing Users page under a new "Permissions" section.

---

## Phase 2: Rich Data Capture
**Goal**: Operators can capture more data types beyond text fields.

### 2A. Enhanced Custom Field Types
Extend the existing custom field system to support:

- **Photo/Image Capture**: Camera button → capture or upload → stored as file, thumbnail shown
- **Measurement Entry**: Numeric with units (mm, µm, °C, etc.) + tolerance range (green/yellow/red)
- **Serial Number Scanner**: Text field with barcode scan button (camera-based)
- **Material Lot Selector**: Dropdown pulling from Inventory module
- **File Attachment**: Upload PDFs, images, measurement reports
- **Signature Pad**: Touch/mouse signature capture for sign-offs
- **Multi-Select Checklist**: Configurable checklist (not just sign-off — data capture)

### 2B. Stage-Specific Form Builder (Admin)
Visual form designer in Admin → Stages → [Stage] → Form Designer:

- Drag-and-drop field placement
- Field types: text, number, dropdown, checkbox, photo, measurement, serial, file, signature
- Field properties: label, required, default value, validation rules, help text
- Layout: single column or two-column sections
- Preview mode: see what operator will see
- Form definitions stored as JSON in `ProcessStage.FormDefinitionJson`

---

## Phase 3: Operator Visibility
**Goal**: Operators see everything they need without leaving the stage page.

### 3A. Inline Work Instructions
- Work instructions render directly on the stage page (not a separate modal)
- Support rich text (HTML), images, step-by-step with checkboxes
- Version-aware: shows instructions for the current process stage
- Collapsible panel — operator can hide after reading

### 3B. Part Context Panel
Expandable panel showing:
- Part drawing (image/PDF viewer)
- Previous stage notes and completion data
- Part specifications (material, dimensions, tolerances)
- Work order context (customer, PO#, due date, priority)
- Build plate position (for SLS parts)

### 3C. Machine Status Integration
Live machine status card (where applicable):
- Current state (Running/Idle/Down/Changeover)
- Current job progress (if auto-tracked)
- Temperature/sensor readings (from MachineSyncService)
- Maintenance alerts
- Next scheduled maintenance

### 3D. Shift Schedule Visibility
Show current operator:
- Current shift hours remaining
- Upcoming break times
- Who's on next shift
- Handoff notes from previous shift

---

## Phase 4: Workflow Improvements
**Goal**: Smoother operator flow, less clicking, faster throughput.

### 4A. Auto-Advance
After completing a stage execution:
- Automatically load the next item in queue (configurable)
- Or show "No more work" card with shift summary
- Configurable per stage: auto-advance on/off

### 4B. Batch Operations
- Select multiple queue items → Start All
- Batch complete (already exists for build-level, extend to part-level)
- Batch transfer to another operator
- Batch priority change

### 4C. Barcode/QR Integration
- Scan-to-start: scan part label → auto-find and start that execution
- Scan-to-complete: scan → complete current work on that part
- Print labels: generate QR codes for parts/builds

### 4D. Time Tracking Enhancements
- Setup vs. Run time split (per execution)
- Break time auto-detection (shift-aware)
- Idle time alerts (configurable threshold)
- Time estimate accuracy tracking (actual vs estimated, historical)

---

## Phase 5: Page Layout Builder (WYSIWYG)
**Goal**: Admins visually design the stage page layout.

### 5A. Layout Engine
Widget-based page composer:
- **Available Widgets**: Queue, Active Work, History, Work Instructions, Machine Status, Part Context, Custom Form, Timer, Checklist, Notes, Photo Gallery, Shift Info
- **Grid Layout**: Drag widgets into a responsive grid (1-3 columns)
- **Widget Sizing**: Small/Medium/Large per widget
- **Widget Config**: Each widget has its own settings (e.g., History widget: how many rows to show)

### 5B. Admin Layout Designer
Visual editor in Admin → Stages → [Stage] → Page Layout:
- Drag-and-drop widget placement
- Live preview with sample data
- Mobile vs. desktop layout toggle
- Save as template → apply to multiple stages
- Default layout provided (current layout as baseline)

Layout stored as JSON in `ProcessStage.PageLayoutJson`.

### 5C. Template Library
Pre-built layouts:
- "SLS Build Monitor" — large machine status, small queue
- "Manual Machining" — large form, work instructions, timer
- "Quality Inspection" — measurement form, photo capture, previous notes
- "Simple Stage" — queue + complete button (minimal)

---

## Implementation Order (Recommended)

| Phase | Effort | Impact | Dependencies |
|-------|--------|--------|-------------|
| **1A** Stage UI Config | Medium | High | None — starts delivering value immediately |
| **1B** Operator Permissions | Small | Medium | None |
| **2A** Enhanced Field Types | Medium | High | 1A (config drives which fields show) |
| **3A** Inline Work Instructions | Small | High | None |
| **3B** Part Context Panel | Small | High | None |
| **4A** Auto-Advance | Small | Medium | None |
| **2B** Form Builder | Large | Very High | 2A (needs field types first) |
| **3C** Machine Status | Medium | Medium | MachineSyncService (exists) |
| **4B** Batch Operations | Medium | Medium | None |
| **4D** Time Tracking | Medium | Medium | None |
| **5A-C** Layout Builder | Very Large | Very High | 2B (form builder proves the pattern) |
| **4C** Barcode/QR | Medium | Medium | None |
| **3D** Shift Schedule | Small | Low | ShiftManagementService (exists) |

---

## Technical Notes

- **Config storage**: JSON columns on existing models (no new tables for Phase 1-2)
- **Form builder**: Reuse existing `dynamic-form-renderer.js` pattern
- **Layout builder**: New JS module, similar to Gantt viewport approach
- **Backward compatible**: All new features are opt-in via config. Existing stages work unchanged.
- **Migration**: New columns added with defaults, existing data preserved
