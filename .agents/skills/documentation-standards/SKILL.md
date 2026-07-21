---
name: documentation-standards
description: Documentation standards for RedWave Labs EA Team. Defines the EA docs/ directory structure, Strategy Tester reports, PRDs, and ADRs.
category: skill
---

# RedWave Labs EA Documentation Standards

## 1. Documentation Architecture (The `docs/` Folder)

To keep EA development organized, we strictly follow this structure:

```text
docs/
├── PROJECT_ROOT.md       <-- (Master File: Overall bot logic and active version)
├── v1.0/                 
│   ├── 1-prds/           <-- (algo-strategist) Strategy Rules, Risk Parameters
│   │   └── PRD-scalper-bot.md
│   ├── 2-architecture/   <-- (mql5-expert) UML, state machines, OCO relations
│   │   └── ARCH-grid-logic.md
│   ├── 3-plans/          <-- (algo-strategist) High-level implementation steps
│   │   └── PLAN-trailing-stop.md
│   ├── 4-tasks/          <-- Dev tracking files
│   │   └── TASK-implement-trailing.md
│   └── 5-reports/        <-- (ea-tester) MT5 Strategy Tester XML/HTML outputs
│       └── REPORT-bt-2024-XAUUSD.md
└── architecture/         <-- Global technical decisions
    ├── ADR-001-broker-selection.md
    └── ADR-002-tick-vs-bar-execution.md
```

**🔴 Rules for Agents:**
- **`algo-strategist`**: Save requirements in `1-prds/`, plans in `3-plans/`.
- **`mql5-expert` / `cbot-expert`**: Save structural flows in `2-architecture/` and track tasks in `4-tasks/`.
- **`ea-tester`**: Save and analyze backtest results in `5-reports/`.
- **`documentation-writer`**: Polish README/ADR/report write-ups only when the user explicitly requests docs.

## 2. README Structure for EA

Every EA must have a standard README:
```markdown
# [EA Name]

Brief strategy description (e.g., Mean Reversion on Asian Session).

## Quick Start
- Supported Pairs: XAUUSD, EURUSD
- Timeframe: M15
- Minimum Balance: $500

## Core Logic
[Explain entry and exit logic briefly]

## Input Parameters
| Variable | Description | Default |
| -------- | ----------- | ------- |
| InpMagic | Magic Number | 10001  |
```

## 3. Code Comment Guidelines (Doxygen/MQL5)
Instead of commenting obvious things, document why a specific MT5 limitation is bypassed:

| ✅ Comment           | ❌ Don't Comment         |
| -------------------- | ------------------------ |
| Why (Workaround for MT5 bug) | What (obvious from code) |
| Complex math for indicators | Every variable assignment |

```cpp
/**
 * Calculates the dynamic lot size based on RiskManager settings.
 * Note: Returned volume is strictly normalized by SYMBOL_VOLUME_STEP.
 * 
 * @param sl_distance Distance to StopLoss in points
 * @returns Normalized lot size double
 */
```

## 4. Architecture Decision Record (ADR)
When making core EA decisions (e.g., Virtual Pending Orders vs Hard Pending, Hedging vs Netting):

```markdown
# ADR-00X: Virtual Pending Orders

## Context
Broker limits the amount of pending orders to 100, our grid needs 200.

## Decision
We will store pending orders natively in arrays and execute Market Orders when price is hit.

## Consequences
- ✅ Bypasses broker limits
- ❌ Higher latency due to slippage during market execution
```
