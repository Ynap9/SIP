namespace Sip.be.Application.Document.Dtos.Sign
{
    /// <summary>
    /// Kết quả kiểm vị trí chữ ký của MỘT file, nhúng trong <see cref="SignPositionCheckResultDto"/>.
    /// <see cref="Fitted"/> = false nghĩa là khi ký, khối sẽ bị dịch/phóng cho vừa trang chứ KHÔNG phải file
    /// bị từ chối — bộ ký luôn sửa thay vì báo lỗi.
    /// </summary>
    public class SignPositionFileDto
    {
        /// <summary>Tên file (kèm đuôi).</summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>Đường dẫn tuyệt đối.</summary>
        public string FullPath { get; set; } = string.Empty;

        /// <summary>Số trang thật của file. 0 = không đọc được file.</summary>
        public int PageCount { get; set; }

        /// <summary>Số khối chữ ký sẽ đóng lên file này.</summary>
        public int BlockCount { get; set; }

        /// <summary>Đọc được file để đo hay không.</summary>
        public bool Readable { get; set; }

        /// <summary>Mọi khối đều vừa vặn, không phải chỉnh gì.</summary>
        public bool Fitted { get; set; }

        /// <summary>Mô tả từng chỗ sẽ bị chỉnh (tràn mép, trang không tồn tại, khối quá nhỏ).</summary>
        public List<string> Issues { get; set; } = new();
    }
}
