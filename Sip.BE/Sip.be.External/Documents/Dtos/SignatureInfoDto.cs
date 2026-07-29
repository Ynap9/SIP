namespace Sip.be.External.Documents.Dtos
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
}
