using System.Collections.Generic;

namespace Sip.be.Application.Document.Dtos.Pack
{
    /// <summary>
    /// Kết quả đóng gói SIP_tailieu.
    /// </summary>
    public class PackResultDto
    {
        /// <summary>OBJID của gói (= tên thư mục gói), dạng uuid-...</summary>
        public string Objid { get; set; } = string.Empty;

        /// <summary>Mã hồ sơ (fileCode) lấy từ sheet Ho_so.</summary>
        public string FileCode { get; set; } = string.Empty;

        /// <summary>Đường dẫn file ZIP của gói SIP.</summary>
        public string ZipPath { get; set; } = string.Empty;

        /// <summary>Tổng số tài liệu đã đóng gói.</summary>
        public int TotalDoc { get; set; }

        /// <summary>Cảnh báo phát sinh trong quá trình đóng gói (vd thiếu schema).</summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>Thông điệp tổng kết.</summary>
        public string Message { get; set; } = string.Empty;
    }
}
