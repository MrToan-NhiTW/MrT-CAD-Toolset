# Theo dõi cuộc trò chuyện dài và phục hồi dự án Mr.T

Cập nhật: 13/11/2026. Mục đích: tiếp tục quan sát cuộc trò chuyện Codex gốc sau nhiều lần nén, đồng thời giữ đủ thông tin để tiếp tục dự án nếu không mở được chat.

## Điểm xuất phát của phép thử

- Cuộc trò chuyện Codex gốc: **Sửa bảng chú giải AutoCAD**, đang được dùng tại máy cơ quan. Chat gốc không tự đồng bộ qua GitHub.
- Khi kiểm tra nhật ký ngày 13/11/2026 đã quan sát **8 sự kiện nén**. Nhật ký ghi `model_context_window` 475.000 token; các lần nén đã quan sát gần 447.000–448.000 token đầu vào. Đây là quan sát, không phải cam kết về ngưỡng hay số lần nén tối đa.
- Sau lần nén mới nhất đã quan sát, lúc 17:20 ngày 12/11/2026 (giờ Việt Nam), lần đo hợp lệ tiếp theo là 49.613 token. Sau đó số token tiếp tục thay đổi theo công việc.
- Nén không bảo đảm giữ toàn bộ chi tiết cũ trong context hiện hành. Bản ghi chat được lưu và context mô hình đang dùng là hai thứ khác nhau.
- Không có dịch vụ theo dõi chạy nền: khi người dùng yêu cầu hoặc trước một mốc bàn giao quan trọng, đọc nhật ký hiện có, đếm sự kiện `compacted`, ghi thời gian, số đo gần nhất và khả năng tiếp tục. Không coi số 0 ở lần đếm sau nén là số context thật; kiểm tra lần đo đầu vào khác 0 tiếp theo. Tránh công bố nội dung nhật ký cá nhân.
- Mỗi lần nén ghi lại những dấu hiệu suy giảm: mâu thuẫn với quyết định đã lưu, bỏ sót yêu cầu, không tra cứu được lịch sử, lỗi mở/tiếp tục chat. Chỉ xác nhận khi có bằng chứng; không tự khẳng định do nén.

## Nếu chat không còn mở hoặc cần đổi máy

1. Mở kho riêng `MrToan-NhiTW/MrT-CAD-Toolset` trên GitHub; lấy nhánh/commit mới nhất và xem xét thay đổi ở hai máy trước khi hợp nhất.
2. Đọc `README.md` và `HANDOFF.md`, đối chiếu mã nguồn, kết quả kiểm thử và phiên bản setup; không cho rằng mọi chức năng đã chạy thành công trên DWG thực tế.
3. Tiếp tục trong chat mới: “Đọc README.md, HANDOFF.md, THEO_DOI_VA_PHUC_HOI_CONTEXT_MRT.md và kiểm tra trạng thái kho; tiếp tục công việc [ghi rõ việc cần làm]. Nếu có lịch sử cũ truy cập được thì tra các quyết định liên quan.”
4. Với mốc nền 2.0.1.6: bản thử MRTROOMS chỉ đọc `.xlsx`/`.xlsm`; chưa có Excel thực tế, chưa triển khai tách phòng và chưa xác nhận kiểm thử AutoCAD native. Các chi tiết và vấn đề khác nằm trong `HANDOFF.md`.
5. Trước mỗi mốc quan trọng, bổ sung vào `HANDOFF.md`: yêu cầu, quyết định, thay đổi, kiểm thử đã làm/chưa làm, việc còn mở, commit và vị trí bộ cài. Không đưa bản vẽ thật, Excel công việc, thông tin xác thực hoặc log cá nhân lên GitHub khi chưa xem xét.

## Bản sao hội thoại riêng tư

Ngày 13/11/2026 đã tạo bản chụp ZIP nhật ký Codex trên máy cơ quan tại thư mục `outputs` của workspace. Đây là snapshot tại thời điểm tạo, không tự cập nhật; có thể chứa nội dung cá nhân, đường dẫn, các lệnh và kết quả công cụ. Không tải bản thô này lên kho GitHub. Bản sao không bảo đảm chứa đủ ảnh, file đính kèm hoặc bản vẽ và không bảo đảm nhập ngược nguyên trạng vào Codex. Muốn dự phòng mất máy, người dùng cần lưu nó ở nơi riêng tư khác và kiểm tra quyền truy cập.

Nếu chỉ cần phục hồi phát triển từ máy nhà, ưu tiên GitHub + bản bàn giao. Chat mới có thể đọc bản sao nhật ký nếu được cung cấp, theo từng phần liên quan; không yêu cầu nạp toàn bộ vào một context.
