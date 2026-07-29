namespace Sip.be.External.Documents.Dtos
{
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
