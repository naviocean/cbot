---
name: mql5-clean-code
description: MQL5 Language Mastery, Project Structure Standards, and EA Checklist.
---

# MQL5 Clean Code & Architecture

## Core Expertise
- Deep understanding of MQL5 syntax, OOP, and event-driven programming
- Expert in `OnInit()`, `OnDeinit()`, `OnTick()`, `OnTimer()`, `OnCalculate()` lifecycle
- Proficient with CTrade, CPositionInfo, COrderInfo, CSymbolInfo classes
- Knowledge of indicator handles, buffers, and iCustom integration

## Project Structure Standards
All EAs should follow this directory structure:
```
MQL5/
├── Experts/
│   └── RedWave/                   # RedWave organization folder
│       └── [ProjectName]/         # EA-specific folder
│           └── [ProjectName].mq5  # Main EA file
├── Include/
│   ├── RWCommon/                  # Shared reusable modules
│   └── RedWave/                   # RedWave organization folder
│       └── [ProjectName]/         # Project-specific modules
├── Indicators/                    # Custom indicators
└── Scripts/                       # Utility scripts
```

## Coding Standards

### Input Parameters Convention
```mql5
// Group inputs logically with separators
input group "=== TRADE SETTINGS ==="
input double InpRiskPercent     = 1.0;      // Risk Per Trade (%)
input int    InpSL_Points       = 500;      // Stop Loss (Points)
input int    InpTP_Points       = 1000;     // Take Profit (Points)

input group "=== FILTERS ==="
input bool   InpUseEMAFilter    = true;     // Use EMA Trend Filter
input int    InpEMA_Period      = 200;      // EMA Period

input group "=== SESSION ==="
input bool   InpUseSessions     = true;     // Enable Session Filter
```

### Units Convention
- **User Inputs**: Always use **Pips** for user-facing distance values (SL, TP, trailing, etc.)
- **Internal Calculations**: Convert Pips to **Points** internally
```mql5
int pipToPoints = (_Digits == 3 || _Digits == 5) ? 10 : 1;
int slPoints = InpSL_Pips * pipToPoints;
```

### Magic Number Management
```mql5
input int InpMagicNumber = 123456;  // Magic Number
// In OnInit():
g_trade.SetExpertMagicNumber(InpMagicNumber);
```

### Resource Cleanup
```mql5
void OnDeinit(const int reason)
{
    // Release all indicator handles
    if(g_atrHandle != INVALID_HANDLE) IndicatorRelease(g_atrHandle);
    if(g_emaHandle != INVALID_HANDLE) IndicatorRelease(g_emaHandle);
    
    // Delete chart objects if created
    ObjectsDeleteAll(0, "EA_");
}
```

## EA Development Checklist

### 1. Initialization (`OnInit`)
- [ ] Initialize CTrade with magic number and slippage
- [ ] Create indicator handles
- [ ] Initialize RWCommon modules
- [ ] Validate input parameters
- [ ] Set filling mode based on broker support

### 2. Main Logic (`OnTick`)
```mql5
void OnTick()
{
    // 1. New bar check (if needed)
    if(!IsNewBar()) return;
    
    // 2. Apply filters
    if(!g_sessionFilter.IsSessionActive()) return;
    if(!g_timeFilter.IsTimeAllowed()) return;
    if(!g_marketCondition.IsConditionMet()) return;
    
    // 3. Check existing positions
    if(HasOpenPosition()) 
    {
        ManagePositions();
        return;
    }
    
    // 4. Signal generation
    int signal = GetSignal();
    
    // 5. Trade execution
    if(signal == 1) ExecuteBuy();
    else if(signal == -1) ExecuteSell();
}
```

### 3. Trade Management
- [ ] Trailing stop implementation
- [ ] Breakeven functionality
- [ ] Partial close at TP levels
- [ ] Maximum positions/trades per day limit

### 4. Risk Management
- [ ] Position sizing based on risk %
- [ ] Maximum drawdown protection
- [ ] Daily loss limit

## Performance Considerations
- Minimize calculations on every tick
- Cache values that don't change frequently
- Use `CopyBuffer()` efficiently (copy only needed values)
- Avoid string operations in hot paths
- Use `static` variables for persistence within functions

## Documentation Requirements
Every EA must include:
```mql5
//+------------------------------------------------------------------+
//|                                             EA_Name_V1.0.mq5     |
//|                                    Copyright 2024, Your Name     |
//|                                       Description of EA          |
//+------------------------------------------------------------------+
#property copyright "Your Name"
#property link      "https://yoursite.com"
#property version   "1.00"
#property description "Brief description of the EA strategy"
#property strict
```
