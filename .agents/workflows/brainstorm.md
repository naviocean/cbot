---
description: Brainstorm chiến lược trading (class strategy). Không code.
---

# /brainstorm — Strategy exploration

$ARGUMENTS

---

## Harness

- **class:** `strategy`  
- **mode:** `plan` (options, chưa implement)  
- **persona:** `algo-strategist`  
- Skill: `brainstorming`  

**Không** dùng workflow này cho `trivial` / `bugfix` / “đổi một dòng” — classify lại theo GEMINI.

---

## Behavior

1. **Gate strategy** — hỏi lần lượt (tối thiểu): điều kiện thị trường (trend/range/news)? prop hay personal? max DD chấp nhận? symbol/TF nếu chưa có.  
2. **Chưa** sinh options cho đến khi trả lời đủ blocking.  
3. **≥3 hướng** algorithm (vd breakout vs reversion), mỗi hướng pros/cons (DD vs PF), giới hạn MQL5/cTrader.  
4. **So sánh + recommend** có lý do.  
5. Nếu chốt hướng: đề xuất ghi `docs/{version}/1-prds/` hoặc chuyển `/plan` — **lưu dưới docs/**, không yêu cầu git commit.

---

## Output

```markdown
## 🧠 Strategy Brainstorm: [Topic]

🎛️ **Harness:** class=`strategy` · mode=`plan` · persona=`algo-strategist`

### Context
…

### Option A: …
✅ Pros / ❌ Cons / 📊 Effort: Low|Medium|High

### Option B: …
…

## 💡 Recommendation
…
```
