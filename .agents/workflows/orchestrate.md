---
description: Điều phối multi-persona (modes + HANDOFF) cho vòng đời EA/cBot đầy đủ.
---

# /orchestrate — Orchestration EA / cBot

**Harness:** class=`orchestrate` · mode bắt đầu=`plan` · cần **SESSION.md**

Nhiệm vụ:

$ARGUMENTS

---

## Nguyên tắc harness (bắt buộc)

1. **Không** giả spawn nhiều process. Dùng **đổi persona + mode** + file **HANDOFF**.  
2. **Tối thiểu 2 persona khác nhau** trên suốt lifecycle (ví dụ strategist + expert), thể hiện bằng HANDOFF/PLAN — không đếm “agent_count” ảo.  
3. `documentation-writer` chỉ khi user **xin docs**.  
4. Tạo/cập nhật `docs/{version}/4-tasks/SESSION.md` (template `SESSION.template.md`). Đọc version từ `docs/PROJECT_ROOT.md`.

### Platform

| Tín hiệu | Platform | Persona dev |
| -------- | -------- | ----------- |
| `.mq5`, MT5, RWCommon, Strategy Tester | MT5 | `mql5-expert` |
| cBot, cAlgo, cTrader | cTrader | `cbot-expert` |
| Migrate MT5→cBot | dual | migration qua `cbot-expert` (+ làm rõ source nếu cần) |
| Chưa rõ | **HỎI** trước Phase 2 | — |

Xác định `rwcommon=` theo GEMINI D10 (flexible).

### Ma trận persona

| Task | Persona |
| ---- | ------- |
| New EA MT5 | algo-strategist → mql5-expert → ea-tester |
| New cBot | algo-strategist → cbot-expert (+ ea-tester nếu có log/report) |
| Indicator MT5 | algo-strategist → mql5-expert |
| Bug MT5 | ea-tester → mql5-expert (HANDOFF) |
| Bug cBot | cbot-expert (+ ea-tester optional) |

---

## PHASE 1 — PLAN (mode=`plan`)

| Bước | Persona | Việc |
| ---- | ------- | ---- |
| 1 | `algo-strategist` | Gate strategy; tạo `docs/{version}/3-plans/PLAN-*.md` |
| 2 | — | Cập nhật SESSION (mode=plan, plan path, rwcommon) |

> Chỉ persona planning trong phase này. **Không code.**

### Checkpoint

```
✅ Plan đã lưu. Bạn approve để sang implement? (Y/N)
```

**Không** vào Phase 2 nếu chưa Y tường minh.

---

## PHASE 2 — IMPLEMENT (mode=`implement`)

1. Viết **HANDOFF** strategist → dev (template `HANDOFF.template.md`) nếu PLAN chưa đủ field handoff.  
2. Persona dev (`mql5-expert` / `cbot-expert`) implement theo PLAN/HANDOFF.  
3. Cập nhật SESSION (`mode=implement`, `last_handoff`).  
4. Skill: tối đa 2 core + on-demand; không bulk-read references.

---

## PHASE 3 — REVIEW (mode=`review`)

1. HANDOFF dev → `ea-tester` (hoặc tester đọc trực tiếp nếu cùng session đã có HANDOFF).  
2. Verify profile: `mt5-code` / `cbot-code` — xem `docs/architecture/VERIFY-PROFILES.md`.  
3. Evidence: compile/build **hoặc** `VERIFY=MANUAL`.  
4. SESSION → `mode=done` khi DoD đạt.

---

## Context khi đổi persona

Luôn mang theo:

1. User request gốc  
2. Path PLAN + HANDOFF  
3. Platform + `rwcommon`  
4. Constraints risk/symbol/TF  

---

## Output

```markdown
## 🎼 Orchestration Report

🎛️ **Harness:** class=`orchestrate` · mode=`…` · …

### Overview
…

### Persona / Handoff
| # | From → To | Handoff path | Status |
|---|-----------|--------------|--------|

### Evidence
- verify_profile=…
- …

### Next
…
```
