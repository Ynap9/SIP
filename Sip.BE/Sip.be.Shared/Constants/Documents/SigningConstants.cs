namespace Sip.be.Shared.Constants.Documents
{
    /// <summary>
    /// Hằng số cho luồng KÝ SỐ (lấy chứng thư số + ký PDF). Xem docs/plans/pdf-signing.plan.md.
    /// Khác <see cref="SignatureConstants"/>: file này phục vụ việc TẠO chữ ký, file kia phục vụ việc KIỂM chữ ký.
    /// </summary>
    public static class SigningConstants
    {
        // ===== TSA (RFC 3161) =====
        // TSA Ban Cơ yếu Chính phủ. Đo thực: chỉ trả lời giao thức TSP (GET 403 / POST 400).
        public const string TsaUrl = "http://tsa.ca.gov.vn/";
        public const string TsaContentType = "application/timestamp-query";
        public const string TsaReplyContentType = "application/timestamp-reply";

        // Vòng gọi TSA đo thực mất 2s-131s -> timeout phải rộng, nếu không file hợp lệ bị đánh trượt oan.
        public const int TsaTimeoutSeconds = 180;

        // ===== OID =====
        // ESS signingCertificateV2 — .NET KHÔNG tự thêm, phải tự dựng bằng AsnWriter; thiếu nó thì file không
        // conform CAdES và Adobe/Foxit có thể từ chối dù verifier của SIPPACK vẫn nhận.
        public const string OidSigningCertificateV2 = "1.2.840.113549.1.9.16.2.47";

        // ===== PDF signature dictionary =====
        // PAdES bắt buộc SubFilter này (đo thực trên gói mẫu: file thật ghi đúng giá trị này).
        public const string PdfSubFilter = "ETSI.CAdES.detached";
        public const string DefaultReason = "Ký số tài liệu";
        public const string DefaultLocation = "Việt Nam";

        // ===== Hình thức chữ ký hiển thị =====
        // Số đo TRÍCH TỪ FILE THẬT (docs/pdf-signature-appearance.md), không phải tự chọn cho đẹp.
        // Khối 170x30pt đặt ở góc TRÊN-PHẢI trang; 2 dòng chữ: dòng 1 = CN người ký, dòng 2 = giờ ký ISO 8601.
        // Hai dòng được CANH GIỮA ô (không phải dóng trái x=2pt như file mẫu) nên không còn hằng số tọa độ
        // từng dòng: chỗ đặt chữ suy ra từ chính khổ ô mà người dùng kéo thả.
        public const double AppearanceWidth = 170;
        public const double AppearanceHeight = 30;
        public const string AppearanceFontFamily = "Times New Roman";
        public const double AppearanceFontSize = 10;
        // Giờ ký hiển thị trong khối chữ ký: ISO 8601 theo giờ VN, KHÔNG kèm phần bù múi giờ.
        // File mẫu ghi thêm "+07:00" ("2026-04-16T05:25:06.2686779+07:00") nhưng bỏ đi theo yêu cầu nghiệp vụ:
        // mọi giờ trong khối đều là giờ VN nên phần bù chỉ làm khối dài thêm.
        public const string AppearanceTimeFormat = "yyyy-MM-ddTHH:mm:ss.fffffff";

        // ===== Co giãn khối chữ ký theo khổ trang =====
        // Khổ trang của file production mà bộ số 170x30pt ở trên được đo ra (A4 dọc). Khối chữ ký được nhân
        // theo tỉ lệ khổ trang thật / khổ tham chiếu này, nên trang A3 không bị chữ ký teo bằng con tem còn
        // trang nhỏ hơn A4 không bị tràn. Không có bước co giãn này thì một vị trí chọn 1 lần không thể
        // dùng chung cho cả thư mục có nhiều khổ giấy.
        public const double AppearanceReferencePageWidth = 595;
        public const double AppearanceReferencePageHeight = 842;

        // Sàn kích cỡ khi người dùng TỰ chọn vị trí: khối không được nhỏ hơn ngần này lần khổ chuẩn của trang.
        // Vị trí người dùng chọn được tôn trọng nguyên vẹn; chỉ hai thứ bị sửa là khối TRÀN ra ngoài trang
        // (kẹp lại) và khối NHỎ tới mức không đọc nổi chữ (kéo lên sàn này). Khớp SIGN_BLOCK_MIN_SCALE của FE.
        public const double AppearanceMinScale = 0.5;

        // Lề chừa với mép trang khi TỰ đặt chữ ký (vị trí mặc định) — dính sát mép trông như lỗi in.
        // Cũng co giãn theo khổ trang như khối chữ ký.
        public const double AppearancePageMargin = 8;

        // ===== Tên file đã ký =====
        // File đã ký ghi CẠNH file gốc trong CHÍNH thư mục nguồn (KHÔNG tạo thư mục mới), tên =
        // "{tên gốc không đuôi}_daky_{yyyyMMdd_HHmmss}{đuôi gốc}". Giữ nguyên file gốc: file đã ký là bản MỚI
        // nên ký lỗi thì bản gốc vẫn nguyên và chạy lại được. Timestamp lấy MỘT LẦN đầu lô, dùng chung cả lô.
        public const string SignedFileSuffix = "_daky_";
        public const string SignedFileTimeFormat = "yyyyMMdd_HHmmss";

        // ===== Song song =====
        // Nghẽn nằm ở MẠNG (TSA) chứ không ở CPU -> ProcessorCount đơn thuần là con số sai.
        // Chặn dưới 2 để máy 1 nhân vẫn chạy; chặn trên 8 để không mở quá nhiều kết nối.
        public const int MinDegreeOfParallelism = 2;
        public const int MaxDegreeOfParallelism = 8;
        // Trần riêng cho tầng gọi TSA — fan-out không giới hạn là dội request vào TSA của cơ quan nhà nước.
        public const int MaxTsaConcurrency = 4;

        // Số byte giả dùng để test-sign khi kiểm tra token trước khi ký cả lô.
        public const int PreflightTestDataSize = 32;

        // Nhịp job giám sát token trong lúc ký. Đủ nhanh để dừng ngay khi người dùng rút token,
        // đủ thưa để không quét cert store liên tục.
        public const int TokenMonitorIntervalSeconds = 2;

        /// <summary>
        /// Chuỗi nhận diện provider LƯU KHÓA BẰNG PHẦN CỨNG (USB token/smartcard) trong tên CSP/KSP của private key.
        /// Cert trong store mà private key nằm ở provider phần cứng -> nguồn UsbToken; còn lại -> Local/Server.
        /// So khớp KHÔNG phân biệt hoa thường, dạng "chứa" — tên provider do middleware đặt nên không cố định
        /// (bit4id xPKI / VGCA PKI Manager, SafeNet, Gemalto...), liệt kê cứng từng tên đầy đủ là sẽ sót.
        /// </summary>
        public static readonly string[] HardwareKeyProviderMarkers =
        {
            "smart card",   // Microsoft Smart Card Key Storage Provider (đường chung của mọi minidriver)
            "bit4id",       // bit4id xPKI — middleware token Ban Cơ yếu (xem docs/token-signing-spike.md §5)
            "vgca",         // VGCA PKI Manager
            "token",
            "etoken",       // SafeNet eToken
            "safenet",
            "gemalto",
            "thales",
            "epass",        // Feitian ePass
            "feitian",
        };

        /// <summary>
        /// Nhà cung cấp CSP (CAPI đời cũ) mặc định của Windows cho khóa PHẦN MỀM — dùng để loại trừ.
        /// Cert dùng các provider này chắc chắn KHÔNG phải token.
        /// </summary>
        public static readonly string[] SoftwareKeyProviderMarkers =
        {
            "microsoft software key storage provider",
            "microsoft enhanced cryptographic provider",
            "microsoft base cryptographic provider",
            "microsoft strong cryptographic provider",
            "microsoft enhanced rsa and aes cryptographic provider",
        };
    }
}
