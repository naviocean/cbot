---
name: brainstorming
description: Socratic + giao tiếp cho class strategy/feature/orchestrate. Không bắt buộc với trivial/bugfix/analyze. Bao gồm progress và error handling.
allowed-tools: Read, Glob, Grep
---

# Brainstorming & Communication (Harness-aware)

> Dùng khi RequestClass ∈ `strategy` | `feature` (lớn) | `orchestrate`, hoặc yêu cầu mơ hồ về **logic giao dịch mới**.  
> **Không** kích hoạt full ritual cho `trivial` | `bugfix` | `analyze` | `meta` — xem `.agents/rules/GEMINI.md`.

---

## Khi nào bật Socratic đầy đủ

| Pattern | Action |
| ------- | ------ |
| EA/strategy mới thiếu symbol/TF/risk | 🛑 Hỏi blocking (lần lượt) |
| Feature multi-module / kiến trúc | 🛑 Làm rõ scope trước code |
| “Build cả hệ grid từ zero” | 🛑 Phân rã sub-project trước |
| Đổi 1 magic / typo / đọc log | ❌ **Không** dùng skill này — làm việc luôn theo class |

### Process (strategy / feature lớn)

1. **Decomposition** nếu quá lớn.  
2. **STOP** code production.  
3. **ASK** blocking — trading-first, không “Users = end users web”:
   - Mục tiêu / edge  
   - Symbol + timeframe  
   - Risk / DD / prop hay personal  
   - Scope must-have  
   > Hỏi lần lượt hoặc multiple-choice.  
4. **PROPOSE** 2–3 hướng + trade-off + recommend.  
5. **WAIT** user.

---

## Design / plan trước code (strategy & orchestrate)

**Không** implement EA/cBot cho đến khi có plan/PRD được user approve (class `strategy`|`orchestrate`).

1. Tóm tắt architecture / rules / trade-off.  
2. **Lưu** vào `docs/` theo `documentation-standards` (PRD/PLAN) — **không** bắt buộc `git commit`.  
3. Self-review: không TBD mơ hồ; một nghĩa cho requirement.  
4. Hỏi user review file path cụ thể.  
5. Sau **Y** tường minh → `/orchestrate` Phase 2 hoặc HANDOFF sang dev.

`bugfix` / `trivial`: **bỏ qua** mục này.

---

## Dynamic questions

**Không** template cứng. Nguyên tắc trong `dynamic-questioning.md` (nếu cần sâu).

| Principle | Meaning |
| --------- | ------- |
| Questions → consequences | Mỗi câu chặn một nhánh implement |
| Context first | greenfield / feature / refactor / debug |
| Minimum viable questions | Chỉ P0 blocking |
| Không đoán | Hỏi kèm default nếu bỏ qua |

Format gợi ý:

```markdown
### [P0] **[ĐIỂM QUYẾT ĐỊNH]**

**Câu hỏi:** …

**Vì sao quan trọng:** … (rủi ro / DD / broker)

**Options:** …
| Option | Pros | Cons |

**Nếu không nói:** [default]
```

---

## Visual companion

Với state machine / order flow phức tạp: **một lần** hỏi có muốn diagram (Mermaid) — message riêng, không gộp với câu hỏi gate.

---

## Progress

| Icon | Nghĩa |
| ---- | ----- |
| ✅ | Xong |
| 🔄 | Đang làm |
| ⏳ | Chờ user/dependency |
| ❌ | Lỗi |
| ⚠️ | Cảnh báo |

---

## Error handling (EA domain)

```
1. Nhận lỗi
2. Giải thích dễ hiểu
3. Hướng xử lý + trade-off
4. User chọn
```

| Category | Gợi ý |
| -------- | ----- |
| **Compile MQL5/C#** | Đưa dòng lỗi + hướng fix |
| **Runtime 10016/4756** | Stops/freeze/filling — skill debug |
| **Thiếu dependency / RWCommon** | Hỏi có lib không; rwcommon flexible |
| **Backtest ảo (overfit)** | Cảnh báo tester mindset |
| **Unclear** | Xin log/path report |

(Không dùng category lỗi web-stack làm default.)

---

## Completion

1. Xác nhận ngắn  
2. Tóm tắt concrete  
3. Cách verify (profile / path)  
4. Bước tiếp (plan / HANDOFF / implement)

---

## Anti-patterns

| Tránh | Vì sao |
| ----- | ------ |
| Code trước khi hiểu strategy mới | Sai edge |
| 3 câu strategy cho typo | Friction / model bỏ gate |
| Over-engineer v1 | Chậm value |
| “I think” thay vì hỏi P0 | Đoán mò |
