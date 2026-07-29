namespace Sip.be.External.Documents.Dtos
{
    /// <summary>
    /// Kết quả kiểm tra dấu thời gian tin cậy của 1 file so với TSA Ban Cơ yếu Chính phủ.
    /// Xem docs/verify-signature-timestamp.md.
    /// </summary>
    public class TimestampVerifyDto
    {
        /// <summary>Kết luận cuối cùng.</summary>
        public TimestampVerdict Verdict { get; set; }

        /// <summary>File có token dấu thời gian RFC 3161 hay không.</summary>
        public bool HasTimestamp { get; set; }

        /// <summary>Tên TSA (CN của cert máy chủ cấp dấu thời gian).</summary>
        public string? TimestampAuthority { get; set; }

        /// <summary>CN của cert người ký (của chữ ký quyết định verdict cấp file).</summary>
        public string? SignedBy { get; set; }

        /// <summary>
        /// Thời gian tin cậy = genTime thô của TSA (UTC chuẩn), "dd/MM/yyyy HH:mm:ss".
        /// Là giờ của chữ ký MỚI NHẤT (khối phủ xa nhất) — file nhiều chữ ký thì đây là cái người dùng vừa
        /// tạo ra, không phải chữ ký cũ có sẵn trong file. Giờ của từng chữ ký nằm ở <see cref="Signatures"/>.
        /// </summary>
        public string? TrustedTime { get; set; }

        /// <summary>Giờ ký tự khai (signingTime) của chữ ký MỚI NHẤT, "dd/MM/yyyy HH:mm:ss".</summary>
        public string? SigningTime { get; set; }

        /// <summary>Mô tả lý do khi không hợp lệ.</summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Kết luận + giờ của TỪNG chữ ký trong file, xếp theo THỨ TỰ KÝ (cũ nhất trước, mới nhất sau cùng).
        /// Verdict cấp file ở trên = verdict XẤU NHẤT trong danh sách này (1 chữ ký hỏng là hỏng cả file);
        /// với file nhiều chữ ký, verdict và một cặp giờ cấp file không đủ để chẩn đoán nên phải xem ở đây.
        /// </summary>
        public List<SignatureDetailDto> Signatures { get; set; } = new();

        /// <summary>
        /// Số chữ ký ĐẠT (ValidTrusted) trong file. Khác <see cref="SignatureBlockCount"/> (tổng số khối tìm
        /// thấy, kể cả khối hỏng): file 2 chữ ký mà 1 cái không tin cậy thì ở đây là 1.
        /// </summary>
        public int ValidSignatureCount { get; set; }

        /// <summary>
        /// Số khối /ByteRange đọc được như 1 chữ ký (kể cả khối HỎNG không giải mã được CMS).
        /// Dùng để biết "file này có chữ ký hay chưa" — KHÔNG dùng Signatures.Count: khối hỏng không sinh
        /// SignatureDetailDto per-signer nên Count có thể nhỏ hơn số chữ ký thực tế trong file.
        /// </summary>
        public int SignatureBlockCount { get; set; }

        /// <summary>
        /// Số khối /ByteRange bị BỎ QUA vì không phải chữ ký (chuỗi "/ByteRange" trong content stream).
        /// Báo ra ngoài để "ta đã bỏ qua thứ trông giống chữ ký" không bao giờ vô hình với caller.
        /// </summary>
        public int SkippedBlocks { get; set; }

        /// <summary>
        /// Verifier không đọc nổi file (exception cấp file) -> kết luận KHÔNG đáng tin, fail-closed.
        /// Khi true, tuyệt đối không suy ra "file chưa ký" từ việc Signatures rỗng.
        /// </summary>
        public bool ReadFailed { get; set; }

        /// <summary>File có dấu thời gian Ban Cơ yếu thật và khớp giờ.</summary>
        public bool IsValid => Verdict == TimestampVerdict.ValidTrusted;

        /// <summary>Verdict này có CHẶN đóng gói không (PDF lỗi = chặn; hợp lệ & non-PDF = không chặn).</summary>
        public bool Blocks => Verdict != TimestampVerdict.ValidTrusted && Verdict != TimestampVerdict.NotApplicable;
    }
}
