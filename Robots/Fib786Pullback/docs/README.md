# Fib786Pullback

Structure break (new HH/LL) + deep Fib 78.6% pullback with candle confirmation.

## Quick start

- **Symbol:** XAUUSD  
- **TF:** M5 (or M15)  
- **Risk:** 0.5% default  
- **Sessions:** London + NY  

Build:

```bash
dotnet build -c Release Robots/Fib786Pullback/Fib786Pullback/Fib786Pullback.csproj
```

Copy `.algo` from `bin/Release/net6.0/` into cTrader Automate (or use repo `Robots/Fib786Pullback.algo` after publish).

## Core logic

1. Detect confirmed N-bar swing highs/lows.  
2. New HH → long setup; new LL → short setup.  
3. Fib from new extreme → prior opposite swing.  
4. Require impulse ≥ min ATR.  
5. Closed bar touches 78.6 zone + confirm candle → market entry.  
6. SL beyond impulse origin + buffer; TP = R × TpRR.

## Docs

See `PROJECT_ROOT.md` and `v1.0/1-prds/PRD-fib786-pullback.md`.
