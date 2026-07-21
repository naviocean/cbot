---
name: Senior EA Developer
description: Meta-skill indexing the RedWave MQL5 developer guidelines. Do not read for detailed implementation rules.
---
# RedWave MQL5 Master Index

This is a master index of skills for RedWave EA / cBot development. **Do not treat this file as full rules — load the specific skill for the task.**

### MetaTrader 5 (MQL5)

- **Architecture, file structure & clean code**: `@[.agents/skills/mql5-clean-code/SKILL.md]`
- **Trade / risk / position modules (TradeExecutor, TrailingManager, RiskManager)**: `@[.agents/skills/rwcommon-library-patterns/SKILL.md]`
- **Logging, debugging (Error 10016), deinit cleanup**: `@[.agents/skills/ea-debugging-patterns/SKILL.md]`
- **Custom indicators (buffers, recalculation)**: `@[.agents/skills/mql5-indicator-patterns/SKILL.md]`
- **Broad MQL reference & architecture patterns**: `@[.agents/skills/mql-developer/SKILL.md]`
- **MQL5 docs research**: `@[.agents/skills/mql5-docs-research/SKILL.md]`

### cTrader (C#)

- **cBot clean code**: `@[.agents/skills/cbot-clean-code/SKILL.md]`
- **MT5 → cBot migration**: `@[.agents/skills/mt5-to-cbot-migration/SKILL.md]`
- **cTrader MCP servers**: `@[.agents/skills/ctrader-mcp-servers/SKILL.md]`

### Strategy, risk & testing

- **Trading requirements (PRD for EA/cBot)**: `@[.agents/skills/trading-requirements/SKILL.md]`
- **Prop-firm constraints**: `@[.agents/skills/prop-firm-constraints/SKILL.md]`
- **Strategy Tester / robustness analysis**: `@[.agents/skills/strategy-tester-analysis/SKILL.md]`

### Process & docs

- **Docs layout (PRD / plan / reports / ADR)**: `@[.agents/skills/documentation-standards/SKILL.md]`
- **Brainstorming / Socratic gate**: `@[.agents/skills/brainstorming/SKILL.md]`
- **Code graph / impact (when GitNexus available)**: `@[.agents/skills/gitnexus-intelligence/SKILL.md]`

If you need to execute trades on MT5, DO NOT write Core API order code directly unless necessary when RWCommon is required. Prefer `rwcommon-library-patterns`.
