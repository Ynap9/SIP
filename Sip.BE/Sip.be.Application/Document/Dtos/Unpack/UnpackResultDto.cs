using System.Collections.Generic;

namespace Sip.be.Application.Document.Dtos.Unpack
{
    /// <summary>Kết quả dịch ngược 1 gói SIP về thư mục hồ sơ, trả kèm cây thư mục để FE hiển thị dạng library.</summary>
    public class UnpackResultDto
    {
        /// <summary>Tên hồ sơ (= mã hồ sơ / fileCode) = tên thư mục dựng lại.</summary>
        public string HoSoName { get; set; } = string.Empty;

        /// <summary>Đường dẫn thư mục hồ sơ đã dựng lại (mở trực tiếp được).</summary>
        public string FolderPath { get; set; } = string.Empty;

        /// <summary>Tổng số tài liệu dựng lại (số dòng Van_ban).</summary>
        public int TotalDoc { get; set; }

        /// <summary>Cây thư mục hồ sơ: folder hồ sơ -> metadata/ (Excel) + attachment/ (các file).</summary>
        public FileNodeDto? Tree { get; set; }

        /// <summary>Cảnh báo phát sinh khi dịch ngược (vd gói thiếu data).</summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>Thông điệp tổng kết.</summary>
        public string Message { get; set; } = string.Empty;
    }
}
