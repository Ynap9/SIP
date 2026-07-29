namespace Sip.be.External.Documents.Dtos
{
    /// <summary>
    /// Tham số ký 1 file PDF mà <c>IPdfSigner</c> cần. Là hợp đồng RIÊNG của External: tầng Application nhận
    /// request từ HTTP rồi ánh xạ sang đây, nên đổi khuôn API không kéo theo đổi code ký.
    /// </summary>
    public sealed class PdfSignOptionsDto
    {
        /// <summary>Thumbprint của chứng thư số dùng để ký (khóa đã được cache từ bước kiểm tra token).</summary>
        public string Thumbprint { get; set; } = string.Empty;

        /// <summary>Lý do ký ghi vào signature dictionary. Rỗng -> dùng mặc định của SigningConstants.</summary>
        public string? Reason { get; set; }

        /// <summary>Nơi ký ghi vào signature dictionary. Rỗng -> dùng mặc định của SigningConstants.</summary>
        public string? Location { get; set; }

        /// <summary>
        /// Các vị trí khối chữ ký do người dùng chọn, tính theo TỈ LỆ trang nên dùng chung được cho cả lô file
        /// khác khổ giấy. NULL/rỗng = vị trí mặc định: góc TRÊN-PHẢI trang 1, khổ 170x30pt của file production
        /// đã co giãn theo khổ trang thật.
        ///
        /// KHÔNG giới hạn số vị trí: người dùng kéo thả bao nhiêu khối thì đóng bấy nhiêu khối, mỗi khối một
        /// widget đặt đúng trang của nó. Vẫn CHỈ MỘT chữ ký cho cả file (một field nhiều widget) nên số lần
        /// gọi TSA không đổi.
        /// </summary>
        public IReadOnlyList<PdfSignPlacementDto>? Placements { get; set; }

        /// <summary>
        /// Người dùng CHẤP NHẬN chữ ký đè lên nội dung trang hay không.
        /// CHƯA CÓ TÁC DỤNG: bộ dò vùng có nội dung của trang chạy chậm và độ chính xác thấp nên tạm gác lại,
        /// hiện chữ ký luôn được đặt đúng ô đã tính. Cờ này đã nằm sẵn trong hợp đồng để khi có bộ dò thì
        /// <c>ISignaturePlacementResolver</c> chỉ việc dùng thêm, FE không phải đổi payload.
        /// </summary>
        public bool AllowOverlapContent { get; set; }
    }
}
