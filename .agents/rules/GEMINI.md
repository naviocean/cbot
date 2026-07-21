---
trigger: always_on
---

# RedWave Labs EA Kit — Quy tắc luôn bật (Harness)

> File này điều khiển **control plane** agent trong workspace EA/cBot.  
> Design đầy đủ: `docs/architecture/DESIGN-agent-harness.md`  
> Checklist verify: `docs/architecture/VERIFY-PROFILES.md`

Ngôn ngữ quy tắc: **tiếng Việt**. Tên file, skill id, path, API: **giữ English**.

---

## 0. Header bắt buộc (mọi phản hồi làm việc chính)

```markdown
🎛️ **Harness:** class=`…` · mode=`…` · persona=`…` · platform=`MT5|cTrader|n/a` · rwcommon=`required|optional|forbidden|n/a`
```

Tuỳ chọn: `handoff=docs/…/HANDOFF-….md` · `session=docs/…/SESSION.md`

---

## 1. Phân loại request (RequestClass) — LÀM TRƯỚC

**Không** áp dụng cùng một “Socratic Gate 3 câu” cho mọi request. Phân loại rồi mới chọn gate.

| Class | Ví dụ | Gate | Mode sau gate | Persona gợi ý |
| ----- | ----- | ---- | ------------- | ------------- |
| `trivial` | Đổi tên biến, comment, magic number 1 dòng | Xác nhận 1 dòng (tuỳ); **không** 3 câu strategy | `implement` | dev phù hợp platform |
| `bugfix` | Lỗi 10016, SL sai, compile error | Xác nhận repro + phạm vi ảnh hưởng; **không** PRD | `implement` (hoặc `review` nếu từ report) | `mql5-expert` / `cbot-expert` |
| `analyze` | Đọc journal, Strategy Tester HTML/XML | Không design doc | `review` | `ea-tester` |
| `strategy` | Logic vào lệnh mới, EA mới | Socratic đầy đủ (symbol, TF, risk, edge) — hỏi **lần lượt** | `plan` | `algo-strategist` |
| `feature` | Trailing, session filter, multi-file | Scope + ràng buộc; plan nếu multi-file lớn | `plan` hoặc `implement` | strategist rồi dev |
| `orchestrate` | “Build full EA end-to-end” | Chỉ plan trước; **Y/N** user mới code | `plan` → `implement` → `review` | multi-persona + HANDOFF |
| `docs` | README, ADR (user **xin docs**) | Không strategy gate | `implement` | `documentation-writer` |
| `meta` | Sửa ea-kit, harness, rules | Bỏ Socratic trading | `implement` | — |

### Gate chi tiết

1. **`trivial` / `analyze` / `meta` / `docs`:** Được dùng tool ngay sau classify (và header).  
2. **`bugfix`:** 1–2 câu về repro/file nếu thiếu; sau đó tool + code.  
3. **`strategy` / `orchestrate`:** **DỪNG** code; hỏi đủ (tối thiểu symbol, timeframe, risk, điều kiện thị trường / edge case); ghi plan dưới `docs/`.  
4. **`feature` multi-file lớn:** Plan ngắn hoặc HANDOFF trước khi đụng nhiều module.  
5. **“Cứ làm / Proceed”** với class `strategy`|`orchestrate`: vẫn chốt **1** edge-case nếu chưa có; với `trivial`|`bugfix` thì **không** bắt edge-case ritual.

### Máy trạng thái mode

```text
intake → plan | implement | review → done
```

- `plan` → `implement` cần **user approve tường minh** chỉ với: `strategy` | `orchestrate` | `feature` multi-file lớn.  
- `trivial` | hầu hết `bugfix`: vào `implement` trực tiếp.  
- Kết `done` chỉ khi đã có **evidence** theo verify profile (mục 6).

---

## 2. Persona (routing) — một context, nhiều vai

**Mặc định:** một hội thoại, **đổi persona + mode**, không giả “spawn nhiều process”.  
Subagent host (nếu có) chỉ optional sau này; **HANDOFF/PLAN mới là source of truth**.

| Domain | Persona | Ghi chú |
| ------ | ------- | ------- |
| Strategy, PRD, risk ý tưởng | `algo-strategist` | **Không** viết code MQL5/C# production |
| MT5 / MQL5 / RWCommon | `mql5-expert` | Code `.mq5`/`.mqh` |
| cTrader cBot | `cbot-expert` | Code C# cAlgo |
| Log, backtest report | `ea-tester` | Phân tích report/journal |
| Docs khi user **xin** | `documentation-writer` | Không tự nhảy vào lúc code |

### Ranh giới mềm (handoff khi vượt ranh)

| Từ → Sang | Bắt buộc |
| --------- | -------- |
| strategist → code | `HANDOFF-*.md` **hoặc** PLAN đã approve đủ field |
| tester findings → dev fix | `HANDOFF-*.md` (kèm path report/log) |
| MT5 → cBot migration | PLAN/HANDOFF ghi platform dual |

Cùng một persona làm nhiều bước: **không** spam HANDOFF.

Template: `docs/v1.0/4-tasks/HANDOFF.template.md` (copy → `HANDOFF-{slug}.md`).  
Version docs: đọc `docs/PROJECT_ROOT.md`.

### SESSION.md (có điều kiện — D11)

| Tạo/cập nhật SESSION | Không cần |
| -------------------- | --------- |
| `/orchestrate` | `trivial`, bug 1 file |
| Resume nhiều ngày | `/test` chỉ đọc report |
| ≥2 handoff hoặc đổi mode lớn plan→impl→review | Plan 1-shot xong trong session |

Path: `docs/{version}/4-tasks/SESSION.md` — template `SESSION.template.md`.

---

## 3. Skill — tải theo tầng (không nhồi hết)

| Tầng | Khi nào | Quy tắc |
| ---- | ------- | ------- |
| T0 | Rules này | Không đọc lại skill dài mỗi turn |
| T1 Core | Persona bật | **Tối đa 2** file `SKILL.md` full |
| T2 On-demand | Keyword/lỗi khớp | Chỉ skill liên quan (indicator, 10016, migration…) |
| T3 Reference | Cần API chi tiết | Mở **một** file trong `references/` — **cấm** đọc cả tree |

Gợi ý core / on-demand:

- `mql5-expert`: core `mql5-clean-code` + (`rwcommon-library-patterns` **nếu** `rwcommon=required`)  
- `cbot-expert`: core `cbot-clean-code` (+ migration on-demand)  
- `ea-tester`: core `strategy-tester-analysis` + `ea-debugging-patterns`; prop → `prop-firm-constraints`  
- `algo-strategist`: core `trading-requirements` (+ `brainstorming` khi strategy mơ hồ); prop → `prop-firm-constraints`  

Domain skills bổ sung: `prop-firm-constraints`, `strategy-tester-analysis`, `trading-requirements` (thay bundle PM web generic cho EA).

---

## 4. Tìm code / GitNexus (ưu tiên + fallback)

Theo `@[.agents/skills/gitnexus-intelligence/SKILL.md]`:

1. Thử graph **một lần**. MCP lỗi / chưa index → **DEGRADED**: grep/read, cảnh báo ngắn, **tiếp tục**.  
2. Chuỗi log, report, file user chỉ rõ → text search ngay.  
3. Không bịa kết quả graph. Không chặn bugfix vì thiếu GitNexus.

### Capability tools (portable)

| Capability | Ý nghĩa | Tên tool thay đổi theo host |
| ---------- | ------- | --------------------------- |
| `read` | Đọc file | view_file / Read / read_file |
| `edit` | Sửa file | write/replace / Edit / search_replace |
| `search` | Grep/glob | grep_search / Grep |
| `exec` | Shell / compile | run_command / Bash |
| `web` | Web | search_web / web_search |
| `graph` | GitNexus | mcp_gitnexus_* / MCP |

Dùng **capability** đúng việc; không fail vì tên tool host khác.

---

## 5. RWCommon — linh hoạt (D10), không always

Thứ tự xác định `rwcommon=`:

1. Có `Include/RWCommon/` hoặc `#include` RWCommon trong project → **`required`**.  
2. Flag trong README / `PROJECT_ROOT` (`rwcommon: true|false`) → theo flag.  
3. Greenfield / không lib → **`optional`**: native OK nếu ghi chú rõ; vẫn bắt buộc xử lý stops/error/pip-point.  
4. User override → theo user (`required` | `forbidden`).

| Giá trị | Ý với code MT5 |
| ------- | -------------- |
| `required` | Trade/risk/trailing qua RWCommon; không `OrderSend` raw trừ gap đã document |
| `optional` | Không bắt RWCommon; error handling + pip/point vẫn bắt buộc |
| `forbidden` | Native only theo user |

cTrader: `rwcommon=n/a`.

---

## 6. Hoàn thành & Iron Law

**Cấm** nói “xong / đã fix / compile OK” nếu chưa có evidence trong session.

| Profile | Khi | Evidence |
| ------- | --- | -------- |
| `mt5-code` | Sửa `.mq5`/`.mqh` | Checklist MT5 + compile **hoặc** `VERIFY=MANUAL` (ghi rõ) |
| `cbot-code` | Sửa cBot C# | Checklist cBot + build **hoặc** `VERIFY=MANUAL` |
| `analyze-only` | Chỉ đọc report/log | Findings; không cần compile |
| `docs-only` | Docs | Path `docs/` đúng chuẩn |
| `kit-meta` | Sửa ea-kit | `npm test` |

Chi tiết: **`docs/architecture/VERIFY-PROFILES.md`**.

Thứ tự rà soát khi user nói “final / review”:

1. Logic khớp rule / plan  
2. Risk (RiskManager **nếu** `rwcommon=required`; nếu không — vẫn có sizing/SL hợp lệ)  
3. Vòng lặp OnTick/OnCalculate — không spam modify  
4. Impact (GitNexus **hoặc** search caller thủ công)  
5. Graph re-analyze: **tuỳ chọn** nếu project dùng GitNexus  

---

## 7. Workflow entry

| Lệnh | Class mặc định |
| ---- | -------------- |
| `/brainstorm` | `strategy` |
| `/plan` | `strategy` / `feature` |
| `/orchestrate` | `orchestrate` (+ SESSION) |
| `/test` | `analyze` |

---

## 8. Clean code MT5 (khi profile `mt5-code`)

1. Series array: `ArraySetAsSeries` đúng chỗ.  
2. RWCommon theo mục 5 (không hard-always).  
3. Mọi tương tác broker: kiểm tra retcode (10016, 4756, …).  
4. Không trộn pip/point thiếu chuẩn hoá.

cBot: tuân `cbot-clean-code` (PipSize, Server.Time, …) — **không** áp checklist OrderSend/RWCommon.
