namespace Sip.be.Shared.Constants.Documents
{
    /// <summary>
    /// Hằng số cho kiểm tra dấu thời gian tin cậy (RFC 3161) của chữ ký số PDF do TSA Ban Cơ yếu Chính phủ cấp.
    /// Xem docs/verify-signature-timestamp.md.
    /// </summary>
    public static class SignatureConstants
    {
        // Trust anchor: RootCA chuyên dùng Chính phủ G2 — SHA-256 (fingerprint DER) là NGUỒN CHÂN LÝ.
        // File cert (rootcag2.crt) chỉ là "material", đối chiếu theo pin này khi nạp (tráo file -> fail).
        public const string RootG2Sha256 = "F04BA4B459A7C9A1B971AD9B1CB833E695A80C79AEA63B174B66A70EDFE2FDB8";

        // File cert Root G2 đặt cạnh app (Sip.be.Api/rootcag2.crt -> copy ra output), gitignore, cấp out-of-band từ
        // http://ca.gov.vn/pki/pub/crt/rootcag2.crt (định dạng PEM).
        public const string RootG2FileName = "rootcag2.crt";

        // CA TRUNG GIAN dưới Root G2 (do Root G2 cấp). Ghim theo SHA-256 (fingerprint DER) như Root G2;
        // nạp vào ExtraStore khi dựng chain để cert leaf (TSA/người ký) vẫn về được Root G2 khi token/PDF
        // KHÔNG nhúng sẵn CA trung gian. Cấp out-of-band, gitignore. Xem docs/ca-trust-certificates.md.
        // key = tên file cạnh app; value = SHA-256 pin (DER).
        public static readonly IReadOnlyDictionary<string, string> IntermediateCaPins =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // CA phục vụ các cơ quan Đảng G2
                ["dcscag2.crt"] = "E4B402E3CB4CACC0A20D21FA49BCE76BEB4EB47BA7FFB7A8F71C554DBE561A81",
                // CA phục vụ các cơ quan Nhà nước G2
                ["cpg2.crt"] = "D8CAB7A4AEF92E6BB2509D7C61CEE7B44F3731B2D21074963530277B366FAE4B",
            };

        // Nhánh G1 — dùng cho chain của NGƯỜI KÝ (token production hiện là G1).
        // G1/G2 là THẾ HỆ hạ tầng PKI (G1 cấp 2010-2030, G2 cấp 2018-2048), KHÔNG phải phân theo cơ quan/cá nhân.
        // Cấp out-of-band từ ca.gov.vn (lưu ý path /cert/, khác /crt/ của G2), gitignore. Xem docs/ca-trust-certificates.md.
        //
        // Root G1: trust anchor self-signed, http://ca.gov.vn/pki/pub/cert/rootca.crt.
        // Xác thực KHÔNG vòng tròn: chính private key của nó đã ký cp.crt (chain build = true), mà cp.crt lại là
        // issuer của toàn bộ cert người ký trong các PDF thật -> anchor này neo được vào dữ liệu thật.
        public const string RootG1Sha256 = "F7BC89054CEC7F12730D7B8CD3EB7955B163835087E5EB873EDFB584763B3F90";
        public const string RootG1FileName = "rootca.crt";

        // Sub-CA G1: CA trực tiếp cấp cert người ký, http://ca.gov.vn/pki/pub/cert/cp.crt (URL caIssuers trong AIA
        // của cert người ký). Vào ExtraStore khi dựng chain vì CMS trong PDF thật CHỈ nhúng leaf.
        public const string SubCaG1FileName = "cp.crt";
        public const string SubCaG1Sha256 = "75E969C07BA35C29C72BBC1BFB880F20F3891A619B00B77EB02CA6160736A1FE";

        // OID
        public const string OidSignatureTimeStampToken = "1.2.840.113549.1.9.16.2.14"; // id-aa-signatureTimeStampToken
        public const string OidSigningTime = "1.2.840.113549.1.9.5";                    // PKCS#9 signingTime (giờ tự khai)
        public const string OidEkuTimeStamping = "1.3.6.1.5.5.7.3.8";                    // id-kp-timeStamping

        // Quirk của BÊN KÝ, KHÔNG phải của TSA. signingTime là giờ người ký TỰ KHAI; một số công cụ ký ghi GIỜ
        // ĐỊA PHƯƠNG vào trường này nhưng đánh dấu Kind=Utc, làm nó lệch đúng bằng offset múi giờ VN (+7h) so với
        // genTime. genTime của TSA mới là UTC chuẩn -> TUYỆT ĐỐI KHÔNG cộng offset này vào genTime: đó là bù trừ
        // sai phía và còn kéo lệch cả mốc dựng chain. Chỉ dùng để NHẬN DIỆN quirk khi đối chiếu signingTime.
        // Chấp nhận các file như vậy là TƯƠNG THÍCH NGƯỢC: signingTime theo RFC 5652/PKCS#9 là ASN.1 UTCTime,
        // bắt buộc UTC tuyệt đối, nên ghi giờ địa phương vào đó là sai chuẩn — không phải chuẩn thứ hai.
        public const int SignerLocalTimeQuirkHours = 7;

        // Cửa sổ nhận diện quirk trên: coi là "ghi nhầm giờ địa phương" khi lệch nằm quanh +7h trong khoảng này.
        // Rộng 1h là an toàn tuyệt đối vì hai trường hợp cách nhau rất xa: file ghi đúng chuẩn lệch tối đa vài
        // phút, file ghi nhầm lệch ~25200s — không có vùng nào nhập nhằng ở giữa.
        public const int SignerLocalTimeQuirkWindowSeconds = 3600;

        // Biên cho phép signingTime nhỉnh hơn genTime, tính bằng giây.
        // KHÔNG phải ngưỡng chống giả mạo — chỉ để phòng ĐỒNG HỒ TSA CHẠY CHẬM nhẹ và việc signingTime mã hóa
        // theo giây (TSA trả lời trong cùng một giây là chuyện thường). Giữ NHỎ, đừng nới: nới thêm chỉ mở đường
        // cho việc khai giờ ký sau thời điểm đóng dấu mà vẫn lọt.
        public const int TsaClockSkewGraceSeconds = 60;
    }
}
