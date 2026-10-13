namespace Sip.be.Application.Document.Dtos.SignTemplate
{
    /// <summary>
    /// Vị trí file PDF mẫu đi kèm app, dùng để người dùng đặt thử vị trí chữ ký/dấu/chữ ký tươi khi chưa có
    /// hồ sơ thật.
    ///
    /// Chỉ trả về ĐƯỜNG DẪN chứ không trả bytes: FE lấy nội dung bằng endpoint sẵn có
    /// <c>GET api/core/document/sign/xem-file?path=...</c>. Trong repo này <c>sign/xem-file</c> là endpoint DUY
    /// NHẤT không bọc ApiResponse - đẻ thêm ngoại lệ thứ hai chỉ để phục vụ một file tĩnh là không đáng.
    /// </summary>
    public class SampleFileDto
    {
        /// <summary>Đường dẫn tuyệt đối tới file PDF mẫu trên máy đang chạy app.</summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>Tên file để hiển thị.</summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>File mẫu có thực sự tồn tại không - thiếu file thì FE ẩn phần xem trước thay vì báo lỗi đỏ.</summary>
        public bool Exists { get; set; }
    }
}
