# ADR-003: Structure LVN source vs rolling POC (research-open)

## Context

Highway logic needs LVN geometry; migration needs a time series of POC. Using one rolling window for both is simple but LVNs may flicker. Using multi-day composite for LVN (VH-style) is more stable but dual builds cost more.

No backtest yet → **must not freeze** a single approach.

## Decision

Implement **`LvnSource` parameter**:

| Value | Behavior |
| --- | --- |
| `Composite` | LVN/shape from adaptive composite; POC migration from rolling N |
| `Rolling` | LVN + POC from same rolling window |
| `DualPreferComposite` | Prefer composite LVN; fallback rolling |

Default starter: **`Composite`** (stable nodes + dynamic POC). All values remain first-class for ablation.

## Consequences

- ✅ Data can pick winner  
- ✅ Aligns with “do not narrow scope pre-BT”  
- ❌ Slightly more orchestrator branches  
- ❌ Dual builds on every bar when composite + rolling both needed (always for Composite default)

## Status

Accepted (v1.0-spec).
