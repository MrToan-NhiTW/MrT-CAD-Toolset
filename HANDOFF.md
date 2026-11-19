# Bàn giao 2.0.2.1

Nhánh **feat/rooms-2.0.2.1** tiếp nối **fix/2.0.2.0**. Bộ cài offline ở **releases/MrT_CAD_Toolset_2.0.2.1_Setup.exe**.

MRTROOMS cho chọn cột Excel có tiêu đề và ví dụ, layer chữ cũ, layer đích và kiểu chữ. Mặc định đích là **Mr.T Tên phòng** và **Mr.T Arial**. Tìm thủ công hỗ trợ nhận chữ có sẵn hoặc phóng tới chữ để đặt điểm. Rà soát theo layer đối chiếu TEXT/MTEXT trực tiếp trong Model hoặc Layout hiện hành, ghi nhận tên thiếu/trùng, chỉ nhận ứng viên duy nhất sau khi xem trước. Đối tượng đã nhận giữ handle, vị trí và độ cao. ID vẫn dùng khi cập nhật lại từ Excel.

Đã build và kiểm thử đọc Excel, giao diện, đối chiếu tên trùng và ID đã gắn. Đã kiểm tra bộ cài nhận bản 2.0.2.0 và khớp 135 tệp trong MSI. SHA-256 EXE **F43FC0B25F50517A861DD55CCA3384E65B908791DD4B3A74D5CCED418CF71659**. Ca DWG MRTROOMS2016TEST đã biên dịch. Core Console trên máy build dừng khi mở profile trước khi chạy lệnh; cần kiểm tra trên bản sao DWG trong CAD 2024.

# Bàn giao 2.0.2.0

Nhánh **fix/2.0.2.0** kế thừa 2.0.1.11. Bộ cài thử ở **releases/MrT_CAD_Toolset_2.0.2.0_Setup.exe** theo yêu cầu đồng bộ hai máy.

- Chuyển ABC tự động giữ lại chuỗi nhập nhằng như PHÒNG, cả trong prompt ATTDEF.
- MRTZ0PL bỏ qua cung nghiêng và đoạn chiếu co về một điểm, bảo toàn độ rộng đỉnh và chuyển liên kết Isolate sang polyline thay thế.
- Bảng ký hiệu yêu cầu pick lại khi mất ống nguồn và các ống thay thế khác màu hoặc lineweight.
- Các project của solution lấy đủ tệp nguồn giống build.ps1.

Bản đã build bằng build.ps1 và MSBuild Release|x64. Kiểm tra MSI nhận bản cũ 2.0.1.11 và xác minh 135 tệp trong CAB. SHA-256 bộ cài **BE8783301FDCF3AE390E226770FBDF66A07CEBAE137AB3A624361051D7AFAE23**. Bộ mô phỏng đọc thuộc tính đã chạy 13 ca trên LISP đóng gói. Hai lệnh kiểm thử DWG MRTZ0PL2020TEST và MRTLEGSYNC2015TEST nằm trong bin/LegendPipeSyncRuntime.dll. Core Console trên máy build gặp lỗi khởi tạo profile/registry trước khi chạy hai lệnh. Chờ thử trong AutoCAD 2024.

## Nhật ký bàn giao 2.0.1.11

## Quy trình: chạy offline trên máy trước, đẩy GitHub sau

- Dự án có **bản làm việc offline** trên máy tính của người dùng (`C:\Projects\MrT-CAD-Toolset`). Mọi thay đổi được build, đóng gói bộ cài EXE và chạy thử ngay trên máy đó trước: `build.ps1`, `packaging/test-package.ps1` (24 PASS), thử trong AutoCAD 2024 trên **bản sao** DWG, rồi người dùng tự cài bộ cài thử và kiểm tra.
- **Chỉ khi người dùng xác nhận ổn mới commit/đẩy lên GitHub.** Vì vậy GitHub có thể chậm hơn bản offline: có thể có commit chưa đẩy, hoặc bộ cài thử mới chưa có trên GitHub.
- Bộ cài thử (`installer/MrT_CAD_Toolset_<phiên bản>_Setup.exe`) **không nằm trong git**; chỉ có ở máy đã build. Máy khác muốn có thì build lại từ mã nguồn (kiểm SHA-256 ghi ở mục phiên bản tương ứng nếu cùng mã) hoặc người dùng chép tay.
- Tại thời điểm bàn giao (18/11/2026) bản offline và GitHub trùng nhau ở commit `34894cd` (2.0.1.11), PR #2 vào `test/combined-2026-11-17`, PR #1 vào `main`; cả hai chưa gộp. `main` vẫn là bản cũ — tiếp tục từ `feat/density-r-z0`.
- **Khi tiếp tục ở máy khác (Codex hoặc phiên khác):**
  - Trước khi bắt đầu, hỏi người dùng bản offline có thay đổi nào chưa đẩy không; không mặc định GitHub là mới nhất.
  - Làm theo cùng quy trình trên máy đó: tạo nhánh riêng, build và kiểm thử offline, tạo bộ cài thử, chờ người dùng thử và đồng ý rồi mới đẩy. Không tự đẩy hay gộp khi chưa được đồng ý.
  - Mỗi bộ cài thử cần phiên bản và ProductCode mới (đổi trong `build-msi.ps1`, `PackageContents.xml`, thêm mã cũ vào `tests/PackagingTests.cs`). Hai máy làm song song phải thống nhất số phiên bản kế tiếp (2.0.1.12…) để không trùng.
  - Ghi kết quả (phiên bản, ProductCode, SHA-256, đã kiểm gì, chưa kiểm gì) vào file này trước khi đẩy.
  - Không gắn vào AutoCAD người dùng đang mở; chỉ tự khởi động phiên riêng và mở bản sao trong thư mục nháp. Không đổi SECURELOAD. Không đưa DWG thật lên GitHub.
- Việc đang chờ: giao diện bộ cài Setup.exe (`packaging/SetupProgress.cs`, `Setup.cs`, `build-setup.ps1`) cần thanh/khung tiến trình và cùng thiết kế dải tiêu đề tối như các hộp thoại 2.0.1.10 (người dùng rất hài lòng với thiết kế đó). Bộ cài phải giữ offline, tự chứa.

## Điểm xuất phát

- Đây là bản thử được tạo từ mã nguồn 2.0.1.6 tại máy cơ quan. Build, các kiểm thử logic/giao diện và kiểm tra payload/upgrade của bộ cài đã chạy thành công.
- Không xác nhận được kiểm thử AutoCAD native cho các thay đổi 2.0.1.5 và 2.0.1.6: AutoCAD Core Console không khởi tạo được hồ sơ/registry trong môi trường tác vụ. Người dùng tự kiểm tra trên bản sao DWG. Không mô tả các chức năng này như đã kiểm thử AutoCAD thành công.
- Bộ cài 2.0.1.6 SHA-256: `D257E8786CD33F0FA863ADC82B21C87B37E5756E940FF0E554F24D94D397E5D6`.

## Tên phòng — MRTROOMS

- Bản thử đọc `.xlsx`/`.xlsm`, cho chọn sheet, dòng tiêu đề, cột tên cũ, tên mới và ID; mặc định ID là tên cũ ban đầu. Đọc kết quả đã lưu của công thức. Phòng mới do công cụ đặt là MText trực tiếp; dữ liệu ID/cũ/mới lưu trên đối tượng. Không quét bên trong block/Xref.
- Có gắn từng vị trí, chuyển cũ/mới, nạp lại Excel và xem trước. Dòng ID trùng với nội dung xung đột, đối tượng sửa tay và layer khóa được báo/bỏ qua.
- Chưa nhận file Excel thực tế của người dùng để đối chiếu cấu trúc. Chỉ mới thử bằng dữ liệu giả. `.xls` chưa hỗ trợ trong bản hiện tại.
- Người dùng đã thống nhất: khi **hai phòng cũ nhập thành một**, có thể giữ hai vị trí cũ và cho hai nhãn hiện cùng tên mới; mỗi nhãn giữ ID gốc để quay lại tên cũ.
- Khi **một phòng cũ tách thành nhiều phòng mới**, cần phát triển thao tác tách: giữ quan hệ với ID gốc, mỗi phòng con có ID phân biệt, người dùng chọn vị trí, chuyển cũ/mới thì hiện đúng số nhãn. Chưa triển khai. Chờ Excel thực tế để chọn cấu trúc đối chiếu; không tự chia số liệu hoặc sửa hình học tường phòng.
- Trùng tên gốc nhưng cần tên mới khác nhau: dùng thêm tầng hoặc mã phân biệt. Tránh tự đoán.

## Bảng ký hiệu và mật độ

- 2.0.1.5 thêm đồng bộ mẫu ống trong bảng theo ống nguồn sau MRTDENSITY và lệnh MRTLEGSYNC. Hệ số tỷ lệ/độ rộng mẫu vẫn riêng. Trường hợp nhiều nguồn không xác định duy nhất cần báo, không đoán. Chưa kiểm thử native AutoCAD thành công.
- Với bản vẽ mm, tỷ lệ in tách riêng, không tự chia 1000 theo INSUNITS. Duy trì TABLE + WIPEOUT + GROUP và không làm thay đổi các biểu tượng block.
- Các vấn đề batch kiểm tra chiều R và bản vẽ thực tế từng được người dùng báo cần rà soát; không giả định đã xác nhận toàn bộ chỉ vì các kiểm thử logic chạy thành công.

## Bối cảnh dự án cần giữ

- Người dùng muốn bộ cài EXE offline sau mỗi bản phát triển; họ tự chạy thử. Đóng AutoCAD trước khi cài và tránh nạp nhiều bản cùng phiên. Không yêu cầu NETLOAD làm quy trình giao mặc định.
- `MRTALIGNR`: người dùng từng báo chọn riêng một đường chữ R thì sửa được, còn chọn toàn bộ bản vẽ thì vẫn còn sót. Ảnh lượt chọn riêng báo một polyline cần tách. Nguyên nhân gốc của lượt hàng loạt chưa được chứng minh. Đối chiếu lựa chọn thực tế, phạm vi, số đối tượng xử lý/bỏ qua và lý do F2; không gọi quét layer giới hạn là toàn bộ DWG. Người dùng cho phép tách khi có xem trước, hiểu số đối tượng thay đổi, ưu tiên nét native thay vì MText.
- `MRTJOIN` là nối nhanh LINE/LWPOLYLINE, khe mặc định 0,5 đơn vị và giữ cao độ; Z0 là bước riêng. Đối tượng có metadata/XDATA cần xử lý thận trọng. Không khẳng định mọi trường hợp sau JOIN và đảo R đã chạy đúng trên DWG thật.
- `MRTISO`, `MRTVIEW`, `MRTUNISO`: người dùng từng báo bối cảnh trước isolate bị thiếu ống trong khi MRTUNISO làm chúng hiện lại. Bản sửa là thử nghiệm, chưa có xác nhận đã hết; cần kiểm tra đối tượng ẩn sẵn, layer và layout.
- Chuyển font: người dùng từng báo lỗi chữ Unicode trong block động và chữ `PHÒNG`. Không ép chuyển mã hai lần; kiểm tra kỹ trên bản sao, gồm TEXT/MTEXT/attribute, TCVN3 và Unicode; không coi kiểm thử logic là đã xác nhận mọi font.
- Quản lý ống: hỗ trợ tên hệ tiếng Việt, tiền tố/hậu tố tùy chọn, tên nét dễ đọc; không tự ép hoặc xóa các định nghĩa cũ khi chưa xác định nguồn. Mật độ tính theo Model mm và tỷ lệ in; kiểm tra bản in/viewport.
- Bối cảnh công cụ và ví dụ trong `packaging/help.html` có thể chứa nội dung lịch sử. Khi sửa, đối chiếu hành vi và kiểm thử hiện hành thay vì tin toàn bộ hướng dẫn cũ.

## Rà soát và sửa ngày 17/11/2026 (chưa hợp nhất vào main)

Ba nhánh tách riêng từ `main`, mỗi nhánh đã chạy `build.ps1` thành công (test logic/UI). **Chưa kiểm thử native trong AutoCAD**; người dùng tự thử trên bản sao DWG trước khi hợp nhất.

- `fix/isolate-context`: bối cảnh MRTVIEW thiếu nét. Nguyên nhân chính suy ra từ code: khi MRTISO mở phiên mới mà bản vẽ còn đối tượng ẩn từ HIDE/ISOLATE trước (hoặc file mở lại với OBJECTISOLATIONMODE=1, hoặc phiên cũ bị mất), chúng bị ghi là "ẩn trước phiên" và MRTVIEW luôn ẩn lại. Nay MRTISO hỏi [Hien/Giu] (mặc định Hien). Thêm: REGENALL cho mọi viewport; mở khóa Layer tạm trong cùng transaction rồi khóa lại; luôn trả Layer/hiển thị dù bước ẩn lại báo lỗi; không ghi các đối tượng chưa khôi phục được thành "ẩn trước". Lệnh mới `MRTISODIAG` để chẩn đoán.
- `fix/legend-sync`: bảng không tự cập nhật. Tự đồng bộ sau MRTTOOLS/MRTR0 (sửa nhãn), MRTPIPE (gán), sửa hàng loạt; đồng bộ cả màu và lineweight; theo ống nguồn bằng handle nên đổi tên nhãn không còn bị bỏ qua (cập nhật nhãn dòng và diễn giải nếu diễn giải đúng bằng nhãn cũ); MRTLEGEDIT dựng lại mẫu từ ống nguồn; dọn block `Mr.T_LG_` thừa. Hệ số tỷ lệ/độ rộng mẫu vẫn riêng như trước.
- `ui/ribbon-layout`: 6 panel theo thứ tự công việc (Đường ống · Chọn & Cô lập · Hình học & Block · Chữ & Font · Bảng & Phòng · Tiện ích), nút phụ xếp chồng nhỏ. `tests/RibbonPlacement2005.cs` cập nhật và chạy PASS. `tests/RibbonTests.cs` đã lỗi thời từ trước (đếm 10 nút, 2 panel), chưa sửa.
- Thứ tự hợp nhất gợi ý: isolate → legend → ribbon. Riêng `PackageContents.xml`/`PackagingTests.cs` chỉ đổi ở nhánh isolate.
- Bản cài thử **2.0.1.7** dựng từ nhánh `test/combined-2026-11-17` (gộp cả ba nhánh; ProductCode mới để nâng cấp đè 2.0.1.6). `packaging/test-package.ps1`: 24 PASS (chỉ kiểm tra gói, chưa cài thật). Setup SHA-256: `3508FE836C2A1B610F0C8496C9D0D4CE70C3B70C8A5039529A33BCAD3DBB8EC6`.
- Người dùng đã cài 2.0.1.7 và báo ổn; PR #1 mở vào `main`.

## Bản thử 2.0.1.8 — nhánh `feat/density-r-z0` (từ `test/combined-2026-11-17`)

- **Mật độ chữ ống**: thêm icon `mrtdensity` (và `mrtz0pl`) trong `packaging/Assets.cs`. Sau mỗi lần áp dụng, xóa các nét `<stem>_SP_n` cũ cùng họ mà `Database.Purge` xác nhận không còn tham chiếu (trước đây tích tụ mãi).
- **MRTALIGNR** còn sót đoạn — nguyên nhân tìm thấy trong code: (1) quét "toàn bộ" chỉ lấy Layer tên `Mr.T…`, bỏ qua ống trên Layer khác/gán nét riêng → nay lấy mọi LINE/polyline dùng nét có chữ; (2) polyline có đỉnh trùng (đoạn dài 0) bị bỏ cả tuyến → đoạn 0 nhận hướng đoạn kề; (3) polyline có cung bị bỏ → dùng hướng dây cung; (4) ngưỡng "đứng" 0,25° quá hẹp nên đường lệch 1° đọc dưới→trên cạnh đường đứng đọc trên→dưới → ngưỡng 2° (`NativeDirectionRules.VerticalToleranceDegrees`, dùng chung với `DirectionRules`); (5) LINE hai đầu khác Z / polyline lật pháp tuyến vẫn bỏ qua nhưng lý do chỉ sang MRTZ0PL; tóm tắt gom lý do theo nhóm.
- **MRTZ0PL** (mới, `FlattenService`): chiếu LINE/LWPOLYLINE/polyline 2D cũ/polyline 3D xuống Z=0 (giữ X,Y), sửa polyline lật pháp tuyến (đổi dấu bulge); tùy chọn đổi LINE → LWPOLYLINE bằng `HandOverTo` (giữ handle, thuộc tính, XData). Polyline 2D/3D thành LWPOLYLINE mới (handle mới). Bỏ qua: trong block, Layer khóa/Xref, LINE đứng theo Z, polyline spline/fit, đối tượng có liên kết. Chưa kiểm thử native.
- Linetype **U=0**: công cụ đã có tùy chọn tạo "U=0 (thử nghiệm)" trong Quản lý đường nét; API .NET 2024 không đặt được cờ upright nên cần đường LIN/entmod nếu muốn chuyển hàng loạt. Chưa làm lệnh chuyển R→U.
- Bộ cài 2.0.1.8: ProductCode mới; `test-package.ps1` 24 PASS, gồm xác nhận nhận diện và thay bản 2.0.1.7 đang cài. Setup SHA-256: `5A2959440A74962CBBA9F369961CEFF50A9ED7B01231713C61396B52B2226641`.
- Người dùng đã cài 2.0.1.8 (bản có MRTZ0PL) trước đợt giao diện dưới đây.

## Bản thử 2.0.1.9 — cùng nhánh `feat/density-r-z0`

- **Giao diện kiểu Autodesk** (`src/UI/UiTheme.cs`): nền xám trung tính, nút chính xanh Autodesk #0696D7, nút phụ viền xám, bảng kẻ ngang, tiêu đề cột xám nhạt, dòng chọn xanh nhạt, chữ Segoe UI 11 pt (trước 10,5). `UiTheme.Attach(this)` ở đầu constructor của cả 25 Form (UI + Plugin); chỉ đổi màu/viền/font, không đổi kích thước nút để giữ các test "fits at minimum size". Đã nới: hàng tìm kiếm/ghi chú của Quản lý đường nét, cỡ tối thiểu Bảng ký hiệu 1240×800, cột "Loại". Ribbon trong form đổi sang bảng màu trung tính.
- **Đổi font (MRTFONT/MRTFONTALL)** trong `MrT_FontSafe2006.lsp`: chỉ còn hỏi font đích (nhớ lựa chọn lần trước qua `setenv MrT_FontTarget`, thêm ArialNarrow); Text Style luôn là `Mr.T_<FONT>` chuẩn (cao 0, rộng 1, nghiêng 0), tự tạo, không hỏi tên style; bỏ câu hỏi ABC/FontChiDinh — từng đối tượng tự nhận diện (ABC → chuyển mã, Unicode → chỉ đổi style, mã chồng lấn/legacy → để lại báo "?"); xác nhận mặc định Yes. Chưa kiểm thử trong AutoCAD.
- Bộ cài 2.0.1.9: ProductCode mới; `test-package.ps1` 24 PASS, xác nhận nhận diện bản 2.0.1.8 đang cài. Setup SHA-256: `3BF591FDA192EE153E1675CE75BE38D646DD5967B701A42714B4728DEF21FE05`.
- Chưa làm: gọn các form (Bảng ký hiệu, Quản lý đường nét) theo một khung chung.

## Bản thử 2.0.1.10 — cùng nhánh `feat/density-r-z0` (18/11/2026)

- **Sửa lỗi MRTFONT `bad argument type: lselsetp T`** (người dùng báo trên 2.0.1.9): `(setq ss (or *MRTFONT-Preselection* (ssget ...)))` trả về `T` thay vì bộ chọn. Đổi sang `cond` ở cả `MrT_FontSafe2006.lsp` và `MrT_ABC2Font.lsp`. Tái hiện và xác nhận đã sửa trong AutoCAD 2024 thật (điều khiển qua COM, nạp LSP từ thư mục cài Program Files nên không đổi SECURELOAD).
- **Nhận diện bảng mã chặt hơn**, kiểm trên bản sao mặt bằng khí y tế tầng 4: trước 692 chuyển / 441 "?", sau 1133 chuyển / 0 "?" / 0 lỗi:
  - Style ABC (.VnAvantH...) được tin; chỉ "?" khi chuỗi có ký tự > 255 (TCVN3 không sinh được).
  - Style Unicode: chỉ coi là TCVN3 khi có mã TCVN3 không thuộc chữ Việt Latin-1 (ß « æ Ö §...; Ø × µ ½ tính nửa). "Khí Nén", "PHÒNG 602", "PHÒNG MỔ" không còn bị giữ lại.
  - Style SHX: chữ thuần ASCII được đổi style; SHX có ký tự ngoài ASCII và font ký hiệu (symbol, gdt, wingding, sym*...) vẫn "?".
  - Style Mr.T chỉ có typeface (không có tệp font) được nhận là TrueType.
  - MTEXT có `\fArial...` (Unicode) đổi theo font đích cả khi chạy toàn bản vẽ; \f vào font ký hiệu thì giữ.
- **Giao diện** (`UiHeader.cs` mới, `UiTheme.cs`): dải tiêu đề tối trên mọi hộp thoại (badge icon xanh theo loại công cụ, nhãn MR.T CAD TOOLSET, tiêu đề lớn, phụ đề/phiên bản bên phải, vạch xanh Autodesk); tiêu đề lớn trong form được chuyển lên dải (hàng cũ thu về 0), control đặt tuyệt đối được dời xuống. Windows 11: viền cửa sổ xanh Autodesk, thanh tiêu đề cùng màu dải. Nhóm ribbon trong form có vạch xanh trên, chú thích nền xanh nhạt. Thêm icon: layers, legend, density, room, keyboard, block. Tab vẫn để hệ thống vẽ (owner-draw làm hỏng test cỡ tối thiểu).
- Bộ cài 2.0.1.10: ProductCode `{57A9D13B-F5B9-4693-96F0-666AE36C86E8}`; `test-package.ps1` 24 PASS, nhận diện bản 2.0.1.9 đang cài. Setup SHA-256: `E739D16FF937D080383C0470496E697E272DC4B9CF35F0D75D5B5644862D42D8`.
- Ghi chú: các lần thử đã ghi `MrT_FontTarget` = Bahnschrift (setenv) trên máy này. Chữ kích thước (trong block *D) không được đổi; nếu cần chuẩn hóa thì phải sửa Dimension Style.

## Bản thử 2.0.1.11 — cùng nhánh `feat/density-r-z0` (18/11/2026)

- **Kiểu chữ** trong MRTFONT/MRTFONTALL (`MrT_FontSafe2006.lsp`): câu hỏi thứ hai `[GiuNguyen/Thuong/Hoa/Proper] <GiuNguyen>`. Chỉ đổi phần chữ nhìn thấy; giữ nguyên mã MTEXT (\P, \H..;, \f..;, {}), %%C/%%D/%%nnn, field %<..>%, \U+XXXX; nội dung xếp chồng \S..; được đổi. Thuong/Proper giữ nguyên từ có chữ số (MA4, CO2, D110); Hoa thì viết hoa hết. Bảng hoa/thường tiếng Việt lấy từ `*A2FLT-UpperMap*` (67 cặp) + strcase cho ASCII.
- **MRTFONT với block**: nếu tập chọn có INSERT thì hỏi `Sửa cả chữ/ATTDEF bên trong block đã chọn [Co/Khong] <Co>`; Co → quét định nghĩa block (bản *U và EffectiveName của block động), block lồng (mỗi định nghĩa một lần), thuộc tính của block lồng. Bỏ qua Xref/layout.
- **ATTDEF**: prompt được chuyển mã khi style là ABC hoặc prompt có ký tự chỉ có trong TCVN3; chỉ ghi prompt sau khi ghi chữ/style thành công.
- **Sửa lỗi thuộc tính nhiều dòng**: AutoCAD 2024 bỏ qua `MTextAttributeContent` trên thuộc tính nhiều dòng có sẵn (khung tên tầng 4 báo 6 lỗi); `A2FLT:CommitText` nay ghi lại bằng `TextString` khi đọc lại không khớp. Bản sao tầng 4 với Proper: 1133 chuyển / 0 lỗi / 0 "?".
- Bộ cài 2.0.1.11: ProductCode `{15DB0B8D-4BC5-4E71-B911-4680D1160C5D}`; `test-package.ps1` 24 PASS, nhận diện 2.0.1.10 đang cài. Setup SHA-256: `5418DF679472BE0AF99BFFB96CDADCF716F9BFA3B783C55CF1393B0C48198925`.
- **Sự cố khi kiểm thử**: script COM cũ dùng `GetActiveObject` đã gắn vào AutoCAD người dùng đang mở bản vẽ gốc tầng 4 và chạy thử trên đó (chưa lưu; tệp trên đĩa không đổi, người dùng đóng không lưu). Từ nay chỉ dùng trình điều khiển tự khởi động phiên AutoCAD riêng, xác nhận PID mới và chỉ mở bản vẽ trong thư mục nháp; không bao giờ gắn vào phiên AutoCAD sẵn có.

## Phối hợp hai phiên

- Cả hai bắt đầu từ cùng commit nền, làm trên nhánh khác nhau, ghi rõ phạm vi công việc.
- Không đưa DWG thật, Excel công việc, token, hồ sơ người dùng hay log môi trường vào kho nếu chưa được yêu cầu và xem xét.
- Trước hợp nhất: đọc cả hai nhánh, kiểm tra xung đột và tương tác giữa thay đổi, chạy build/kiểm thử phù hợp, cập nhật bàn giao và tạo setup mới sau khi hợp nhất.

## Theo dõi chat và phục hồi

- Xem [hướng dẫn theo dõi context và phục hồi](THEO_DOI_VA_PHUC_HOI_CONTEXT_MRT.md) khi tiếp tục từ chat hoặc máy khác. Tài liệu này ghi rõ giới hạn của bản sao nhật ký và cách kiểm tra số lần nén theo yêu cầu.
