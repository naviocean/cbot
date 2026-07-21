---
name: cbot-clean-code
description: Tiêu chuẩn viết code C# cho cTrader cBot, bao gồm kiến trúc, namespace, thuộc tính Parameter.
---

# cTrader cBot Clean Code Standards

## 1. Cấu trúc và Namespace
Mỗi file cBot bắt buộc phải chứa các references sau:
```csharp
using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
```
Đừng bao giờ để quên `cAlgo.API` hoặc `cAlgo.API.Internals`, lỗi `CS0234` sẽ xảy ra.

## 2. Thông số đầu vào (Parameters)
Sử dụng thuộc tính `[Parameter]` của C# thay vì `input` như MQL5.
```csharp
[Parameter("Stop Loss (pips)", DefaultValue = 10, MinValue = 1)]
public double StopLossPips { get; set; }
```

## 3. Kiến trúc Event
cBot hoạt động dựa trên các event. 
- `OnStart()`: Khởi tạo, gán giá trị indicator.
- `OnTick()`: Gọi mỗi khi có tick mới. Tránh logic nặng ở đây.
- `OnBar()`: Gọi khi một cây nến mới hình thành. Phù hợp cho chiến lược dựa trên nến.
- `OnStop()`: Dọn dẹp tài nguyên.

## 4. Báo lỗi và Xử lý Exceptions
C# bắt buộc phải có Try/Catch ở các đoạn code nhạy cảm. Không để exception văng ra làm crash cBot. In ra Log bằng hàm `Print()`.
