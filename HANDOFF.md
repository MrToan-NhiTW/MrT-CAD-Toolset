# Bàn giao phát triển — 2.0.1.6

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
- Chưa làm: gọn các form (Bảng ký hiệu, Quản lý đường nét) theo một khung chung.

## Phối hợp hai phiên

- Cả hai bắt đầu từ cùng commit nền, làm trên nhánh khác nhau, ghi rõ phạm vi công việc.
- Không đưa DWG thật, Excel công việc, token, hồ sơ người dùng hay log môi trường vào kho nếu chưa được yêu cầu và xem xét.
- Trước hợp nhất: đọc cả hai nhánh, kiểm tra xung đột và tương tác giữa thay đổi, chạy build/kiểm thử phù hợp, cập nhật bàn giao và tạo setup mới sau khi hợp nhất.

## Theo dõi chat và phục hồi

- Xem [hướng dẫn theo dõi context và phục hồi](THEO_DOI_VA_PHUC_HOI_CONTEXT_MRT.md) khi tiếp tục từ chat hoặc máy khác. Tài liệu này ghi rõ giới hạn của bản sao nhật ký và cách kiểm tra số lần nén theo yêu cầu.
