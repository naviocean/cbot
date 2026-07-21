---
name: mt5-to-cbot-migration
description: Hướng dẫn chuyển đổi từ MQL5 sang cTrader C#, bản đồ các API và những khác biệt quan trọng.
---

# MT5 to cBot Migration Guide

Khi chuyển đổi code từ MQL5 sang cTrader C#, tuân thủ các ánh xạ sau:

## 1. Lệnh giao dịch (Execution)
- MT5: `OrderSend`
- cTrader Market: `ExecuteMarketOrder(TradeType, SymbolName, VolumeInUnits, label, stopLossPips, takeProfitPips)`
- cTrader Pending: `PlaceStopOrder`, `PlaceLimitOrder`

## 2. Pips và Points
- **CỰC KỲ QUAN TRỌNG:** Ở cTrader, thuộc tính `Symbol.PipSize` của mã Vàng (XAUUSD) mặc định là `0.1` USD, thay vì `0.01` như MT5.
- Nếu bạn code StopLoss/TakeProfit dựa trên Pip, bạn phải quy chuẩn thủ công nếu cần đồng bộ hành vi với MT5.
- MT5 `_Point` tương đương cTrader `Symbol.TickSize`.
- Hàm `Point()` trong MT5 thường ánh xạ với `Symbol.PipSize` nhưng phải kiểm tra trường hợp XAU.

## 3. Thời gian và Backtesting
- MT5: Dùng `GetTickCount` hoặc các hàm Time tương tự.
- cTrader: Cấm sử dụng `System.Diagnostics.Stopwatch` cho logic độ trễ/timeout trong backtest, vì Stopwatch đo giờ thực của máy tính, khiến cBot bỏ qua logic khi backtest nhanh. BẮT BUỘC dùng `Server.Time` để so sánh.

## 4. Quản lý trạng thái (State)
- MT5: `PositionsTotal()`, `PositionGetSymbol()`...
- cTrader: Truy xuất trực tiếp qua collection `Positions` và `PendingOrders`.
