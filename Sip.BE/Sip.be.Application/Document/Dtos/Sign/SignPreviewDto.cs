namespace Sip.be.Application.Document.Dtos.Sign
{
    /// <summary>
    /// Một bước điều hướng xem trước PDF ở bước cấu hình chữ ký: file đang xem + còn file trước/sau hay không.
    ///
    /// <see cref="Files"/> RỖNG khi <see cref="Index"/> nằm ngoài danh sách (bấm tiếp ở file cuối / bấm lùi ở
    /// file đầu). Đây là trạng thái BÌNH THƯỜNG, không phải lỗi: FE giữ nguyên file đang xem thay vì hiện lỗi.
    /// FE khoá nút bằng <see cref="HasPrev"/>/<see cref="HasNext"/> nên thường không bao giờ đi tới nước này.
    /// </summary>
    public class SignPreviewDto
    {
        /// <summary>Vị trí đã hỏi, đếm từ 0.</summary>
        public int Index { get; set; }

        /// <summary>Tổng số file PDF trong nguồn.</summary>
        public int Total { get; set; }

        /// <summary>Còn file phía trước không (dùng để khoá nút "Trang trước").</summary>
        public bool HasPrev { get; set; }

        /// <summary>Còn file phía sau không (dùng để khoá nút "Trang sau").</summary>
        public bool HasNext { get; set; }

        /// <summary>
        /// File tại <see cref="Index"/>: đúng 1 phần tử, hoặc RỖNG nếu index ngoài danh sách.
        /// Dùng list thay vì 1 object để trạng thái "không có gì để xem" là mảng rỗng, không phải null.
        /// </summary>
        public List<SignFileItemDto> Files { get; set; } = new();
    }
}
