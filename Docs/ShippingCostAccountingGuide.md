# Shipping Cost Accounting Guide
## InventorySystem — Freight-Out Treatment: UI and Accounting Reference

**Last Updated:** February 19, 2026  
**Accounting Basis:** Cash Basis  
**Branch:** master  

---

## 1. Overview

Every outbound shipment involves two distinct financial events that must be tracked separately:

| Event | Description | When Recorded (Cash Basis) |
|---|---|---|
| **Freight-Out Expense** | What you actually pay FedEx / UPS to move the package | When carrier invoice is **paid** |
| **Shipping Revenue** | What you charge the customer on the invoice | When customer **pays** the invoice |

### Key Design Decisions

- `Sale.ShippingCost` = **what is charged to the customer** on the invoice. This field already exists and is unchanged.
- `Shipment.ActualCarrierCost` = **what was paid to the carrier**. This is a new field — internal only, never shown to customers.
- Because this is a **cash basis** system, **no GL entries are made at shipment time**. The shipment record stores cost data only. GL entries are created only when cash moves.
- No accruals, no `2200 Accrued Expenses` entries, no two-step pending/paid workflow for freight.

---

## 2. Required Data Model Changes

### 2.1 New Enum — `ShippingAccountType`

**File:** `Models\Enums\ShippingAccountType.cs` *(new file)*

```csharp
public enum ShippingAccountType
{
    OurAccount,
    CustomerAccount
}
```

### 2.2 Updated `Shipment` Fields

**File:** `Models\Shipment.cs`

| Field | Type | Purpose |
|---|---|---|
| `ActualCarrierCost` | `decimal?` | The real amount paid to FedEx/UPS at time of shipment |
| `ShippingAccountType` | `ShippingAccountType` (enum) | `OurAccount` or `CustomerAccount` |
| `FreightOutExpenseId` | `int?` | FK to the `Expense` record auto-created for this shipment |

### 2.3 New GL Account — `4300 Shipping Revenue`

**File:** `Models\Accounting\DefaultChartOfAccounts.cs`

Add in the Revenue (4000–4999) section, after account `4200 Custom Manufacturing`:

````````
    /// <summary>
    /// Income account for shipping and handling revenue.
    /// </summary>
    public const string ShippingRevenue = "4300";

    /// <summary>
    /// Description for the shipping revenue account.
    /// </summary>
    public const string ShippingRevenueDescription = "Shipping Revenue";

---

## 3. The Four Shipping Scenarios

### Scenario A — Customer's Carrier Account

> We ship using the **customer's** FedEx/UPS account number. The carrier bills them directly. We have zero cost and charge nothing.

| Field | Value |
|---|---|
| `ShippingAccountType` | `CustomerAccount` |
| `ActualCarrierCost` | `$0.00` (locked in UI) |
| `Sale.ShippingCost` | `$0.00` |
| GL entries | **None — ever** |
| `FreightOutExpensePaymentId` | Remains `null` |

---

### Scenario B — We Pay, Customer Not Charged (Free Shipping)

> We prepay the carrier. `Sale.ShippingCost = 0`. Full carrier cost is a business expense.

**Example:** Carrier cost = `$18.50`, Customer charged = `$0.00`

| Field | Value |
|---|---|
| `ShippingAccountType` | `OurAccount` |
| `ActualCarrierCost` | `$18.50` (recorded at shipment, no GL yet) |
| `Sale.ShippingCost` | `$0.00` |
| GL entry at shipment | **None** |

**GL entry when carrier invoice is paid:**

| Account | Debit | Credit |
|---|---|---|
| **6500 Freight-Out** | $18.50 | |
| **1010 Checking Account** | | $18.50 |

**Net shipping P&L:** –$18.50

---

### Scenario C — We Pay, Under-Recovery (Customer Charged Less Than Cost)

> We prepay the carrier. We charge the customer less than our actual cost.

**Example:** Carrier cost = `$18.50`, Customer charged = `$12.00`

| Field | Value |
|---|---|
| `ShippingAccountType` | `OurAccount` |
| `ActualCarrierCost` | `$18.50` (recorded at shipment, no GL yet) |
| `Sale.ShippingCost` | `$12.00` |
| GL entry at shipment | **None** |

**GL entry when customer pays invoice:**

| Account | Debit | Credit |
|---|---|---|
| **1010 Checking Account** | $12.00 | |
| **4300 Shipping Revenue** | | $12.00 |

*(The $12.00 shipping is part of the total customer payment receipt — no separate entry needed if the full invoice is paid in one transaction.)*

**GL entry when carrier invoice is paid:**

| Account | Debit | Credit |
|---|---|---|
| **6500 Freight-Out** | $18.50 | |
| **1010 Checking Account** | | $18.50 |

**Net shipping P&L:** $12.00 revenue – $18.50 expense = **–$6.50**

---

### Scenario D — We Pay, Over-Recovery (Customer Charged More Than Cost)

> We prepay the carrier. We charge the customer more than our cost (markup or handling fee).

**Example:** Carrier cost = `$18.50`, Customer charged = `$25.00`

| Field | Value |
|---|---|
| `ShippingAccountType` | `OurAccount` |
| `ActualCarrierCost` | `$18.50` (recorded at shipment, no GL yet) |
| `Sale.ShippingCost` | `$25.00` |
| GL entry at shipment | **None** |

**GL entry when customer pays invoice:**

| Account | Debit | Credit |
|---|---|---|
| **1010 Checking Account** | $25.00 | |
| **4300 Shipping Revenue** | | $25.00 |

**GL entry when carrier invoice is paid:**

| Account | Debit | Credit |
|---|---|---|
| **6500 Freight-Out** | $18.50 | |
| **1010 Checking Account** | | $18.50 |

**Net shipping P&L:** $25.00 revenue – $18.50 expense = **+$6.50**

---

## 4. Cash Basis Recording Rules

> **This system is cash basis.** No accruals are created. No entries are made at shipment time.

### Step 1 — At Shipment Time (Data Capture Only)

When `ProcessSaleWithShipping` or `CreateAdditionalShipment` (POST) fires:

- `Shipment.ShippingAccountType` is saved
- `Shipment.ActualCarrierCost` is saved (if `OurAccount`)
- `Shipment.FreightOutExpensePaymentId` = `null`
- **No GL entry. No `Expense` record. No `ExpensePayment` record.**

### Step 2 — When the Carrier Invoice Arrives and Is Paid

The user navigates to **Expenses → Record Payment**, selects or creates a `ShippingOut` expense type, enters the carrier amount, and submits.

The system creates one journal entry:

| Account | Debit | Credit |
|---|---|---|
| **6500 Freight-Out** | `ActualCarrierCost` | |
| **1010 Checking Account** | | `ActualCarrierCost` |

After saving, `Shipment.FreightOutExpensePaymentId` is set to the new `ExpensePayment.Id` by matching on `TrackingNumber` = `ExpensePayment.ReferenceNumber`.

### Step 3 — When the Customer Invoice Is Paid

The existing `CustomerPayment` workflow handles this. The shipping charge (`Sale.ShippingCost`) is part of the total invoice amount. When the customer pays:

- The entire receipt (product + shipping) hits **1010 Checking Account** (debit) and the appropriate revenue accounts (credit)
- `4300 Shipping Revenue` is credited for the `Sale.ShippingCost` portion

No special handling is needed beyond what `CustomerPaymentService.RecordPaymentAsync` already does — provided the journal entry generation includes a `4300` line when `Sale.ShippingCost > 0`.

### What Is Explicitly Excluded

| Item | Status |
|---|---|
| Accrual to `2200 Accrued Expenses` at shipment time | ❌ Not used |
| `ExpensePayment` with `Status = Pending` for carrier costs | ❌ Not used |
| Auto-created `Expense` or `ExpensePayment` at shipment time | ❌ Not used |
| Two-step accrue-then-pay workflow | ❌ Not used |

---

## 5. Unpaid Carrier Costs Queue

Replaces the need for an accrued liability account. A report query surfaces all shipments where the carrier has not yet been paid:

````````
This is the code block that represents the suggested code change:

````````markdown
| Item | Status |
|---|---|
| Accrual to `2200 Accrued Expenses` at shipment time | ❌ Not used |
| `ExpensePayment` with `Status = Pending` for carrier costs | ❌ Not used |
| Auto-created `Expense` or `ExpensePayment` at shipment time | ❌ Not used |
| Two-step accrue-then-pay workflow | ❌ Not used |

---

## 5. Unpaid Carrier Costs Queue

Replaces the need for an accrued liability account. A report query surfaces all shipments where the carrier has not yet been paid:

// SalesController.UnpaidCarrierCosts (GET)
var unpaid = await _context.Shipments
    .Include(s => s.Sale).ThenInclude(s => s.Customer)
    .Where(s => s.ShippingAccountType == ShippingAccountType.OurAccount && s.ActualCarrierCost > 0 && s.FreightOutExpensePaymentId == null)
    .OrderBy(s => s.ShipmentDate)
    .ToListAsync();

````````

This is the code block that represents the suggested code change:

````````markdown
This list is the user's payables queue for carrier invoices. Each row shows: Sale#, Customer, Courier, Tracking#, Ship Date, `ActualCarrierCost`, and a link to **Record Payment**.

---

## 6. Shipping P&L Summary

Computed per period from existing data — no new tables required:
Shipping Revenue (4300) = SUM(Sale.ShippingCost) WHERE Sale has a CustomerPayment in period AND Sale.ShippingCost > 0
Freight-Out (6500)      = SUM(ExpensePayment.Amount) WHERE Expense.Category = ShippingOut AND ExpensePayment.PaymentDate in period
Net Shipping P&L        = Shipping Revenue − Freight-Out

- **Positive** = shipping is a net revenue contributor (over-recovery / markup)
- **Negative** = shipping is a subsidized business cost (under-recovery / free shipping)
- **Zero** = exact cost recovery across the period

---

## 7. UI Behavior — Shipping Forms

### 7.1 Fields Added to Process Sale and Create Additional Shipment Forms

| Field | Control | Behavior |
|---|---|---|
| `ShippingAccountType` | Radio button group | Toggles `ActualCarrierCost` visibility |
| `ActualCarrierCost` | Currency input (`$`) | Hidden / disabled when `CustomerAccount` selected |
| Shipping guidance alert | Read-only, dynamic | Updates as user types (see §7.2) |

### 7.2 Inline Guidance Messages (JavaScript, client-side)

Displayed below the carrier cost field in real time. Uses `Sale.ShippingCost` (already on the page) vs the entered `ActualCarrierCost`:

| Condition | Alert Style | Message |
|---|---|---|
| `CustomerAccount` selected | `alert-info` (blue) | "Shipping billed directly to customer by carrier. No freight-out expense will be recorded." |
| `OurAccount`, `ActualCarrierCost = 0` | `alert-warning` (yellow) | "Enter the actual carrier cost so unpaid freight can be tracked." |
| `OurAccount`, `ShippingCost = 0`, `ActualCarrierCost > 0` | `alert-warning` (yellow) | "Free shipping to customer. Full carrier cost of {cost:C} will be recorded as Freight-Out when paid." |
| `ShippingCost < ActualCarrierCost` | `alert-warning` (yellow) | "Under-recovery: you absorb {delta:C}. Net freight-out cost: {delta:C}." |
| `ShippingCost = ActualCarrierCost` | `alert-success` (green) | "Exact cost recovery. No net freight-out expense." |
| `ShippingCost > ActualCarrierCost` | `alert-info` (blue) | "Over-recovery: shipping contributes {delta:C} net revenue." |

### 7.3 Packing Slip

`ActualCarrierCost` is **never** shown on the packing slip or any customer-facing document. Only `Sale.ShippingCost` (the invoiced amount) appears on packing slips and invoices.

---

## 8. GL Account Reference

| Account | Code | Type | Role |
|---|---|---|---|
| Checking Account | 1010 | Asset | Cash paid to carrier / received from customer |
| Accounts Receivable | 1100 | Asset | Customer owes for invoice (if not immediate payment) |
| Shipping Revenue | 4300 | Revenue | Shipping amount billed to and collected from customer |
| Freight-Out | 6500 | Operating Expense | Full amount paid to carrier when invoice is settled |

---

## 9. Decision Flowchart
flowchart TD A["Shipment Created"] --> B{"ShippingAccountType?"} B -- "CustomerAccount" --> C["ActualCarrierCost = 0\nNo GL entry — ever\nFreightOutExpensePaymentId = null"] B -- "OurAccount" --> D["Save ActualCarrierCost\non Shipment record\nNo GL entry yet"] D --> E["Shipment.FreightOutExpensePaymentId = null\n"Unpaid Carrier Costs" queue"] E --> F{"Carrier Invoice\nReceived?"} F -- "Yes — user pays via\nExpenses → Record Payment" --> G["GL: Debit 6500 Freight-Out\nCredit 1010 Checking\nSet FreightOutExpensePaymentId"] F -- "Not yet" --> E G --> H{"Sale.ShippingCost > 0?"} H -- "Yes" --> I["When customer pays invoice:\nGL includes 4300 Shipping Revenue credit\nfor ShippingCost portion"] H -- "No" --> J["Free shipping — no\n4300 entry needed"]


---

## 10. Implementation Checklist

### Phase 1 — Data Model
- [ ] Create `Models\Enums\ShippingAccountType.cs` with `OurAccount` / `CustomerAccount`
- [ ] Add `ActualCarrierCost`, `ShippingAccountType`, `FreightOutExpensePaymentId`, computed `HasUnpaidCarrierCost` and `ShippingPnL` to `Models\Shipment.cs`
- [ ] Add `4300 Shipping Revenue` account to `Models\Accounting\DefaultChartOfAccounts.cs`
- [ ] Run EF migration: `AddShipmentFreightOutTracking`

### Phase 2 — ViewModels
- [ ] Add `ShippingAccountType` and `ActualCarrierCost` to `ViewModels\ProcessSaleViewModel.cs`
- [ ] Add `ShippingAccountType` and `ActualCarrierCost` to `ViewModels\CreateAdditionalShipmentViewModel.cs`

### Phase 3 — Controller: Capture at Shipment Time
- [ ] In `SalesController.CreateShipmentRecordAsync`, set `shipment.ShippingAccountType` and `shipment.ActualCarrierCost` from the viewmodel (no GL entry)
- [ ] Apply same data capture in `SalesController.CreateAdditionalShipment` (POST)

### Phase 4 — Controller: Link at Payment Time
- [ ] In `ExpensesController.RecordPayments` (POST), after saving the `ExpensePayment`, match on `ReferenceNumber` = tracking number and set `Shipment.FreightOutExpensePaymentId`
- [ ] Ensure `IAccountingService.GenerateJournalEntriesForExpensePaymentAsync` posts to `6500 Freight-Out` for `ShippingOut` category payments

### Phase 5 — Controller: Shipping Revenue on Customer Payment
- [ ] In `CustomerPaymentService.RecordPaymentAsync` (or journal entry generation), when `Sale.ShippingCost > 0`, include a `4300 Shipping Revenue` credit line in the journal entry

### Phase 6 — UI: Forms
- [ ] Add `ShippingAccountType` radio buttons to `Views\Sales\ProcessSale.cshtml` (or the Details page ship modal)
- [ ] Add `ActualCarrierCost` currency input, toggled by `ShippingAccountType`
- [ ] Add JavaScript `updateShippingGuidance()` function for inline alerts (§7.2)
- [ ] Apply same UI changes to `Views\Sales\CreateAdditionalShipment.cshtml`
- [ ] Confirm `ActualCarrierCost` is absent from `Views\Sales\PackingSlip.cshtml` and all invoice views

### Phase 7 — Unpaid Carrier Costs Report
- [ ] Add `SalesController.UnpaidCarrierCosts` (GET) action
- [ ] Create `Views\Sales\UnpaidCarrierCosts.cshtml` listing shipments where `HasUnpaidCarrierCost = true`
- [ ] Add navigation link (e.g., under Shipments menu or Expenses menu)

### Phase 8 — Shipping P&L Report (optional, Phase 2)
- [ ] Add shipping P&L summary panel to Sale Details view
- [ ] Add period shipping P&L to Expense Reports or Income Statement