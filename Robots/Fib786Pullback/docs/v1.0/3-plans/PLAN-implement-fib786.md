# PLAN — Implement Fib786Pullback v1.0

## Scope

Greenfield cBot under `Robots/Fib786Pullback/`, reuse `Sources/Common`.

## Steps

1. Scaffold csproj/sln + link Common  
2. `SignalEngine` — pivots, HH/LL leg, Fib 78.6, confirm, reject codes  
3. `Fib786Pullback.cs` — params, OnBar evaluate, market order, BE/trail  
4. Docs PROJECT_ROOT + PRD  
5. `dotnet build -c Release` → `.algo`  

## Out of scope v1

- Zigzag indicator API  
- HTF bias  
- Partial TP  
- Limit resting at 78.6  

## Verify

```bash
dotnet build -c Release Robots/Fib786Pullback/Fib786Pullback/Fib786Pullback.csproj
```

User: cTrader BT XAUUSD M5 tick, London+NY.
