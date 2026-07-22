# Kế hoạch Triển khai (Implementation Plan): Common/VolumeProfileV2.cs

> **Mục tiêu**: Xây dựng module `Common/VolumeProfileV2.cs` (`RedWave.Common.CVolumeProfileV2`) nâng cấp toàn diện thuật toán Volume Profile cho cBot từ `Free Volume Profile v2.0.cs`, bảo tồn tính tương thích ngược và giữ nguyên hiệu năng siêu nhẹ cho cTrader Strategy Tester.
> **Vị trí Module**: `Common/` (Thư viện dùng chung cho toàn bộ cBot & Indicator).
> **Vị trí Document**: `Common/docs/PLAN-VolumeProfileV2.md`
> **Ngày lập**: 21/07/2026  
> **Người lập**: Algo Strategist / cBot Expert  

---

## 1. Quyết định Kiến trúc (Architectural Decision)

### ❓ Tạo `VolumeProfileV2.cs` hay nâng cấp trực tiếp `VolumeProfile.cs`?
**Quyết định**: **Tạo file mới `Common/VolumeProfileV2.cs` (Class `CVolumeProfileV2`)**.

**Lý do**:
1. **Bảo vệ Codebase hiện tại (Zero Regression Risk)**: `Common/VolumeProfile.cs` (`CVolumeProfile`) đang chạy ổn định trong các cBot cũ. Việc tạo file mới đảm bảo $100\%$ không làm đứt gãy (break) code hiện tại.
2. **Định danh phiên bản rõ ràng (Version Isolation)**: `CVolumeProfileV2` đánh dấu bước chuyển mình sang Order Flow & Composite Profiling nâng cao (M1 Source Bars + Delta Bins + Gaussian Filter + Day Caching).
3. **Mở rộng `ProfileData.cs` an toàn**: Mở rộng class `ProfileData` bằng cách thêm các trường mới có default value / null-check, không ảnh hưởng đến code đang đọc `ProfileData`.

---

## 2. Các Tính năng Trọng tâm của `CVolumeProfileV2`

| # | Tính năng | Mô tả chi tiết |
|---|---|---|
| 1 | **High-Precision Source Bars** | Hỗ trợ nạp `Bars sourceBars` (e.g. nến M1) để dựng Volume Profile cho chart H1/H4 với độ chuẩn xác tuyệt đối. |
| 2 | **Order Flow Delta Bins** | Tính song song `UpHistogram[]` (Buy Vol), `DownHistogram[]` (Sell Vol) và `DeltaHistogram[]` (Net Delta) trên từng bin giá. |
| 3 | **1D Gaussian Smoothing** | Áp dụng nhân lọc nhiễu Gaussian $1\text{D}$ 5-tap kernel ($[0.06, 0.24, 0.40, 0.24, 0.06]$) loại bỏ nhiễu răng cưa trước khi phát hiện HVN/LVN. |
| 4 | **Incremental Day Caching** | Lưu cache mảng histogram của các ngày quá khứ ($D-1, D-2,\dots$). Khi tick/nến mới nhảy, chỉ re-calculate ngày hiện tại ($D_{\text{live}}$), giảm $80\%$ overhead CPU. |

---

## 3. Cập nhật Data Structure (`ProfileData.cs`)

Bổ sung các trường dữ liệu Order Flow vào `Common/ProfileData.cs`:

```csharp
public sealed class ProfileData
{
    // === Các trường hiện có (Giữ nguyên 100%) ===
    public double BinSize { get; set; }
    public double MinPrice { get; set; }
    public double MaxPrice { get; set; }
    public double[] Histogram { get; set; }
    public double TotalVolume { get; set; }
    public double POC { get; set; }
    public double VAH { get; set; }
    public double VAL { get; set; }
    public int PocBin { get; set; }
    public ProfileShape Shape { get; set; }
    public double VolAbovePoc { get; set; }
    public double VolBelowPoc { get; set; }
    public double PocRelative { get; set; }
    public List<VolumeNode> Hvns { get; set; }
    public List<VolumeNode> Lvns { get; set; }
    public DateTime BuiltAt { get; set; }
    public int BarsUsed { get; set; }
    public int LookbackDays { get; set; }
    public bool IsValid { get; set; }

    // === BỔ SUNG MỚI (Order Flow Delta Extensions) ===
    public double[] UpHistogram { get; set; }    // Buy Volume per Bin
    public double[] DownHistogram { get; set; }  // Sell Volume per Bin
    public double[] DeltaHistogram { get; set; } // Net Delta (Buy - Sell) per Bin
    public double PocUpVolume { get; set; }      // Buy Volume riêng tại POC
    public double PocDownVolume { get; set; }    // Sell Volume riêng tại POC
    public double PocDelta => PocUpVolume - PocDownVolume; // Net Delta tại POC
    public bool HasOrderFlowData { get; set; }   // True nếu được tính từ Source Bars / Delta
}
```

---

## 4. Chi tiết Kế hoạch Triển khai (Task Breakdown)

### Phase 1: Mở rộng `ProfileData.cs`
- [x] Thêm các trường Order Flow (`UpHistogram`, `DownHistogram`, `DeltaHistogram`, `PocUpVolume`, `PocDownVolume`, `HasOrderFlowData`).
- [x] Bổ sung helper methods: `GetBinDelta(int bin)`, `GetBinBuyVolume(int bin)`, `GetBinSellVolume(int bin)`.

### Phase 2: Triển khai Core Engine `CVolumeProfileV2.cs`
- [x] Khởi tạo class `CVolumeProfileV2` trong namespace `RedWave.Common`.
- [x] Thêm overload `Init(Bars bars, Bars sourceBars = null, Chart chart = null, ...)`:
  * Nếu `sourceBars` != null $\rightarrow$ dùng nến nhỏ (M1) gom volume.
  * Nếu `sourceBars` == null $\rightarrow$ fallback dùng `bars` chính.
- [x] Xây dựng hàm `DistributeBarVolumeV2`:
  * Phân tách Volume nến M1 thành Buy/Sell dựa vào chiều nến ($Close \ge Open \rightarrow \text{Buy}$, $Close < Open \rightarrow \text{Sell}$) hoặc tick direction.
  * Phân bổ đều vào các bin giá tương ứng.

### Phase 3: Thuật toán Gaussian Smoothing & Node Detection
- [x] Triển khai hàm `ApplyGaussianSmooth(double[] input)`:
  $$\text{Smoothed}[i] = \sum_{k=-2}^{2} \text{input}[i+k] \times K[k+2]$$
  với $K = [0.06, 0.24, 0.40, 0.24, 0.06]$.
- [x] Cập nhật `DetectNodesV2`:
  * Chạy Gaussian filter lên mảng volume tổng trước khi so sánh với `hvnCut` và `lvnCut`.
  * Tìm cực trị địa phương (local min/max) mượt mà hơn.

### Phase 4: Incremental Day Caching (Tối ưu Performance)
- [x] Thiết lập struct cache ngày `DayProfileCache` chứa mảng histogram của ngày đó.
- [x] Trong `BuildComposite`:
  * Kiểm tra dictionary cache `_dayCache`.
  * Nếu ngày $D$ đã đóng cửa và có trong cache $\rightarrow$ tái sử dụng mảng volume ngày $D$.
  * Chỉ tính toán lại cho ngày hiện tại (Forming Day).

---

## 5. Quy trình Kiểm thử & Xác minh (Verification Plan)

| Tiêu chí | Phương pháp kiểm thử | Kết quả mong đợi |
|---|---|---|
| **Tính tương thích** | Compile toàn bộ project `PocAbsorption` và các cBot khác. | Không phát sinh lỗi compile, các cBot cũ hoạt động bình thường. |
| **Độ chính xác POC/VA** | So sánh kết quả `CVolumeProfileV2` và `Free Volume Profile v2.0` trên cùng mốc thời gian. | Mốc POC, VAH, VAL lệch không quá $1$ bin size. |
| **Hiệu năng Backtest** | Chạy Backtest 1 năm dữ liệu M1 trên cTrader Strategy Tester. | Thời gian thi hành giảm $\ge 50\%$ so với việc tính lại full composite mỗi bar. |
