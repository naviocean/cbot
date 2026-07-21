---
description: Lập plan giao dịch (algo-strategist). Không viết code implementation.
---

# /plan — Planning mode

$ARGUMENTS

---

## Harness

- **class:** `strategy` hoặc `feature` (chọn theo scope)  
- **mode:** `plan`  
- **persona:** `algo-strategist`  
- **SESSION:** không bắt buộc nếu one-shot; có nếu multi-day  

---

## CRITICAL

1. **KHÔNG viết code** EA/cBot — chỉ plan/PRD.  
2. Gate theo class (strategy = Socratic symbol/TF/risk/edge).  
3. Skills: `trading-requirements`; `brainstorming` (nếu mơ hồ); `prop-firm-constraints` (nếu prop); `plan-writing`; `documentation-standards`.  
4. RWCommon: ghi rõ dự kiến `required|optional` trong plan (detect project nếu đã có code).

---

## Các bước

### 1. Clarification

- Hỏi đủ thông tin blocking (lần lượt nếu strategy).  
- Chờ user trước khi ghi plan.

### 2. Version docs

- Đọc `docs/PROJECT_ROOT.md` → `{version}` (mặc định `v1.0`).  
- Đảm bảo `docs/{version}/3-plans/` tồn tại.

### 3. Ghi plan

- Path: `docs/{version}/3-plans/PLAN-{slug}.md`  
- Nội dung tối thiểu:
  1. Mục tiêu (EA / Indicator / cBot)
  2. Rules entry/exit/filter
  3. Platform + module (RWCommon flexible / cAlgo)
  4. Task breakdown
  5. Persona assignments + **cần HANDOFF ở đâu**
  6. Verify profile dự kiến (`mt5-code` / `cbot-code`)
  7. Definition of done

### 4. Kết

```
[OK] Plan: docs/{version}/3-plans/PLAN-{slug}.md

Bước tiếp:
- Review plan.
- Approve xong có thể /orchestrate hoặc implement theo HANDOFF.
```
