namespace Sip.be.Application.Document.Dtos.Sign
{
    /// <summary>
    /// Yêu cầu ký số: chọn chứng thư số + đường dẫn nguồn (1 file PDF hoặc 1 thư mục chứa PDF).
    /// </summary>
    public class SignRequestDto
    {
        /// <summary>Thumbprint của chứng thư số dùng để ký (lấy từ GET sign/chung-thu-so).</summary>
        public string Thumbprint { get; set; } = string.Empty;

        /// <summary>Đường dẫn tuyệt đối tới 1 file PDF hoặc 1 thư mục chứa PDF.</summary>
        public string SourcePath { get; set; } = string.Empty;

        /// <summary>Lý do ký, ghi vào signature dictionary. Rỗng -> dùng mặc định của SigningConstants.</summary>
        public string? Reason { get; set; }

        /// <summary>Nơi ký, ghi vào signature dictionary. Rỗng -> dùng mặc định của SigningConstants.</summary>
        public string? Location { get; set; }

        /// <summary>
        /// Người dùng có TỰ CHỌN vị trí đặt khối chữ ký hay không.
        /// false (mặc định) -> tự đặt góc TRÊN-PHẢI trang 1, khổ 170x30pt co giãn theo khổ trang.
        /// true -> BẮT BUỘC gửi kèm <see cref="Positions"/> (ít nhất 1 vị trí).
        /// Cả hai trường hợp: vị trí được tính LẠI cho từng trang của từng file theo khổ trang thật, vì
        /// người dùng chỉ chọn MỘT LẦN cho cả thư mục.
        /// </summary>
        public bool UseCustomPosition { get; set; }

        /// <summary>
        /// Các tọa độ (theo tỉ lệ trang + số trang) do FE đo được; chỉ đọc khi <see cref="UseCustomPosition"/>
        /// = true. Đây là cấu hình CHUNG, áp cho mọi file KHÔNG có mặt trong <see cref="FileOverrides"/>.
        /// Người dùng kéo thả được BAO NHIÊU khối tùy ý, không có trần; nhiều khối vẫn chỉ tạo MỘT chữ ký cho
        /// mỗi file (một field nhiều widget), không nhân số lần gọi TSA.
        /// </summary>
        public List<SignPositionDto>? Positions { get; set; }

        /// <summary>
        /// Các file được cấu hình RIÊNG (người dùng bật "chỉ áp dụng cho file hiện tại" khi đặt chữ ký).
        /// File có mặt ở đây dùng vị trí riêng của nó, file còn lại dùng <see cref="Positions"/>.
        /// Nhờ vậy cả lô — dù mỗi file một kiểu — vẫn chỉ cần MỘT request ký duy nhất.
        /// </summary>
        public List<SignFileConfigDto>? FileOverrides { get; set; }

        /// <summary>
        /// Cho phép chữ ký ĐÈ LÊN NỘI DUNG trang hay không (người dùng tự chịu trách nhiệm che mất chữ).
        /// CHƯA CÓ TÁC DỤNG ở bản này: bộ dò vùng có nội dung chạy chậm và độ chính xác thấp nên tạm gác;
        /// chữ ký hiện luôn đặt đúng ô đã chọn. Trường đã có sẵn trong hợp đồng để FE gửi từ bây giờ và
        /// không phải đổi payload khi bộ dò được bổ sung.
        /// </summary>
        public bool AllowOverlapContent { get; set; }
    }
}
