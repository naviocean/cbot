# PLAN — Implement ClusterWickLimit v1.0

**Status:** Done (2026-07-13)

## Deliverables

| Item | Path |
| --- | --- |
| Robot | `ClusterWickLimit/ClusterWickLimit.cs` |
| Signal engine | `ClusterWickLimit/SignalEngine.cs` |
| Project | `ClusterWickLimit.csproj` + `.sln` |
| Artifact | `Robots/ClusterWickLimit.algo` |

## Tasks

1. [x] Pure `SignalEngine` (cluster density + wick + arm prices)
2. [x] Robot OnBar arm / OnTick risk+trail
3. [x] Limit place with absolute SL/TP (single TpRR)
4. [x] Pending TTL / acceptance / approach / news / spread cancel
5. [x] BE/Trail via `CTrailingManager` with R→pips
6. [x] Common: Risk / Session / News / MarketCondition / Logger / PriceUtils
7. [x] Release build → `.algo`

## Verify

```bash
/usr/local/share/dotnet/dotnet build Robots/ClusterWickLimit/ClusterWickLimit/ClusterWickLimit.csproj -c Release
```
