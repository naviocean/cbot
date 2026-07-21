# PmLh (PM-LH)

cBot cTrader: **POC Migration + LVN Highway** trên Volume Profile (XAUUSD primary). Bias theo **di chuyển rolling POC**; vào lệnh khi giá tương tác **LVN** như vùng chạy nhanh; **risk %** size; thoát **chỉ** SL / TP cố định theo R / trailing (BE optional).

## Quick start (sau khi có code)

- **Pair:** XAUUSD (research có thể thêm symbol sau)
- **TF:** theo param chart (M5 / M15 / … — **chưa khóa** cho đến backtest)
- **Build:** `Robots/PmLh/PmLh/` → Release → `PmLh.algo`
- **Attach** chart, bật session mong muốn, set Risk %, chạy visual/debug rejects trước live

## Core logic

1. Mỗi closed bar: cập nhật **rolling POC** window `N`, tính **migration score** `M`.  
2. Lấy **LVN** từ profile structure (composite và/hoặc rolling — theo param).  
3. Nếu `M` đủ mạnh theo hướng D, giá không lệch sai phía POC quá xa, **và** pierce/retest LVN theo mode → signal D.  
4. SL theo invalidation LVN (+ ATR floor); **TP = entry ± RR × |entry−SL|**.  
5. Optional BE + trail trên `OnTick`.

**Post-BT gates (v1.0.1):** `E_PRICE_POC` (stale migration), `E_LVN_SIDE`; streak default **off** (rolling POC plateau-friendly).

## Exit (v1 — locked)

| Event | Action |
| --- | --- |
| Hit SL | Close |
| Hit TP (RR×R) | Close |
| Trail stop | Close |
| — | **Không** POC reverse exit, time stop, structure TP, partial |

## Docs

| File | Mục đích |
| --- | --- |
| [PROJECT_ROOT.md](./PROJECT_ROOT.md) | Master index |
| [v1.0/1-prds/PRD-pmlh.md](./v1.0/1-prds/PRD-pmlh.md) | Rules + params + reject codes |
| [v1.0/2-architecture/ARCH-pm-lh.md](./v1.0/2-architecture/ARCH-pm-lh.md) | Layers + flow |
| [v1.0/3-plans/PLAN-implement-pm-lh.md](./v1.0/3-plans/PLAN-implement-pm-lh.md) | Phases implement |
| [v1.0/4-tasks/TASK-backlog.md](./v1.0/4-tasks/TASK-backlog.md) | Checklist |

## Relationship

Complementary to **VacuumHunter** (LVN vacuum) and **HvnMagnet** (HVN magnet). **Separate bot** — see ADR-001.

## Status

**v1.0.1 implemented** — Release build `PmLh.algo`. Continue multi-day research BT; live freeze later.

### Build

```bash
dotnet build -c Release Robots/PmLh/PmLh/PmLh.csproj
```

Output: `Robots/PmLh/PmLh.algo` (and `PmLh/bin/Release/net6.0/PmLh.algo`).
