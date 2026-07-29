namespace Sip.be.External.Documents.Dtos
{
    /// <summary>Kết luận cho MỘT chữ ký trong file (1 file có thể có nhiều chữ ký).</summary>
    public class SignatureDetailDto
    {
        /// <summary>CN của cert người ký.</summary>
        public string? SignedBy { get; set; }

        public TimestampVerdict Verdict { get; set; }

        /// <summary>Mô tả lý do khi chữ ký này không đạt.</summary>
        public string? Reason { get; set; }

        /// <summary>Thời gian tin cậy của chữ ký này (genTime của TSA), "dd/MM/yyyy HH:mm:ss".</summary>
        public string? TrustedTime { get; set; }

        /// <summary>
        /// Giờ ký tự khai (signingTime) của CHÍNH chữ ký này, "dd/MM/yyyy HH:mm:ss".
        /// Có trường này thì file nhiều chữ ký mới hiển thị được giờ của từng cái — trước đây cả file chỉ có
        /// một cặp giờ, mà cặp đó lại là của chữ ký quyết định verdict chứ không phải chữ ký mới nhất.
        /// </summary>
        public string? SigningTime { get; set; }
    }
}
