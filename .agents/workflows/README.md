# RedWave Labs EA — Workflows

Entry points cho harness. Rules luôn bật: `.agents/rules/GEMINI.md` (RequestClass → mode → persona → verify).

## Lệnh

| Command | Class | Mode | Persona | Ghi chú |
| ------- | ----- | ---- | ------- | ------- |
| **`/brainstorm`** | `strategy` | `plan` | `algo-strategist` | Options; không code |
| **`/plan`** | `strategy` / `feature` | `plan` | `algo-strategist` | `docs/…/3-plans/PLAN-*.md` |
| **`/orchestrate`** | `orchestrate` | plan→implement→review | multi + **HANDOFF** + **SESSION** | Cần user Y sau plan |
| **`/test`** | `analyze` | `review` | `ea-tester` | Report/log; fix qua HANDOFF |

Ví dụ: `/plan XAUUSD session breakout` · `/test report bt.html`

## Artifacts

| File | Khi |
| ---- | --- |
| `HANDOFF-*.md` | Đổi persona qua ranh giới |
| `SESSION.md` | Orchestrate / multi-day / multi-handoff |
| `VERIFY-PROFILES.md` | Iron Law theo platform |

Templates: `docs/v1.0/4-tasks/HANDOFF.template.md`, `SESSION.template.md`.

## Personas

| Agent | Dùng khi |
| ----- | -------- |
| `algo-strategist` | Rules, PRD, plan |
| `mql5-expert` | Code MT5 |
| `cbot-expert` | Code cBot |
| `ea-tester` | Backtest / journal |
| `documentation-writer` | User **xin** docs |

## Gate

Theo **class** (không always 3 câu). Chi tiết GEMINI.md.

## Thêm workflow

1. File markdown trong `.agents/workflows/`  
2. Frontmatter `description:`  
3. Ghi class / mode / persona / HANDOFF rules  
4. Chỉ reference agent/skill có thật  
