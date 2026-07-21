---
name: rwcommon-library-patterns
description: Usage patterns for RedWave RWCommon Library (RiskManager, TradeExecutor, TrailingManager, MultiPositionManager, etc.)
---

# RWCommon Library Patterns

Always leverage existing reusable modules from `Include/RWCommon/`:

| Module | Purpose |
|--------|---------|
| `ModuleBase.mqh` | Base class for all modules - provides centralized logging |
| `Logger.mqh` | Core logging with levels: DEBUG, INFO, TRADE, WARN, ERROR |
| `RiskManager.mqh` | Lot sizing, risk % per trade, drawdown limits, daily trade tracking |
| `TradeExecutor.mqh` | Order execution, SL/TP handling, filling modes |
| `TrailingManager.mqh` | Trailing stops (Fixed, ATR, Percent modes), breakeven, partial close |
| `MultiPositionManager.mqh` | Multi-position management with per-position trailing/BE/partial config |
| `SessionFilter.mqh` | Asian/London/NY session filtering |
| `TimeFilter.mqh` | Day-of-week, hour-range filtering |
| `MarketCondition.mqh` | Spread/volatility filtering |
| `StructureAnalyzer.mqh` | Swing high/low, pivot detection, market structure |
| `NewsFilter.mqh` | Economic calendar integration |
| `BasketManager.mqh` | Basket/grid position management |

## RiskManager - Trade Tracking
```mql5
#include <RWCommon/RiskManager.mqh>
CRiskManager g_riskManager;

// In OnInit():
g_riskManager.Init(_Symbol, InpMagic);
g_riskManager.SetEquityProtection(5.0);     // 5% max daily drawdown
g_riskManager.SetMaxDailyTrades(10);        // Max 10 trades/day
g_riskManager.SetDefaultRisk(1.0);          // 1% risk per trade

// In OnTick():
g_riskManager.OnTick();                     // Updates daily stats

// After placing trade:
ulong ticket = g_tradeExecutor.PlaceMarketOrder(...);
if(ticket > 0) {
   g_riskManager.IncrementDailyTrades();    // Count trade when opened
}

// Track trade close via OnTradeTransaction():
void OnTradeTransaction(const MqlTradeTransaction& trans, 
                        const MqlTradeRequest& request, 
                        const MqlTradeResult& result)
{
   if(trans.type != TRADE_TRANSACTION_DEAL_ADD) return;
   // ... validate deal belongs to this EA ...
   double totalProfit = profit + commission + swap;
   g_riskManager.OnTradeClose(totalProfit);  // Updates daily P/L
}
```

## TrailingManager - Init Modes
```mql5
#include <RWCommon/TrailingManager.mqh>
CTrailingManager g_trailingManager;

// MODE 1: Standard Init (creates own ATR handle)
g_trailingManager.Init(_Symbol, InpMagic, 14);  // ATR period = 14

// MODE 2: InitWithExternalATR (shared ATR handle)
int sharedATRHandle = iATR(_Symbol, InpSignalTF, 14);
g_trailingManager.InitWithExternalATR(_Symbol, InpMagic, sharedATRHandle);
// IMPORTANT: Caller must release sharedATRHandle in OnDeinit()

// Configure trailing mode (pick one):
g_trailingManager.SetTrailPoints(500, 200);           // FIXED: start 500pts, step 200pts
g_trailingManager.SetTrailATR(1.5, 300);              // ATR: 1.5x ATR, start at 300pts profit
g_trailingManager.SetTrailPercent(50.0, 300);         // PERCENT: 50% of max profit

// Configure break-even:
g_trailingManager.SetBreakevenPoints(300, 50);        // Activate at 300pts, lock 50pts profit

// Configure partial close:
g_trailingManager.SetPartialClosePoints(500, 50.0);   // Close 50% at 500pts profit
```

## MultiPositionManager Usage
Use `MultiPositionManager` when EA needs to manage multiple concurrent positions with independent trailing/BE/partial close:

```mql5
#include <RWCommon/MultiPositionManager.mqh>

CMultiPositionManager g_multiPosManager;

// In OnInit():
g_multiPosManager.Init(_Symbol, InpMagic, InpSignalTF, InpATRPeriod);

// After opening a position - configure per-position management:
void ConfigurePositionManagement(ulong ticket, double slDistance, double openPrice)
{
   // Dynamic scaling based on actual SL vs baseline SL
   double scaleFactor = (slDistance / g_pipValue) / InpBaselineSLPips;
   
   SPositionConfig config;
   config.Reset();
   config.ticket = ticket;
   config.openPrice = openPrice;
   config.openTime = TimeCurrent();
   
   // Trailing config
   config.trailMode = MULTI_TRAIL_FIXED;  // or ATR, PERCENT
   config.trailStartPoints = InpTrailStartPips * scaleFactor * g_pipValue / g_point;
   config.trailStepPoints = InpTrailStepPips * scaleFactor * g_pipValue / g_point;
   
   // Break-even config  
   config.enableBE = true;
   config.beActivationPoints = InpBEActivationPips * scaleFactor * g_pipValue / g_point;
   config.beBufferPoints = InpBEBufferPips * g_pipValue / g_point;
   
   // Partial close config
   config.enablePartial = true;
   config.partialTriggerPoints = slDistance * InpTP_RR / g_point;
   config.partialPercent = 50.0;
   
   g_multiPosManager.AddPosition(ticket, config);
}

// In OnTick():
g_multiPosManager.OnTick();  // Manages all positions automatically

// In OnDeinit():
g_multiPosManager.Deinit();
```

## Non-Repainting Signal Logic
```mql5
// Always use confirmed (closed) bars for signals
int shift = 1;  // Previous bar (confirmed)
double close1 = iClose(_Symbol, PERIOD_H4, 1);
double close2 = iClose(_Symbol, PERIOD_H4, 2);
```

## iCustom Integration
```mql5
// Get indicator handle
int indicatorHandle = iCustom(_Symbol, _Period, "Indicators/MyIndicator",
    param1, param2, param3);

// Read buffer values
double bufferVal[];
if(CopyBuffer(indicatorHandle, 0, 1, 1, bufferVal) < 0)
{
    Print("Failed to copy indicator buffer");
    return 0;
}
```

## Multi-Timeframe Analysis
```mql5
// Higher TF for trend
ENUM_TIMEFRAMES trendTF = PERIOD_H4;
int emaHandleHTF = iMA(_Symbol, trendTF, 200, 0, MODE_EMA, PRICE_CLOSE);

// Signal TF for entries
ENUM_TIMEFRAMES signalTF = PERIOD_M15;
```
