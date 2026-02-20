Final Consolidated Shortcomings List

Many customers purchase items via purchase orders since most business is B2B. When I talk about cash basis, I don't literally mean cash at time of sale, I mean from a tax perspective, money in during the financial year and money out during the financial year are all that matter.
This is the complete list across all three conversations, deduplicated, corrected for the cash-basis clarification, and ordered by priority. Each item states exactly where in the code the problem lives.


---
🔴 CRITICAL — Incorrect Financial Reporting (Fix Before Tax Filing)
---
C-1 · Revenue is posted at invoice date, not payment date
AccountingService.GenerateJournalEntriesForSaleAsync credits revenue account 4000 and debits AR 1100 the moment a sale is saved. GenerateJournalEntriesForCustomerPaymentAsync then posts Dr Cash / Cr AR at payment time. When a sale crosses a year boundary (invoiced December, paid January), revenue lands in the wrong tax year.
Fix: Remove the revenue credit and COGS entry from GenerateJournalEntriesForSaleAsync. Post only Dr AR / Cr [nothing] as a memo at invoice time. Move Cr Revenue and Dr COGS / Cr Inventory into GenerateJournalEntriesForCustomerPaymentAsync.
---
C-2 · Partial payments post zero COGS
GenerateJournalEntriesForCustomerPaymentAsync posts Dr Cash / Cr AR for the partial amount but posts no COGS because that was already posted at sale time (C-1 above). On cash basis, COGS must be proportional to cash received. A 50% payment in Year 1 should recognise 50% of COGS in Year 1.
Fix: Resolve after C-1. At payment time, calculate (paymentAmount / saleTotal) × totalCOGS and post that fraction.
---
C-3 · Expenses (consumables/R&D) hit the GL at receipt, not payment
PurchaseService.ReceivePurchaseAsync calls GenerateJournalEntriesForPurchaseAsync for all item types. Consumable (6700) and R&D material (5400) purchases are expensed immediately on receipt — but on cash basis they should be expensed when the vendor is paid via GenerateJournalEntriesForVendorPaymentAsync.
Inventory items are fine — they go to an asset account (1200) at receipt, which is correct regardless of accounting method. Only the direct-to-expense item types are affected.
---
C-4 · ReversePaymentAsync does not create a reversing GL entry
CustomerPaymentService.ReversePaymentAsync sets Status = Reversed and logs "Consider creating a reversing journal entry" but creates nothing. A bounced cheque leaves Dr Cash on the books permanently. The cash account is overstated until someone manually intervenes.
Fix: Call _accountingService.ReverseManualJournalEntryAsync(payment.JournalEntryNumber, reason) inside ReversePaymentAsync.
---
C-5 · C-5 · ExpensesController.IncomeStatement reads revenue from Sales.SaleDate, not payment date
var sales = await _context.Sales
    .Where(s => s.SaleDate >= defaultStartDate && s.SaleDate <= defaultEndDate)

This simple income statement is completely accrual-based. It ignores whether payment was ever received and uses invoice dates throughout. It will diverge from the GL-based income statement in AccountingService.GetIncomeStatementAsync.
Fix: Filter by CustomerPayments.PaymentDate for revenue, and ExpensePayments.PaymentDate for expenses — both of which are already on the correct cash-basis trigger.
---
🟠 SIGNIFICANT — Data Integrity & Correctness Issues
---
S-1 · Travel expense maps to two different GL accounts depending on code path
Expense.GetSuggestedAccountCodeForCategory(Travel) → "6500" (Freight-Out) DefaultChartOfAccounts.GetDefaultExpenseAccountCode(Travel) → "6040" (Travel & Transportation)
A travel expense created via the ExpensesController suggestion logic posts to the carrier freight account. This silently misclassifies expenses and corrupts both the income statement and the 1099 vendor summary.
Fix: Change GetSuggestedAccountCodeForCategory(Travel) to return "6040".
---
S-2 · IsJournalEntryGenerated flag permanently blocks re-posting with no void mechanism
Once IsJournalEntryGenerated = true is set on a Sale or Purchase, all re-entry attempts silently return true without creating any entries. There is no void, no reversal, no correction workflow. A sale created before C-1 is fixed will have wrong revenue entries that can never be corrected through the UI.
Fix: Add a VoidAndRegenerateJournalEntryAsync method, or at minimum an admin screen that clears the flag and calls the reversal API.
---
S-3 · Sale.IsJournalEntryGenerated is set even when the GL entry is wrong
Because GenerateJournalEntriesForSaleAsync posts the wrong (accrual) entries, and then sets the flag, the system believes the cash-basis entries have been made when they have not. After fixing C-1, all existing sales with IsJournalEntryGenerated = true will have orphaned accrual entries that need to be voided.
Fix: Address after C-1 is deployed. Run a one-time migration script to clear IsJournalEntryGenerated on all sales where the corresponding customer payment journal entry also exists, then let the system regenerate correctly.
---
S-4 · No reversing GL entry when a vendor payment is deleted
PurchaseService.DeletePurchaseAsync decrements inventory stock but does not reverse the GL entries created by GenerateJournalEntriesForPurchaseAsync. The inventory asset balance on the balance sheet will be wrong after any purchase deletion.
---
S-5 · AccountsPayable.BalanceRemaining is a computed [NotMapped] property used in LINQ queries in some places
Confirmed in AccountingService.GetTotalAccountsPayableAsync (already fixed with the workaround comment). However, GetUnpaidAccountsPayableAsync loads all records to memory to avoid the issue — this will become a performance problem as AP volume grows and is a sign the underlying pattern is fragile.
---
S-6 · FreightOutExpensePaymentId linking is tracking-number-only, case-insensitive string match
LinkFreightOutPaymentsToShipmentsAsync matches on payment.PaymentReference == shipment.TrackingNumber. If the user puts anything other than the bare tracking number in the reference field (e.g. "FedEx 1Z999" vs "1Z999"), the link silently fails and HasUnpaidCarrierCost remains true indefinitely. There is no manual override in the UI.
---
🟡 OPERATIONAL GAPS — Missing Functionality
---
O-1 · No Owner's Draw / Owner's Distribution account
The chart of accounts has 3000 Owner's Equity but no 3400 Owner's Draws contra-equity account. When the owner takes money out of the business, there is no correct account to debit. Most sole proprietors do this regularly.
Fix: Add account 3400 Owner's Draws to DefaultChartOfAccounts.GetDefaultAccounts().
---
O-2 · Sales Tax Payable (2300) has no clearing workflow
Every sale with TaxAmount > 0 credits 2300 Sales Tax Payable. There is no screen, workflow, or journal entry type to record the periodic remittance to the tax authority. The balance accumulates indefinitely and never resets to reflect actual liability.
Fix: Add an Expenses entry type or dedicated "Pay Sales Tax" action that debits 2300 and credits 1010.
---
O-3 · No bank reconciliation module
There is no way to verify that the 1000 Cash / 1010 Checking GL balances match the actual bank statement. For a cash-basis business this is the most important control — the GL balance is the income figure.
---
O-4 · No Petty Cash account
1000 Cash - Operating and 1010 Checking Account exist, but not 1005 Petty Cash. Small out-of-pocket cash purchases either go unrecorded or distort the main cash account.
Fix: Add 1005 Petty Cash to DefaultChartOfAccounts.GetDefaultAccounts().
---
O-5 · 1099 threshold detection exists but Vendor.TaxId field may be null with no enforcement
TaxReportsViewModel correctly flags vendors over $600 as RequiresForm1099 = true. However there is no validation that Vendor.TaxId is populated before a payment is saved. You can pay a contractor $5,000 and have no tax ID recorded — making the 1099 report useless.
Fix: Add a warning (not a hard block) on ExpensePayment save when the vendor's TaxId is null and the year-to-date paid total exceeds $500.
---
O-6 · CashFlowStatement is an empty stub
AccountingService.GetCashFlowStatementAsync returns NetIncome = 0 and empty collections. CalculateInvestingCashFlowForPeriod and CalculateFinancingCashFlowForPeriod both return 0. For a cash-basis business this is the most meaningful report — it is currently non-functional.
---
O-7 · Cash flow projections use a flat 12-month average with no seasonality
GetCashFlowProjectionsAsync applies historicalData.Average(h => h.OperatingCashFlow) uniformly across all future months. Any business with seasonal patterns (holiday sales, annual contract renewals, etc.) will see projections that are wrong in every single month and only accidentally correct on an annual average.
---
Summary Table
# Shortcomings Summary

## 🔴 Critical — Incorrect Financial Reporting

| ID  | Issue                                                        | File(s) Affected                                        |
|-----|--------------------------------------------------------------|---------------------------------------------------------|
| C-1 | Revenue posted at invoice date, not payment date            | `Services\AccountingService.cs`                         |
| C-2 | Partial payments post zero COGS                             | `Services\AccountingService.cs`                         |
| C-3 | Consumable/R&D expenses hit GL at receipt, not payment      | `Services\AccountingService.cs`, `Services\PurchaseService.cs` |
| C-4 | Payment reversal creates no reversing GL entry              | `Services\CustomerPaymentService.cs`                    |
| C-5 | Expenses income statement filters by invoice date, not payment date | `Controllers\ExpensesController.cs`             |

## 🟠 Significant — Data Integrity & Correctness

| ID  | Issue                                                        | File(s) Affected                                        |
|-----|--------------------------------------------------------------|---------------------------------------------------------|
| S-1 | Travel expense maps to GL 6500 (Freight-Out) instead of 6040 | `Models\Expense.cs`                                   |
| S-2 | No void/re-post mechanism — `IsJournalEntryGenerated` flag is permanent | `Services\AccountingService.cs`, `Models\Sale.cs`, `Models\Purchase.cs` |
| S-3 | Existing accrual GL entries become orphaned after C-1 is fixed | Migration script required                             |
| S-4 | Purchase deletion does not reverse GL entries               | `Services\PurchaseService.cs`                           |
| S-5 | `BalanceRemaining` computed property causes fragile LINQ patterns | `Services\AccountingService.cs`, `Models\Accounting\AccountsPayable.cs` |
| S-6 | Freight-Out shipment linking relies on fragile string match only | `Controllers\ExpensesController.cs`, `Models\Shipment.cs` |

## 🟡 Operational — Missing Functionality

| ID  | Issue                                                        | File(s) Affected                                        |
|-----|--------------------------------------------------------------|---------------------------------------------------------|
| O-1 | No Owner's Draw / Owner's Distribution account (3400)       | `Models\Accounting\DefaultChartOfAccounts.cs`           |
| O-2 | Sales Tax Payable (2300) has no remittance/clearing workflow | `Controllers\ExpensesController.cs` (new action needed) |
| O-3 | No bank reconciliation module                               | New feature required                                    |
| O-4 | No Petty Cash account (1005)                                | `Models\Accounting\DefaultChartOfAccounts.cs`           |
| O-5 | Vendor TaxId not validated before payment exceeds $600      | `Controllers\ExpensesController.cs`, `Models\Vendor.cs` |
| O-6 | Cash flow statement is an empty stub                        | `Services\AccountingService.cs`                         |
| O-7 | Cash flow projections use flat average — no seasonality     | `Services\AccountingService.cs`                         |