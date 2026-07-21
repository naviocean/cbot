# ClusterWickLimit (CWL)

Fade micro liquidity clusters on XAU M1 after a strong wick rejection, using a retest limit outside the cluster. Full-size exit: one RR take-profit plus optional BE/trail in R.

## Quick start

| Item | Value |
| --- | --- |
| Symbol | XAUUSD |
| Timeframe | M1 |
| Platform | cTrader cBot (planned) |
| Spec | `docs/v1.0/1-prds/PRD-cluster-wick-limit.md` |
| Status | Implemented v1.0 — load `ClusterWickLimit.algo` |

## Core logic (summary)

1. Find a high/low **cluster** (≥3 touches in dynamic band) in last ~90 M1 bars.  
2. Confirm **closed-bar** wick reject at that level.  
3. Place **BuyLimit / SellLimit** outside cluster by `0.4 × tol`.  
4. SL beyond extreme wick; **one TP** at `TpRR × R`; BE/trail by R.

## Input parameters

See PRD § parameters table. Defaults are research starters, not live-optimized.
