---
name: ea-debugging-patterns
description: Centralized Logging via CModuleBase, Log Levels, and Debugging Best Practices.
---
# Debugging & Error Handling

## Error Handling
```mql5
// Always check operation results
if(!g_trade.Buy(lots, _Symbol, 0, sl, tp, comment))
{
    Print("Order failed: ", GetLastError(), " | Retcode: ", g_trade.ResultRetcode());
    return false;
}

// Validate indicator handles
if(g_emaHandle == INVALID_HANDLE)
{
    Print("Failed to create EMA indicator handle");
    return INIT_FAILED;
}
```

## Centralized Logging with CModuleBase

All RWCommon modules inherit from `CModuleBase` which provides:
- Automatic module name prefixing in logs
- Consistent logging methods: `LogDebug()`, `LogInfo()`, `LogTrade()`, `LogWarn()`, `LogError()`
- Optional logger injection via `SetLogger()`

```mql5
#include <RWCommon/Logger.mqh>
#include <RWCommon/RiskManager.mqh>
#include <RWCommon/TradeExecutor.mqh>
#include <RWCommon/TrailingManager.mqh>

// Global logger instance
CLogger g_logger;

// Modules - can inject logger via constructor or SetLogger()
CRiskManager g_riskManager(GetPointer(g_logger));  // Constructor injection
CTradeExecutor g_tradeExecutor;
CTrailingManager g_trailingManager;

int OnInit()
{
   // Initialize logger first
   g_logger.Init("MyEA", InpLogLevel);  // LOG_DEBUG, LOG_INFO, LOG_TRADE, etc.
   
   // Inject logger to modules that weren't initialized via constructor
   g_tradeExecutor.SetLogger(GetPointer(g_logger));
   g_trailingManager.SetLogger(GetPointer(g_logger));
   
   // Initialize modules normally
   g_riskManager.Init(_Symbol, InpMagic);
   g_tradeExecutor.Init(_Symbol, InpMagic);
   g_trailingManager.Init(_Symbol, InpMagic, 14);
   
   return INIT_SUCCEEDED;
}
```

**Inside modules, logging uses inherited methods:**
```mql5
// In RiskManager.mqh (inherits from CModuleBase)
LogInfo(StringFormat("Initialized for %s", m_symbol));     // [RiskMgr] Initialized for XAUUSD
LogTrade(StringFormat("Order placed: #%I64u", ticket));    // [RiskMgr] Order placed: #12345
LogError("Failed to calculate lot size");                  // [RiskMgr] Failed to calculate lot size
LogDebug(StringFormat("Current DD: %.2f%%", drawdown));    // [RiskMgr] Current DD: 2.50%
```

### Advanced Debug Logging Calls
```mql5
// All RWCommon modules inherit LogX methods from CModuleBase
// Use StringFormat() for formatted messages (MQL5 doesn't support variadic functions)

LogInfo(StringFormat("Entry signal detected: %s at %.5f", 
        signal == 1 ? "BUY" : "SELL", price));

LogTrade(StringFormat("Opened position #%I64u Lots=%.2f SL=%.5f TP=%.5f",
         ticket, lots, sl, tp));

LogError(StringFormat("Order failed: %d | Retcode: %d", 
         GetLastError(), g_trade.ResultRetcode()));

LogDebug(StringFormat("ATR=%.5f | Spread=%d | Volatility=%.2f",
         atr, spread, volatility));
```

## Log Levels
```mql5
enum ENUM_LOG_LEVEL {
   LOG_ERROR = 0,   // Errors only
   LOG_WARN  = 1,   // + Warnings  
   LOG_TRADE = 2,   // + Trade actions (default)
   LOG_INFO  = 3,   // + Info messages
   LOG_DEBUG = 4    // All messages
};

// Set via input parameter
input ENUM_LOG_LEVEL InpLogLevel = LOG_TRADE;  // Log Level
```

## Verification Workflow
1. **Compile Check**: Ensure no errors/warnings
2. **Strategy Tester**: Run visual mode backtest
3. **Log Analysis**: Verify signal generation and trade execution
4. **Broker Compatibility**: Test with different filling modes
5. **Edge Cases**: Test with high spread, low volatility conditions
