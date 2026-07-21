# PLAN — Implement SVBS-X v1.0

> **Historical plan.** Implementation complete; **code of record = v1.1**.  
> See [PRD](../1-prds/PRD-svbs-x.md) and [README](../../README.md) for current rules (not all rows below match defaults anymore).

**Goal:** Ship a cTrader cBot for XAUUSD M5 implementing [PRD-svbs-x.md](../1-prds/PRD-svbs-x.md) with architecture [ARCH-svbs-x.md](../2-architecture/ARCH-svbs-x.md).

**Constraint:** No partial close. BE + trailing full size only.

---

## Phase 0 — Scaffold (0.5d)

| # | Task | Done when |
| --- | --- | --- |
| 0.1 | Create `Robots/SvbsX/SvbsX/` csproj + sln mirroring VacuumHunter | Builds empty Robot |
| 0.2 | Link required `Common/*.cs` | Compile OK |
| 0.3 | Parameters skeleton (groups: General, Risk, Session, Signal, Exit, Filters) | Visible in cTrader |

**Verify:** `dotnet build -c Release` succeeds.

---

## Phase 1 — Session clock + profiles (1d)

| # | Task | Done when |
| --- | --- | --- |
| 1.1 | `SessionClock`: UTC windows from params | Unit-testable IsInWindow |
| 1.2 | Freeze Asia profile at end; store VAH/VAL/POC/width | Logged PROFILE Asia |
| 1.3 | Freeze London profile | Logged PROFILE London |
| 1.4 | Developing POC for active window | Updates each closed bar |
| 1.5 | Extend `CVolumeProfile` **only if** session-range build missing | No look-ahead |

**Verify:** Chart audit one day — VA lines match visual session range; times align server UTC.

---

## Phase 2 — SignalEngine pure logic (1–1.5d)

| # | Task | Done when |
| --- | --- | --- |
| 2.1 | Filters F1–F10 with reject codes | String codes stable |
| 2.2 | Break arm E_BREAK_L/S | State BreakArmed |
| 2.3 | RetestHold acceptance default | Enter signal |
| 2.4 | Continuation mode flag | A/B switch |
| 2.5 | Volume V1–V3 + POC rules | Reject V_*/POC_* |
| 2.6 | Cancel C1–C4 | Back to Idle |

**Verify:** Synthetic bar fixtures or debug log walkthrough on historical day with known break.

---

## Phase 3 — Execution + risk (0.5–1d)

| # | Task | Done when |
| --- | --- | --- |
| 3.1 | SL geometry X1 + min/max $ | Skip if &gt; Max SL |
| 3.2 | RiskManager volume | Risk$ ≈ Risk% × equity |
| 3.3 | Market order + label | One position |
| 3.4 | Day counters trades / PnL | F6/F7 work |
| 3.5 | Symbol guard XAU | F1 |

**Verify:** Paper open log shows vol, SL$, risk$.

---

## Phase 4 — Exit management (0.5–1d)

| # | Task | Done when |
| --- | --- | --- |
| 4.1 | Wire TrailingManager BE @ 1R | SL moves once |
| 4.2 | Trail @ 1.5R ATR step | Full size only |
| 4.3 | **Assert no partial API usage** | Code review gate |
| 4.4 | Session hard flat X4a | Closes at window end |
| 4.5 | Time stop X4b | 90m rule |
| 4.6 | ExitMode Trail vs FixedRR | Research toggle |

**Verify:** Single position volume constant from open to close; BE/TRAIL logs.

---

## Phase 5 — Filters polish (0.5d)

| # | Task | Done when |
| --- | --- | --- |
| 5.1 | Spread guard | F5 |
| 5.2 | News filter optional | F9 |
| 5.3 | Optional ATR band F10 | Param off by default OK |
| 5.4 | Equity DD soft-stop | Blocks entries |

---

## Phase 6 — Observability + docs sync (0.5d)

| # | Task | Done when |
| --- | --- | --- |
| 6.1 | Journal events PROFILE/BREAK/ACCEPT/OPEN/BE/TRAIL/CLOSE/DAY | Readable |
| 6.2 | Optional chart VAH/VAL/POC | Visualize param |
| 6.3 | Update README build path status | Code exists |
| 6.4 | TASK-backlog checkboxes | Updated |

---

## Phase 7 — Research backtest (ongoing)

| # | Task | Done when |
| --- | --- | --- |
| 7.1 | Baseline E-Fixed 2R, RetestHold | Report in `5-reports/` |
| 7.2 | E-Trail production settings | Report |
| 7.3 | Ablation: −POC, −Volume, Continuation | Table compare |
| 7.4 | Walk-forward folds | OOS PF ≥ 1.2 gate |

---

## Dependency order

```text
0 Scaffold
  → 1 Session+Profile
    → 2 SignalEngine
      → 3 Execute+Risk
        → 4 Exit
          → 5 Filters
            → 6 Logs
              → 7 Backtest
```

Phases 5 can overlap late 4.

---

## Out of scope this plan

- HVN targets  
- Partial close  
- Multi-symbol  
- Prop daily hard limits  
- M15 primary (M5 only v1)  

---

## Implementation notes for cBot-expert

1. Copy project structure from `VacuumHunter` (csproj Common includes).
2. Do **not** copy VacuumHunter partial/TP1 logic (v1.0 legacy); follow VH v1.1 single-exit spirit + this PRD.
3. Prefer `SignalEngine` as pure static/instance with no `Robot` dependency for testability.
4. XAU volume sizing: follow existing FixedRisk patterns in Common (see VacuumHunter ADR-001 if still relevant).
5. Convert R to trail manager units once at entry; document formula in code comment **why**.

---

## Success criteria (ship v1.0 code)

- [ ] Builds Release `.algo`
- [ ] Paper: one full accept → open → BE → trail or flat without partial
- [ ] All reject codes from PRD appear in debug log when forced
- [ ] Session flat closes full position
- [ ] Docs paths match repo
