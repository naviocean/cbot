# ARCH — SVBS-X v1.1

**Status:** Code of record  
**Depends on:** [PRD-svbs-x.md](../1-prds/PRD-svbs-x.md)

---

## 1. Components

```text
SvbsX (Robot)
  ├── SessionClock          fixed UTC + ResolveWindow + ShouldFlat(entry day)
  ├── CSessionFilter        Trade Asia/London/NY/Overlap
  ├── SignalEngine          Idle → AcceptWait → PASS / reject codes
  ├── CVolumeProfile        BuildRange (Asia/London freeze, developing POC)
  ├── CRiskManager          Equity DD + Max Daily Loss/Profit $
  ├── CTrailingManager      BE + trail (full size)
  ├── CMarketCondition      Spread
  └── CNewsFilter           Optional schedule
```

| Module | Must not |
| --- | --- |
| SignalEngine | Call trade API |
| SessionClock | Know risk / sizing |
| TrailingManager | Partial close |

---

## 2. State machine

```text
Idle → AcceptWait (E_BREAK) → PASS → Execute
                 ↘ C_TIMEOUT / C_REACCEPT / C_WINDOW / C_FLIP
Entered → full close (SL / TP / BE / trail / X4_SESSION_FLAT)
```

---

## 3. Bar / tick flow

**OnBar:** freeze profiles → resolve window → SignalEngine → ExecuteEntry (size + SL + optional TP).  
**OnTick:** RiskManager gates/flatten → TrailingManager BE/trail → session flat check.

**Session flat:** `utc >= flat clock on entry day` **OR** `utc.Date > entryUtc.Date` (weekend/gap).

---

## 4. Sizing (XAU)

See [ADR-004](./ADR-004-xau-volume-sizing.md). Implemented in `SvbsX.TrySizeVolume`.

---

## 5. SL geometry

```text
Long:  SL = anchor − buffer; anchor = VAH (or retest if inside VA)
Short: SL = anchor + buffer; anchor = VAL (or retest if inside VA)
Reject if |entry − VA edge| > MaxEntryExt × ATR   (E_CHASE)
Reject if slDist > MaxSl × ATR                    (X1_SL_CAP)
```

---

## 6. Exit wiring

| Feature | Parameter | Module |
| --- | --- | --- |
| Hard TP | TP RR Multiple (`0` = off) | Order TP at open |
| BE | Use BE, Start/Lock R | TrailingManager |
| Trail | Use Trailing, Start/Step R | TrailingManager |
| Session flat | SessionClock | ClosePosition full |

**Removed:** Exit Mode enum, Trail ATR Mult, time-stop.

---

## 7. File layout

```text
Robots/SvbsX/
  SvbsX.sln
  SvbsX/
    SvbsX.cs
    SignalEngine.cs
    SessionClock.cs
    SvbsX.csproj
  docs/
```

---

## 8. Test hooks

- Reject codes in journal  
- OPEN log: `vol`, `riskEst`, `riskAtSl`, `via=`, tick meta  
- Dry-run: `Enable Trading = false`  
