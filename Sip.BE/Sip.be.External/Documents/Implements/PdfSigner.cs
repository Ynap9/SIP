using PdfSharp.Fonts;
using PdfSharp.Pdf.Signatures;
using Sip.be.External.Documents.Dtos;
using Sip.be.External.Documents.Interfaces;
using Sip.be.Shared.Constants;
using Sip.be.Shared.Constants.Documents;
using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;

namespace Sip.be.External.Documents.Implements
{
    /// <summary>
    /// Ký PDF theo PAdES. Chia việc làm 3 phần:
    /// - <see cref="PdfIncrementalSigner"/> dựng container bằng cách NỐI BẢN (giữ nguyên byte gốc, ghi thêm
    ///   bản mới vào cuối) — TỰ VIẾT, vì PDFsharp document.Save() ghi lại cả tài liệu và giết chữ ký có sẵn
    ///   (docs/bug/pdfsharp-rewrites-document-destroying-existing-signature.md).
    /// - <see cref="PdfAppearanceBuilder"/> vẫn dùng PDFsharp để VẼ khối chữ ký (cần nhúng font Unicode cho
    ///   tên người ký có dấu tiếng Việt).
    /// - <see cref="CadesSigner"/> giữ phần mật mã (CMS + signingCertificateV2 + dấu thời gian TSA).
    /// Xem .claude/be/plans/pdf-incremental-signing.plan.md và docs/pdf-signature-appearance.md.
    /// </summary>
    public class PdfSigner : IPdfSigner
    {
        private readonly ISigningKey _signingKey;
        private readonly ITimestampClient _timestampClient;
        // Tính vị trí + khổ khối chữ ký cho từng trang; tách riêng vì cùng một lựa chọn của người dùng phải
        // ra ô khác nhau trên các file khác khổ giấy.
        private readonly ISignaturePlacementResolver _placementResolver;

        // Khóa trên USB token là MỘT phiên phần cứng: hai luồng cùng ký một lúc dễ khóa token hoặc bung
        // hộp thoại PIN theo từng file. Semaphore này chỉ bao quanh THAO TÁC KÝ, không bao quanh cả file,
        // nên vòng gọi TSA (2-131s, phần chậm nhất) vẫn chạy song song.
        private readonly SemaphoreSlim _keyLock = new(1, 1);

        // PDFsharp 6 KHÔNG tự dùng font hệ thống: không bật cờ này thì vẽ chữ ký ném
        // "No appropriate font found for family name 'Times New Roman'".
        // Bật 1 lần cho cả process (cờ static của thư viện), khóa lại vì nhiều lô ký có thể chạy song song.
        private static readonly object FontInitLock = new();
        private static bool _fontsInitialized;

        public PdfSigner(ISigningKey signingKey, ITimestampClient timestampClient,
            ISignaturePlacementResolver placementResolver)
        {
            _signingKey = signingKey;
            _timestampClient = timestampClient;
            _placementResolver = placementResolver;
        }

        /// <inheritdoc/>
        public async Task SignAsync(string inputPath, string outputPath, PdfSignOptionsDto options,
            CancellationToken cancellationToken = default)
        {
            // Lấy chứng thư + khóa ĐÃ CACHE từ bước Verify. Không tự mở khóa mới ở đây: mở mới = hỏi PIN lại.
            var (cert, privateKey) = _signingKey is StoreSigningKey store
                ? store.GetCached(options.Thumbprint)
                : (null, null);

            if (cert == null || privateKey == null)
            {
                throw new InvalidOperationException(
                    "Chưa kiểm tra chứng thư số trước khi ký. Gọi Verify() trước khi bắt đầu lô ký.");
            }

            lock (FontInitLock)
            {
                if (!_fontsInitialized)
                {
                    GlobalFontSettings.UseWindowsFontsUnderWindows = true;
                    _fontsInitialized = true;
                }
            }

            var signedAt = DateTimeConstants.VietnamNow;
            var signer = new CadesSigner(cert, privateKey, _timestampClient, _keyLock, cancellationToken);

            var signedBytes = await PdfIncrementalSigner.SignAsync(
                await File.ReadAllBytesAsync(inputPath, cancellationToken),
                cert.GetNameInfo(X509NameType.SimpleName, false) ?? cert.Subject,
                string.IsNullOrWhiteSpace(options.Reason) ? SigningConstants.DefaultReason : options.Reason,
                string.IsNullOrWhiteSpace(options.Location) ? SigningConstants.DefaultLocation : options.Location,
                signedAt,
                await signer.GetSignatureSizeAsync(),
                // null/rỗng = giữ nguyên vị trí mặc định (góc trên-phải, khổ chuẩn co giãn theo trang).
                options.Placements,
                _placementResolver,
                // CadesSigner giữ NGUYÊN như cũ (nhận Stream) — phần mật mã đã chạy đúng, không đụng vào.
                async (bytes, _) => await signer.GetSignatureAsync(new MemoryStream(bytes)),
                cancellationToken);

            // Ghi ra file TẠM rồi mới đổi tên vào đúng chỗ. Ghi hỏng giữa chừng sẽ để lại file dở dang ngay
            // trong thư mục kết quả — người dùng mở ra thấy "PDF hỏng" mà thật ra là file chưa ghi xong.
            // Đổi tên là thao tác nguyên tử: thư mục kết quả chỉ chứa file đã ký xong hẳn.
            var tempPath = outputPath + ".tmp";
            try
            {
                await File.WriteAllBytesAsync(tempPath, signedBytes, cancellationToken);
                if (File.Exists(outputPath)) File.Delete(outputPath);
                File.Move(tempPath, outputPath);
            }
            catch
            {
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
                throw;
            }
        }

        /// <summary>
        /// Dựng chữ ký CAdES detached + gắn dấu thời gian RFC 3161.
        /// Bất khả kháng phải là class riêng: PDFsharp yêu cầu truyền vào một IDigitalSigner.
        /// </summary>
        private sealed class CadesSigner : IDigitalSigner
        {
            private readonly X509Certificate2 _cert;
            private readonly AsymmetricAlgorithm _privateKey;
            private readonly ITimestampClient _timestampClient;
            private readonly SemaphoreSlim _keyLock;
            private readonly CancellationToken _cancellationToken;

            public CadesSigner(X509Certificate2 cert, AsymmetricAlgorithm privateKey,
                ITimestampClient timestampClient, SemaphoreSlim keyLock, CancellationToken cancellationToken)
            {
                _cert = cert;
                _privateKey = privateKey;
                _timestampClient = timestampClient;
                _keyLock = keyLock;
                _cancellationToken = cancellationToken;
            }

            // IDigitalSigner khai non-nullable -> chặn null tại đây, không để lọt xuống PDFsharp.
            public string CertificateName =>
                _cert.GetNameInfo(X509NameType.SimpleName, false) ?? _cert.Subject;

            /// <summary>
            /// Chỗ trống phải chừa cho /Contents. Ước lượng THỪA có chủ đích: chữ ký + chain + token TSA
            /// không đoán chính xác được trước, mà chừa thiếu thì hỏng file, chừa thừa chỉ tốn vài KB.
            /// </summary>
            public Task<int> GetSignatureSizeAsync() => Task.FromResult(32 * 1024);

            public async Task<byte[]> GetSignatureAsync(Stream stream)
            {
                // RangedStream của PDFsharp: CÓ Length và cho set Position = 0, nhưng KHÔNG chịu Seek tùy ý.
                // Vì thế CopyToAsync ném "Cannot seek in a RangedStream", còn né seek hoàn toàn thì đọc từ
                // vị trí cuối và ra 0 byte. Phải đúng khuôn: cấp buffer theo Length, đưa Position về 0, rồi
                // đọc cho tới khi đầy (Read có thể trả về ít hơn yêu cầu).
                var content = new byte[stream.Length];
                stream.Position = 0;
                var offset = 0;
                while (offset < content.Length)
                {
                    _cancellationToken.ThrowIfCancellationRequested();
                    var read = stream.Read(content, offset, content.Length - offset);
                    if (read <= 0) break;
                    offset += read;
                }

                if (offset != content.Length)
                {
                    throw new InvalidOperationException(
                        $"Chỉ đọc được {offset}/{content.Length} byte của vùng cần ký trong PDF.");
                }

                var cms = new SignedCms(new ContentInfo(content), detached: true);

                // Truyền privateKey TƯỜNG MINH để dùng lại handle đã cache -> không hỏi PIN lại từng file.
                var cmsSigner = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, _cert, _privateKey)
                {
                    DigestAlgorithm = new Oid("2.16.840.1.101.3.4.2.1"), // SHA-256
                    IncludeOption = X509IncludeOption.WholeChain
                };
                cmsSigner.SignedAttributes.Add(new Pkcs9SigningTime(DateTime.UtcNow));

                // signingCertificateV2 (ESS): .NET KHÔNG tự thêm. Thiếu nó thì file không conform CAdES và
                // Adobe/Foxit có thể từ chối, dù verifier của SIPPACK vẫn nhận.
                // ASN.1: SigningCertificateV2 ::= SEQUENCE { certs SEQUENCE OF ESSCertIDv2 }
                //        ESSCertIDv2 ::= SEQUENCE { hashAlgorithm DEFAULT sha256, certHash OCTET STRING }
                var asn = new AsnWriter(AsnEncodingRules.DER);
                asn.PushSequence();                                  // SigningCertificateV2
                asn.PushSequence();                                  // certs
                asn.PushSequence();                                  // ESSCertIDv2
                asn.WriteOctetString(SHA256.HashData(_cert.RawData));
                asn.PopSequence();
                asn.PopSequence();
                asn.PopSequence();
                cmsSigner.SignedAttributes.Add(new AsnEncodedData(
                    new Oid(SigningConstants.OidSigningCertificateV2), asn.Encode()));

                // CHỈ thao tác chạm khóa mới phải xếp hàng — giữ semaphore qua cả vòng gọi TSA là mất hết
                // lợi ích song song (TSA chiếm gần như toàn bộ thời gian mỗi file).
                await _keyLock.WaitAsync(_cancellationToken);
                try
                {
                    cms.ComputeSignature(cmsSigner);
                }
                finally
                {
                    _keyLock.Release();
                }

                // Dấu thời gian đóng lên GIÁ TRỊ CHỮ KÝ (RFC 3161/CAdES), rồi gắn làm unsigned attribute.
                // FAIL-CLOSED: RequestTokenAsync ném lỗi thì cả file này hỏng — không bao giờ phát hành
                // chữ ký thiếu dấu thời gian.
                var signerInfo = cms.SignerInfos[0];
                var token = await _timestampClient.RequestTokenAsync(signerInfo.GetSignature(), _cancellationToken);
                signerInfo.AddUnsignedAttribute(new AsnEncodedData(
                    new Oid(SignatureConstants.OidSignatureTimeStampToken), token));

                return cms.Encode();
            }
        }
    }
}
