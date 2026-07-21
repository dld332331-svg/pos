# Future Enhancements — Post-Release Backlog

**Project:** POS System  
**Status:** v6 release complete (801 tests, 80.62% coverage, 39/39 spec sections)  
**Date:** 2026-07-22

---

## P1 — HeldSale Unit of Measure Persistence

**Status:** ⚠️ Open — only remaining spec gap  
**Section:** §20 — Multi-Unit of Measure (8/9 sub-items complete)

### Problem

When a cashier adds a product with a non-default unit (e.g., 500g of rice where the default unit is kg), the display unit is lost when the sale is held and later retrieved.

### Root Cause

1. `SaleItem` entity has **no `UnitOfMeasureId`** or `DisplayQuantity` field
2. `SaleService.HoldSaleAsync()` serializes items via `JsonSerializer` but **omits unit info**
3. Unit is only preserved as a text hack in `SaleItem.Notes`: `$"وحدة: {request.Unit}"`
4. Stored `Quantity` is already **converted** to the product's default unit

### Impact

| Scenario | Before | After Fix |
|:---------|:-------|:----------|
| Add 500g rice (default = kg) | Converts to 0.5 kg ✅ | Same ✅ |
| Hold sale | Saves 0.5 kg, **loses "g" display** ❌ | Preserves "500 g" ✅ |
| Retrieve sale | Shows "0.5 kg" ❌ | Shows "500 g" ✅ |

### Implementation Plan (Approach B — Full FK)

| Step | Task | File(s) | Est. Time |
|:----:|:-----|:--------|:---------:|
| 1 | Add `UnitOfMeasureId` (nullable Guid FK) and `DisplayQuantity` (decimal) to `SaleItem` entity | `SaleItem.cs` | 5 min |
| 2 | Create migration `AddUnitOfMeasureIdToSaleItems` | Migration files | 10 min |
| 3 | Update `SaleItemDto` to include new fields | `SaleDtos.cs` | 5 min |
| 4 | Update `AddItemRequest` DTO to pass `UnitOfMeasureId` | `SaleDtos.cs` | 5 min |
| 5 | Populate new fields in `SaleService.AddItemAsync()` | `SaleService.cs` | 10 min |
| 6 | Add unit fields to `HoldSaleAsync()` serialization | `SaleService.cs` | 5 min |
| 7 | Restore unit fields in `RetrieveHeldSaleAsync()` deserialization | `SaleService.cs` | 5 min |
| 8 | Update `PosTerminalForm` to display unit in held items | `PosTerminalForm.cs` | 10 min |
| 9 | Unit tests for hold/retrieve with units | Test files | 30 min |
| | **Total** | | **~1.5 hours** |

### Migration Safety

- **Backwards compatible**: FK is nullable — existing held sales will show quantities in the product's default unit
- **No data loss**: Existing `Notes` field still contains the text hack as fallback
- **Rollback**: Simple `dotnet ef migrations remove` if issues arise

### Acceptance Criteria

- [ ] `SaleItem` has `UnitOfMeasureId` (nullable FK) and `DisplayQuantity` properties
- [ ] Migration creates FK column without data loss
- [ ] Adding an item with a non-default unit populates both properties
- [ ] Holding a sale serializes and preserves the display unit
- [ ] Retrieving a held sale restores the original display unit and quantity
- [ ] Held sales created before the migration still display correctly (default unit fallback)
- [ ] 5+ unit tests covering the full hold/retrieve cycle with unit conversion

---

## P2 — GitHub Repository Initialization

**Status:** ⚠️ Planned  
**Priority:** Medium

### Tasks
- [ ] Initialize Git repository in project root
- [ ] Create `.gitignore` for .NET projects (bin/, obj/, coverage*.json, .suo, .user)
- [ ] Create initial commit with all source files
- [ ] Push to GitHub (public or private repo)
- [ ] Add CI workflow (GitHub Actions): `dotnet build`, `dotnet test`, Coverlet
- [ ] Add release tag v6.0.0 pointing to current state

---

## P3 — HeldSale DTO Refactoring (Optional Enhancement)

**Status:** 💡 Idea  
**Priority:** Low

### Description
Currently, `HeldSale.SerializedData` stores an unstructured JSON blob. For maintainability, consider:
- Adding structured columns to `HeldSale` for key fields (TotalAmount, ItemCount, UserId)
- Using the JSON column only for item-level detail
- Adding an `IQueryable<HeldSale>` query on the structured fields to avoid deserializing every held sale

### Trade-off
- **Pro**: Faster querying, no JSON parsing for list views
- **Con**: Schema change, migration, dual storage (structured + JSON)

---

## Enhancement Summary

| ID | Title | Priority | Effort | Dependencies |
|:--:|:------|:--------:|:------:|:------------|
| P1 | HeldSale Unit Persistence | 🔴 High | ~1.5 hrs | None |
| P2 | GitHub Repository Setup | 🟡 Medium | ~1 hr | None |
| P3 | HeldSale DTO Refactoring | 🟢 Low | ~2 hrs | P1 recommended first |

---

*This document tracks post-release enhancements. Priority is subject to business requirements.*
