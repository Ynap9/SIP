using System.Collections.Generic;

namespace Sip.be.Application.Document.Dtos.Check
{
    /// <summary>
    /// Thông tin ký số của 1 FILE đính kèm (đọc từ chữ ký số nhúng trong PDF - chuẩn PAdES/PKCS#7).
    /// Chỉ mang tính hiển thị (đã ký hay chưa + người ký + thời điểm ký), KHÔNG tính là lỗi chặn đóng gói.
    /// </summary>
    public class SignatureInfoDto
    {
        /// <summary>True nếu file có ít nhất 1 chữ ký số.</summary>
        public bool IsSigned { get; set; }

        /// <summary>Số chữ ký số tìm thấy trong file.</summary>
        public int SignatureCount { get; set; }

        /// <summary>Danh sách người ký (1 file có thể ký nhiều lần).</summary>
        public List<SignerInfoDto> Signers { get; set; } = new();

        /// <summary>Lý do không đọc được chữ ký (nếu có) - vd file không phải PDF hợp lệ.</summary>
        public string? Error { get; set; }

        /// <summary>
        /// Kết quả verify DẤU THỜI GIAN tin cậy (RFC 3161) so với TSA Ban Cơ yếu.
        /// Khác với thông tin ký số ở trên: verdict xấu (PDF) CHẶN đóng gói.
        /// </summary>
        public TimestampVerifyDto? Timestamp { get; set; }
    }

    /// <summary>Thông tin 1 lần ký (1 chữ ký số) trong file.</summary>
    public class SignerInfoDto
    {
        /// <summary>Người ký (Signed by) - lấy từ CN của certificate, fallback sang /Name trong dict chữ ký.</summary>
        public string? SignedBy { get; set; }

        /// <summary>Thời điểm ký, định dạng "dd/MM/yyyy HH:mm:ss" (giờ địa phương).</summary>
        public string? SignedAt { get; set; }

        /// <summary>Lý do ký (nếu có) - từ /Reason.</summary>
        public string? Reason { get; set; }

        /// <summary>Địa điểm ký (nếu có) - từ /Location.</summary>
        public string? Location { get; set; }
    }
}
