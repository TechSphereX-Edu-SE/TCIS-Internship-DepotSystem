# Dữ liệu demo depot (feedback 10)

Bộ demo nằm trong database thật (Postgres, volume `depot-postgres-data`), không phải dữ liệu test.
Code: `src/Infrastructure/Persistence/Demo/DemoCatalog.cs` (nội dung) và `DemoDataSeeder.cs` (cách tạo).
Unit test: `tests/Application.UnitTests/Features/Depot/DemoDataSeederTests.cs` (InMemory + đồng hồ cố định, không đụng DB demo).

## Cách dùng

| Việc | Cách làm | Kết quả |
|:--|:--|:--|
| Tạo lần đầu | Aspire dashboard → resource **api** → menu `...` → **Tạo / bổ sung dữ liệu demo** | Lưu **mốc ngày (anchor)** = thời điểm chạy, tạo 140 container và toàn bộ lịch sử tính lùi từ mốc |
| Bổ sung | Chạy lại đúng lệnh trên, lúc nào cũng được | Chỉ thêm container demo **chưa có** (theo số container), dùng **mốc cũ**, nên ngày giờ giống hệt lần đầu. Container demo đã có thì bỏ qua cả chuỗi sự kiện: không ghi đè thao tác của người dùng |
| Reset | **Reset dữ liệu demo** (có hộp xác nhận) | Xoá toàn bộ dữ liệu depot (container, lượt vào bãi, lệnh, sửa chữa, đối tác, khách hàng, block, mốc demo), **giữ tài khoản** và todo, rồi tạo lại với mốc mới. Ảnh sửa chữa trên đĩa không bị xoá (chỉ xoá dòng DB) |
| Gọi bằng Scalar | `POST /api/demo/seed`, `POST /api/demo/reset?confirm=reset-demo` với token Admin | Như trên |

- Route `/api/demo/*` chỉ tồn tại khi chạy **Development** qua AppHost (`Demo__Enabled=true`). Bản deploy không có.
- Lệnh trong dashboard gửi header `X-Demo-Key` (khoá mới mỗi lần chạy AppHost); người khác không có khoá hoặc token Admin nhận 403.
- App **không tự tạo hay reset** demo khi khởi động. Không có API nghiệp vụ nào nhận ngày giờ từ ngoài: seeder tự đặt giờ cho từng sự kiện rồi chạy qua đúng các handler của màn hình (gate-in, đảo chuyển, lệnh cấp rỗng, gate-out, giám định, dự toán, sửa ngoài), nên lịch sử, hồ sơ sửa chữa, vị trí đều đúng quy tắc.
- Kết quả lệnh (JSON): `anchorAt`, `anchorCreated`, `containersCreated`, `containersSkipped`, `stepsRun`, `failures`. Một bước bị quy tắc từ chối (vd. ô đã có container người dùng đặt) được ghi vào `failures` và log, các bước còn lại của container đó bị bỏ qua; container khác vẫn tạo bình thường.

## Nội dung

- **5 hãng** (vừa là chủ sở hữu vừa là hãng khai thác): MSC, CMA, HMM, ONE, EMC. Có sẵn mã thì dùng lại, chỉ bổ sung vai trò còn thiếu.
- **7 loại × (2 × 20ft + 2 × 40ft)** mỗi hãng = 140 container. Số container: tiền tố hãng + `8000TS` + số kiểm tra ISO 6346 (T = loại 0..6, S = 1..4; S 1-2 là 20ft, 3-4 là 40ft). ISO: 22G1/42G1, 22R1/45R1, 22U1/42U1, 22P1/42P1, 22T0/42T0, 22V0/42V0, 22S1/42S1.
- Phân công khai thác đã xác nhận cho hãng của chính container, từ ngày D−400, không ngày kết thúc (tham chiếu `DEMO-<hãng>`).
- Khách hàng của lệnh: `0312345678` Công ty CP Demo Logistics. Block: `A` (khô, 20×6×5), `B` (lạnh, 10×4×4), `WAIT` (ảo, chờ giám định / sửa) — có sẵn thì dùng lại.
- Vào bãi bình thường: ô trống đầu tiên của block (tầng thấp trước). Vào bãi hỏng và về từ sửa ngoài: `WAIT`. Biển số xe `51C-1xxxx`.
- Mỗi lần cấp rỗng có lệnh riêng `DEMO-<hãng>-<yyMMdd>-<stt>` tạo trước 2 giờ, hạn 7 ngày, tàu `DEMO VOYAGE 001N`, 1 container đúng loại và cỡ.
- Dự toán: mục 1 = hư hỏng lúc vào bãi, "Nắn phẳng, sơn lại", 1.500.000 đ (20ft) / 2.200.000 đ (40ft), hãng tàu trả; mục 2 = "Gioăng cửa rách", 800.000 đ, khách hàng trả. Phiên bản 2 giảm mục 1 còn 80%.

## Kịch bản

D = ngày của mốc (giờ Việt Nam). "D−9" = 9 ngày lịch trước D, sự kiện lúc 07:00–15:00. Sự kiện ngày D đặt vài giây trước mốc.
Số ngày lưu bãi = hôm nay − ngày vào bãi (`DepotTime.DwellDays`), nên **chỉ đúng 9/10/11 vào ngày D**; mỗi ngày sau đó tăng thêm 1. Ranh giới 10 ngày luôn được kiểm chính xác bằng unit test (đồng hồ cố định). Muốn xem lại ranh giới trên giao diện ở ngày khác: lọc theo ngày vào bãi (D−9, D−10, D−11) hoặc reset demo.

| # | Kịch bản | Diễn biến | Dùng để kiểm | Kết quả mong đợi (vào ngày D) |
|:-:|:--|:--|:--|:--|
| 1 | `NeverIn` | Chỉ có danh mục + phân công | Tìm container, lịch sử rỗng, gate-in lần đầu | Không có lượt vào bãi; gate-in được với hãng của container |
| 2 | `InYardToday` | Vào bãi D | Danh sách vào bãi hôm nay, tồn bãi | Trong bãi, lưu 0 ngày |
| 3 | `InYard9Days` | Vào D−9 | Thống kê lưu ngắn / lưu lâu | 9 ngày → **lưu ngắn** (< 10) |
| 4 | `InYard10Days` | Vào D−10 | Ranh giới 10 ngày | 10 ngày → **lưu lâu** (≥ 10) |
| 5 | `InYard11Days` | Vào D−11 | Ranh giới 10 ngày | 11 ngày → lưu lâu |
| 6 | `LongStay` | Vào D−75, đảo chuyển D−40 | Lưu rất lâu, lịch sử vị trí | Trong bãi 75 ngày; lịch sử có 1 lần di chuyển |
| 7 | `Released` | Vào D−50, cấp rỗng D−30 | Cấp rỗng theo lệnh, lịch sử lượt đã đóng | Không còn trong bãi; 1 lượt đã ra với lệnh DEMO-… |
| 8 | `ReturnedManyTimes` | Vào D−120, ra D−100, vào D−60, ra D−45, vào D−5 | Mỗi lần quay lại là lượt mới; thống kê vào/ra theo ngày | 3 lượt (2 đã ra), đang trong bãi 5 ngày |
| 9 | `DamagedAwaitingInspection` | Vào hỏng D−2 | Hồ sơ chờ giám định, chặn cấp rỗng | Ở `WAIT`, hồ sơ **Chờ giám định**; gate-out bị từ chối (409) |
| 10 | `InspectedNoRepair` | Vào hỏng D−6, giám định đạt D−5 | Hư nhẹ không sửa | Hồ sơ **Đạt**, container bình thường, cấp rỗng được |
| 11 | `AwaitingEstimateApproval` | Giám định D−14, dự toán v1 gửi D−13, bị từ chối D−11, v2 gửi D−10 | Phiên bản dự toán, ghi nhận duyệt | v1 **Bị từ chối**, v2 **Chờ duyệt**; hồ sơ **Chờ duyệt phương án** |
| 12 | `InRepair` | v1 duyệt D−17, bắt đầu sửa D−16, khách tranh chấp mục 2 D−15 | Người trả, trạng thái tranh chấp, lọc chi phí | Hồ sơ **Đang sửa**; mục 2: Khách hàng / **Tranh chấp** |
| 13 | `AwaitingAcceptance` | Hoàn thành D−24, nghiệm thu không đạt D−23, hoàn thành lại D−3 | Nghiệm thu, sửa lại | Hồ sơ **Chờ nghiệm thu** (đã một lần không đạt) |
| 14 | `RepairedAndReleased` | Đủ vòng sửa, nghiệm thu đạt D−83, cấp rỗng D−70 | Lịch sử sửa chữa đầy đủ của lượt cũ | Hồ sơ **Đạt**, không còn trong bãi |
| 15 | `AtOutsideRepair` | v1 duyệt D−9, yêu cầu sửa ngoài D−8, ra cổng đi sửa D−7 | Container đang ở xưởng ngoài | Không ở bãi; chuyển sửa ngoài **Đang sửa ngoài** (Xưởng sửa chữa Cát Lái); hồ sơ **Đã duyệt sửa** |
| 16 | `BackFromOutsideRepair` | Ra cổng đi sửa D−40, về D−25, sửa D−24, nghiệm thu đạt D−22 | Về từ sửa ngoài = lượt mới, gắn lại hồ sơ | 2 lượt; chuyển sửa ngoài **Đã về bãi**; hồ sơ **Đạt**; đang ở `WAIT`, cấp rỗng được |

## Danh sách container theo kịch bản

| Kịch bản | SL | Container (hãng, loại, cỡ) |
|:--|:--:|:--|
| 1. `NeverIn` | 9 | `MSCU8000019` MSC Khô 20, `CMAU8000043` CMA Khô 40, `CMAU8000439` CMA Bồn 40, `HMMU8000120` HMM Lạnh 20, `HMMU8000516` HMM Thông gió 20, `ONEU8000148` ONE Lạnh 40, `ONEU8000533` ONE Thông gió 40, `EMCU8000220` EMC Hở mái 20, `EMCU8000616` EMC Chuyên dụng 20 |
| 2. `InYardToday` | 9 | `MSCU8000024` MSC Khô 20, `MSCU800041X` MSC Bồn 20, `CMAU8000444` CMA Bồn 40, `HMMU8000136` HMM Lạnh 40, `HMMU8000521` HMM Thông gió 20, `ONEU8000214` ONE Hở mái 20, `ONEU8000549` ONE Thông gió 40, `EMCU8000236` EMC Hở mái 40, `EMCU8000621` EMC Chuyên dụng 20 |
| 3. `InYard9Days` | 9 | `MSCU800003X` MSC Khô 40, `MSCU8000425` MSC Bồn 20, `CMAU800011X` CMA Lạnh 20, `HMMU8000141` HMM Lạnh 40, `HMMU8000537` HMM Thông gió 40, `ONEU800022X` ONE Hở mái 20, `ONEU8000615` ONE Chuyên dụng 20, `EMCU8000241` EMC Hở mái 40, `EMCU8000637` EMC Chuyên dụng 40 |
| 4. `InYard10Days` | 9 | `MSCU8000045` MSC Khô 40, `MSCU8000430` MSC Bồn 40, `CMAU8000125` CMA Lạnh 20, `CMAU8000510` CMA Thông gió 20, `HMMU8000542` HMM Thông gió 40, `ONEU8000235` ONE Hở mái 40, `ONEU8000620` ONE Chuyên dụng 20, `EMCU8000318` EMC Mặt bằng 20, `EMCU8000642` EMC Chuyên dụng 40 |
| 5. `InYard11Days` | 8 | `MSCU8000111` MSC Lạnh 20, `MSCU8000446` MSC Bồn 40, `CMAU8000130` CMA Lạnh 40, `CMAU8000526` CMA Thông gió 20, `HMMU8000218` HMM Hở mái 20, `ONEU8000240` ONE Hở mái 40, `ONEU8000636` ONE Chuyên dụng 40, `EMCU8000323` EMC Mặt bằng 20 |
| 6. `LongStay` | 8 | `MSCU8000127` MSC Lạnh 20, `MSCU8000512` MSC Thông gió 20, `CMAU8000146` CMA Lạnh 40, `CMAU8000531` CMA Thông gió 40, `HMMU8000223` HMM Hở mái 20, `HMMU8000619` HMM Chuyên dụng 20, `ONEU8000641` ONE Chuyên dụng 40, `EMCU8000339` EMC Mặt bằng 40 |
| 7. `Released` | 8 | `MSCU8000132` MSC Lạnh 40, `MSCU8000528` MSC Thông gió 20, `CMAU8000212` CMA Hở mái 20, `CMAU8000547` CMA Thông gió 40, `HMMU8000239` HMM Hở mái 40, `HMMU8000624` HMM Chuyên dụng 20, `ONEU8000317` ONE Mặt bằng 20, `EMCU8000344` EMC Mặt bằng 40 |
| 8. `ReturnedManyTimes` | 8 | `MSCU8000148` MSC Lạnh 40, `MSCU8000533` MSC Thông gió 40, `CMAU8000228` CMA Hở mái 20, `CMAU8000613` CMA Chuyên dụng 20, `HMMU8000244` HMM Hở mái 40, `HMMU800063X` HMM Chuyên dụng 40, `ONEU8000322` ONE Mặt bằng 20, `EMCU800001X` EMC Khô 20 |
| 9. `DamagedAwaitingInspection` | 9 | `MSCU8000214` MSC Hở mái 20, `MSCU8000549` MSC Thông gió 40, `CMAU8000233` CMA Hở mái 40, `CMAU8000629` CMA Chuyên dụng 20, `HMMU8000310` HMM Mặt bằng 20, `HMMU8000645` HMM Chuyên dụng 40, `ONEU8000338` ONE Mặt bằng 40, `EMCU8000025` EMC Khô 20, `EMCU8000410` EMC Bồn 20 |
| 10. `InspectedNoRepair` | 9 | `MSCU800022X` MSC Hở mái 20, `MSCU8000615` MSC Chuyên dụng 20, `CMAU8000249` CMA Hở mái 40, `CMAU8000634` CMA Chuyên dụng 40, `HMMU8000326` HMM Mặt bằng 20, `ONEU8000019` ONE Khô 20, `ONEU8000343` ONE Mặt bằng 40, `EMCU8000030` EMC Khô 40, `EMCU8000426` EMC Bồn 20 |
| 11. `AwaitingEstimateApproval` | 9 | `MSCU8000235` MSC Hở mái 40, `MSCU8000620` MSC Chuyên dụng 20, `CMAU8000315` CMA Mặt bằng 20, `CMAU800064X` CMA Chuyên dụng 40, `HMMU8000331` HMM Mặt bằng 40, `ONEU8000024` ONE Khô 20, `ONEU800041X` ONE Bồn 20, `EMCU8000046` EMC Khô 40, `EMCU8000431` EMC Bồn 40 |
| 12. `InRepair` | 9 | `MSCU8000240` MSC Hở mái 40, `MSCU8000636` MSC Chuyên dụng 40, `CMAU8000320` CMA Mặt bằng 20, `HMMU8000012` HMM Khô 20, `HMMU8000347` HMM Mặt bằng 40, `ONEU800003X` ONE Khô 40, `ONEU8000425` ONE Bồn 20, `EMCU8000112` EMC Lạnh 20, `EMCU8000447` EMC Bồn 40 |
| 13. `AwaitingAcceptance` | 9 | `MSCU8000317` MSC Mặt bằng 20, `MSCU8000641` MSC Chuyên dụng 40, `CMAU8000336` CMA Mặt bằng 40, `HMMU8000028` HMM Khô 20, `HMMU8000413` HMM Bồn 20, `ONEU8000045` ONE Khô 40, `ONEU8000430` ONE Bồn 40, `EMCU8000128` EMC Lạnh 20, `EMCU8000513` EMC Thông gió 20 |
| 14. `RepairedAndReleased` | 9 | `MSCU8000322` MSC Mặt bằng 20, `CMAU8000017` CMA Khô 20, `CMAU8000341` CMA Mặt bằng 40, `HMMU8000033` HMM Khô 40, `HMMU8000429` HMM Bồn 20, `ONEU8000111` ONE Lạnh 20, `ONEU8000446` ONE Bồn 40, `EMCU8000133` EMC Lạnh 40, `EMCU8000529` EMC Thông gió 20 |
| 15. `AtOutsideRepair` | 9 | `MSCU8000338` MSC Mặt bằng 40, `CMAU8000022` CMA Khô 20, `CMAU8000418` CMA Bồn 20, `HMMU8000049` HMM Khô 40, `HMMU8000434` HMM Bồn 40, `ONEU8000127` ONE Lạnh 20, `ONEU8000512` ONE Thông gió 20, `EMCU8000149` EMC Lạnh 40, `EMCU8000534` EMC Thông gió 40 |
| 16. `BackFromOutsideRepair` | 9 | `MSCU8000343` MSC Mặt bằng 40, `CMAU8000038` CMA Khô 40, `CMAU8000423` CMA Bồn 20, `HMMU8000115` HMM Lạnh 20, `HMMU800044X` HMM Bồn 40, `ONEU8000132` ONE Lạnh 40, `ONEU8000528` ONE Thông gió 20, `EMCU8000215` EMC Hở mái 20, `EMCU800054X` EMC Thông gió 40 |
