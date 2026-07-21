# REPORT — Code Review ClusterWickLimit v1.0

**Status:** COMPLETED — findings addressed in code (2026-07-13)  
**Date:** 2026-07-13  
**Reviewer:** Antigravity (cbot-expert)  
**Resolution:** Grok (fix + rebuild)  
**Target Files:**  
- [ClusterWickLimit.cs](file:///Users/naviocean/cAlgo/Sources/Robots/ClusterWickLimit/ClusterWickLimit/ClusterWickLimit.cs)  
- [SignalEngine.cs](file:///Users/naviocean/cAlgo/Sources/Robots/ClusterWickLimit/ClusterWickLimit/SignalEngine.cs)  
- [PRD-cluster-wick-limit.md](file:///Users/naviocean/cAlgo/Sources/Robots/ClusterWickLimit/docs/v1.0/1-prds/PRD-cluster-wick-limit.md)

---

## 1. Executive Summary

Bản review này đối chiếu mã nguồn hiện tại của cBot **ClusterWickLimit** với tài liệu đặc tả yêu cầu **PRD v1.0**. 

Nhìn chung, cấu trúc code được thiết kế rất tốt, chia tách rõ ràng giữa phần tích hợp sàn (cTrader API) và lõi thuật toán/tín hiệu (SignalEngine). Tuy nhiên, đã phát hiện **01 lỗi logic nghiêm trọng (Critical Bug)** có thể khóa hoàn toàn khả năng giao dịch của bot sau trade đầu tiên, và **01 điểm sai lệch logic tìm kiếm cụm (Major Deviation)** có thể dẫn đến việc bỏ lỡ các tín hiệu giao dịch hợp lệ.

---

## 2. Detailed Findings

### Finding 1: Anti-spam Lock vĩnh viễn Level cũ (Critical Bug)
* **Vị trí ảnh hưởng:** [ClusterWickLimit.cs](file:///Users/naviocean/cAlgo/Sources/Robots/ClusterWickLimit/ClusterWickLimit/ClusterWickLimit.cs#L674-L686) và [SignalEngine.cs](file:///Users/naviocean/cAlgo/Sources/Robots/ClusterWickLimit/ClusterWickLimit/SignalEngine.cs#L185-L190)
* **Đặc tả PRD (mục 7):** *"Re-arm same level: Block until price leaves level by > 2 × tol (anti-spam)"*.
* **Vấn đề thực tế:** 
  Khi lệnh limit được khớp thành position, biến `_hasLastTradedCluster` được đặt thành `true` và `_lastTradedClusterLevel` lưu trữ mức giá của cluster đó. Tuy nhiên, trong toàn bộ mã nguồn của `ClusterWickLimit.cs`, **không có logic nào reset `_hasLastTradedCluster = false`** khi giá đóng cửa đã di chuyển xa khỏi mức đó (`> 2 * tol`).
* **Hệ quả:** 
  Một khi bot đã thực hiện 1 trade, nó sẽ nhớ mãi mãi level đó và bật trạng thái khóa anti-spam. Nếu giá di chuyển đi rất xa rồi quay lại chính level đó (sau vài giờ/ngày), tín hiệu mới tại đó sẽ bị từ chối vĩnh viễn (`REJECT:F_ANTISPAM`) vì giá đóng cửa lúc này lại nằm trong phạm vi `2.0 * tol` của `_lastTradedClusterLevel` cũ.
* **Đề xuất Fix:** Trong hàm `OnBar()` của `ClusterWickLimit.cs`, thêm kiểm tra để tự động tắt cờ hiệu `_hasLastTradedCluster` khi giá đóng cửa đã đi ra ngoài khoảng cách an toàn `2.0 * tol`.

---

### Finding 2: Logic Lựa chọn Cụm (Cluster Selection) (Major Deviation)
* **Vị trí ảnh hưởng:** [SignalEngine.cs](file:///Users/naviocean/cAlgo/Sources/Robots/ClusterWickLimit/ClusterWickLimit/SignalEngine.cs#L192-L198) và phương thức `FindBestCluster`
* **Đặc tả PRD (mục 5.1):** *"Selection: At most one sell cluster and one buy cluster: nearest to close[1] among valid clusters with approach ≤ MaxApproach."*
* **Vấn đề thực tế:** 
  Hàm `FindBestCluster` hiện tại chỉ trả về **duy nhất 1** cluster gần `close` nhất từ trước. Sau đó, bên ngoài hàm `Evaluate` mới kiểm tra điều kiện Wick Confirm và `MaxApproach`.
  Nếu cluster gần close nhất (Cluster A) thỏa mãn `MaxApproach` nhưng **fail Wick Confirm**, trong khi có một cluster khác xa hơn một chút (Cluster B, vẫn nằm trong `MaxApproach`) **thỏa mãn Wick Confirm**, thuật toán hiện tại sẽ chọn Cluster A $\rightarrow$ kiểm tra Wick thất bại $\rightarrow$ Reject tín hiệu, bỏ lỡ cơ hội giao dịch hợp lệ ở Cluster B.
* **Đề xuất Fix:** Thay đổi tham số truyền vào của `FindBestCluster` để nhận thêm `maxApproach`, giúp lọc bỏ các cụm vượt quá khoảng cách này trước khi sắp xếp chọn cụm gần nhất. Điều này đảm bảo cụm được chọn luôn hợp lệ về mặt khoảng cách, tránh việc bỏ sót các cụm khác.

---

### Finding 3: BE Lock buffer / BeLockR (Minor Deviation)
* **Vị trí ảnh hưởng:** [ClusterWickLimit.cs](file:///Users/naviocean/cAlgo/Sources/Robots/ClusterWickLimit/ClusterWickLimit/ClusterWickLimit.cs#L129-L133)
* **Đặc tả PRD (mục 4.5):** Quy định cứng `BeBuffer = 1 tick (or 0.01)`.
* **Vấn đề thực tế:** 
  Code giới thiệu thêm parameter `BeLockR` (mặc định = 0.0) để tính khoảng cách khóa theo bội số của R. Nếu `BeLockR = 0.0`, code fallback về 1 tick.
* **Đánh giá:** Đây là một cải tiến tốt, tăng tính linh hoạt cho bot và không ảnh hưởng xấu đến logic gốc của PRD (vẫn hoạt động đúng 1 tick nếu giữ mặc định `BeLockR = 0`). Tuy nhiên cần được ghi nhận để kiểm soát tham số.

---

## 3. Proposed Implementation Changes

### Fix 1: Thêm cơ chế tự động giải phóng Anti-spam trong `ClusterWickLimit.cs`

```diff
@@ -285,6 +285,17 @@
             // Manage existing pending first (TTL / invalidate) using latest closed bar
             ManagePendingOnBar();
 
+            // Auto-clear anti-spam lock when price leaves the zone by > 2 * tol
+            if (_hasLastTradedCluster)
+            {
+                int lastBi = Bars.Count - 2;
+                double lastClose = Bars.ClosePrices[lastBi];
+                double currentTol = _lastTradedTol > 0 ? _lastTradedTol : BaseBand;
+                if (Math.Abs(lastClose - _lastTradedClusterLevel) > 2.0 * currentTol)
+                {
+                    _hasLastTradedCluster = false;
+                    _logger.Debug($"Anti-spam cleared: price left level {_lastTradedClusterLevel:F2} by > 2*tol");
+                }
+            }
+
             int bi = Bars.Count - 2; // closed bar
             DateTime barTime = Bars.OpenTimes[bi];
             if (barTime <= _lastSignalBarTime)
```

### Fix 2: Tối ưu hóa việc lọc `MaxApproach` trong `FindBestCluster` của `SignalEngine.cs`

```diff
@@ -192,8 +192,8 @@
-            var sellCluster = FindBestCluster(ctx.Bars, lookback, tol, ctx.MinTouches, ctx.MaxClusterAgeBars, highSide: true, close);
-            var buyCluster = FindBestCluster(ctx.Bars, lookback, tol, ctx.MinTouches, ctx.MaxClusterAgeBars, highSide: false, close);
+            var sellCluster = FindBestCluster(ctx.Bars, lookback, tol, ctx.MinTouches, ctx.MaxClusterAgeBars, highSide: true, close, ctx.MaxApproach);
+            var buyCluster = FindBestCluster(ctx.Bars, lookback, tol, ctx.MinTouches, ctx.MaxClusterAgeBars, highSide: false, close, ctx.MaxApproach);
```

```diff
@@ -284,7 +284,8 @@
             int minTouches,
             int maxAge,
             bool highSide,
-            double close)
+            double close,
+            double maxApproach)
         {
             var raw = new List<ClusterInfo>();
 
@@ -324,6 +325,10 @@
                 if (extremes.Count < minTouches || newest > maxAge)
                     continue;
 
+                // Filter by MaxApproach before selecting
+                if (Math.Abs(median - close) > maxApproach)
+                    continue;
+
                 raw.Add(new ClusterInfo
                 {
                     Side = highSide ? SignalSide.Short : SignalSide.Long,
```

---

## 4. Resolution (applied)

| Finding | Verdict | Action |
| --- | --- | --- |
| F1 Anti-spam never clears | **Valid Critical** | `OnBar`: clear `_hasLastTradedCluster` when `\|close − level\| > 2×tol` |
| F2 Nearest-only cluster | **Valid Major** | Stronger than review patch: `FindClusters` (list within approach, nearest first) + **first wick-confirm** wins — not only MaxApproach filter on single pick |
| F3 BeLockR | **Acceptable** | No code change; default `BeLockR=0` → 1 tick as PRD |

**Build:** Release OK → `Robots/ClusterWickLimit.algo`
