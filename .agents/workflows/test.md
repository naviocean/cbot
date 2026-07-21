---
description: Phân tích backtest/log (ea-tester). Class analyze — không phải web unit test.
---

# /test — Backtest & validation

$ARGUMENTS

---

## Harness

- **class:** `analyze`  
- **mode:** `review`  
- **persona:** `ea-tester`  
- **verify_profile:** `analyze-only`  
- **SESSION:** không bắt buộc  

Được **dùng tool ngay** (đọc report/log) — không Socratic strategy, không design doc.

---

## CRITICAL

1. Luôn persona `ea-tester`.  
2. Skills: `strategy-tester-analysis` (report/metrics); `ea-debugging-patterns` (journal/retcode); `prop-firm-constraints` nếu prop.  
3. Tập trung platform trading: MT5 codes (10016, 4756, 10013), DD, PF, modify spam; cBot theo log user cung cấp.  
4. **Không** tự nhảy sang sửa code im lặng. Cần fix → viết **HANDOFF** sang `mql5-expert` / `cbot-expert`.

## Sub-commands

```
/test report [file]  — Phân tích Strategy Tester / report
/test logs [file]    — Lỗi trong journal
/test logic          — Gợi ý edge-case (slippage, gap, spread)
```

## Behavior

1. Đọc data (profit, DD, tần suất lệnh, latency/modify).  
2. Findings cụ thể + path file.  
3. Đề xuất input/TF/symbol **hoặc** HANDOFF fix.  
4. Báo cáo bền vững (nếu user muốn): `docs/{version}/5-reports/REPORT-*.md`.
