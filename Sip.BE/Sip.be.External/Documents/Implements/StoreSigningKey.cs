using Sip.be.External.Documents.Dtos;
using Sip.be.External.Documents.Interfaces;
using Sip.be.Shared.Constants;
using Sip.be.Shared.Constants.Documents;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Sip.be.External.Documents.Implements
{
    /// <summary>
    /// Nguồn khóa ký = Windows certificate store (cert cá nhân, cert cấp máy, cert trên USB token — token do
    /// middleware bắc cầu vào store nên đọc chung một đường). Ký qua CNG.
    /// Xem .claude/be/plans/pdf-signing-be.plan.md step 3.
    /// </summary>
    public class StoreSigningKey : ISigningKey
    {
        // ===== CACHE CHO 1 LÔ KÝ =====
        // Không có cache thì mỗi file lại quét 2 cert store + dựng chain + ký thử: ký 500 file là 500 vòng,
        // và tệ hơn nữa là 500 lần bật hộp thoại PIN.
        // Giữ luôn HANDLE KHÓA (_privateKey) chứ không chỉ cert: middleware token cache PIN theo handle này,
        // nên PIN chỉ hỏi 1 lần ở bước Verify rồi cả lô ký im lặng.
        // Vòng đời: tạo ở Verify -> dùng suốt lô -> ClearCache() xóa (kết thúc lô HOẶC phát hiện rút token).
        private readonly object _cacheLock = new();
        private string? _cachedThumbprint;
        private X509Certificate2? _cachedCert;
        private TokenVerifyDto? _cachedResult;
        private RSA? _cachedRsa;
        private ECDsa? _cachedEcdsa;

        /// <inheritdoc/>
        public TokenVerifyDto Verify(string thumbprint)
        {
            lock (_cacheLock)
            {
                // Cùng thumbprint trong cùng lô -> trả bản cache, không chạy lại ký thử và dựng chain.
                if (_cachedResult != null &&
                    string.Equals(_cachedThumbprint, thumbprint, StringComparison.OrdinalIgnoreCase))
                {
                    return _cachedResult;
                }

                // Đổi sang chứng thư khác -> bỏ toàn bộ cache cũ (kể cả handle khóa của token trước).
                ClearCacheInternal();

                var dto = new TokenVerifyDto { Thumbprint = thumbprint ?? string.Empty };

                // ----- Bước 1a: tìm chứng thư theo thumbprint -----
                var cert = ScanStoreForCertificate(thumbprint);
                if (cert == null)
                {
                    dto.FailedCheck = nameof(dto.FoundInStore);
                    dto.Reason = "Không tìm thấy chứng thư số trên máy. Kiểm tra USB token đã cắm và middleware (VGCA PKI Manager/bit4id xPKI) đã chạy chưa.";
                    return dto;
                }

                dto.FoundInStore = true;
                dto.CommonName = cert.GetNameInfo(X509NameType.SimpleName, false);
                dto.IssuerCommonName = cert.GetNameInfo(X509NameType.SimpleName, true);

                // ----- Bước 1b: có khóa bí mật -----
                if (!cert.HasPrivateKey)
                {
                    dto.FailedCheck = nameof(dto.HasPrivateKey);
                    dto.Reason = "Chứng thư số không có khóa bí mật trên máy này nên không ký được.";
                    return dto;
                }
                dto.HasPrivateKey = true;

                // ----- Bước 1c: còn hạn (giờ Việt Nam, KHÔNG dùng DateTime.Now) -----
                var now = DateTimeConstants.VietnamNow;
                if (now < cert.NotBefore || now > cert.NotAfter)
                {
                    dto.FailedCheck = nameof(dto.NotExpired);
                    dto.Reason = $"Chứng thư số ngoài thời hạn hiệu lực ({cert.NotBefore:dd/MM/yyyy HH:mm:ss} - {cert.NotAfter:dd/MM/yyyy HH:mm:ss}).";
                    return dto;
                }
                dto.NotExpired = true;

                // ----- Bước 1d: KeyUsage cho phép ký -----
                // Không khai KeyUsage -> coi là CHO PHÉP (RFC 5280: thiếu extension = không giới hạn mục đích).
                var keyUsage = cert.Extensions.OfType<X509KeyUsageExtension>().FirstOrDefault();
                var allowsSigning = keyUsage == null
                    || keyUsage.KeyUsages.HasFlag(X509KeyUsageFlags.DigitalSignature)
                    || keyUsage.KeyUsages.HasFlag(X509KeyUsageFlags.NonRepudiation);
                if (!allowsSigning)
                {
                    dto.FailedCheck = nameof(dto.AllowsSigning);
                    dto.Reason = "Chứng thư số không được cấp quyền ký (KeyUsage thiếu digitalSignature/nonRepudiation).";
                    return dto;
                }
                dto.AllowsSigning = true;

                // ----- Bước 2: KÝ THỬ (và đây là chỗ HỎI PIN, đúng 1 lần cho cả lô) -----
                // Bằng chứng DUY NHẤT rằng token đang cắm thật và PIN dùng được: mọi bước trên chỉ đọc metadata,
                // có thể "đạt hết" trong khi token đã rút từ lâu.
                // Handle khóa lấy ở đây được GIỮ LẠI để cả lô ký dùng chung -> middleware không hỏi PIN nữa.
                try
                {
                    var probe = RandomNumberGenerator.GetBytes(SigningConstants.PreflightTestDataSize);
                    var rsa = cert.GetRSAPrivateKey();
                    if (rsa != null)
                    {
                        // Chỉ cho khóa CNG (KSP): báo cho middleware cửa sổ chủ của hộp PIN trước khi ký thử.
                        // NCRYPT_WINDOW_HANDLE_PROPERTY có tên chuỗi đúng là "HWND Handle". Không set thì hộp
                        // thoại PIN không có chủ - luồng gọi là luồng request của Kestrel, không sở hữu cửa sổ
                        // nào - nên nó hiện chìm sau cửa sổ app hoặc không hiện, và người dùng bị kẹt ở đây.
                        // Khóa dạng CSP đời cũ không nhận thuộc tính này; host đặt PP_CLIENT_HWND cấp tiến trình.
                        if (rsa is RSACng rsaCng && SigningUiContext.WindowHandle != IntPtr.Zero)
                        {
                            try
                            {
                                var hwndBytes = IntPtr.Size == 8
                                    ? BitConverter.GetBytes(SigningUiContext.WindowHandle.ToInt64())
                                    : BitConverter.GetBytes(SigningUiContext.WindowHandle.ToInt32());
                                rsaCng.Key.SetProperty(
                                    new CngProperty("HWND Handle", hwndBytes, CngPropertyOptions.None));
                            }
                            catch
                            {
                                // Middleware không hỗ trợ thuộc tính này -> vẫn ký thử bình thường, hộp PIN
                                // có thể hiện không đúng lớp nhưng chặn ở đây thì mất luôn đường ký.
                            }
                        }

                        rsa.SignData(probe, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                        _cachedRsa = rsa;
                    }
                    else
                    {
                        var ecdsa = cert.GetECDsaPrivateKey()
                            ?? throw new InvalidOperationException("Chứng thư số không dùng thuật toán RSA hoặc ECDSA.");
                        ecdsa.SignData(probe, HashAlgorithmName.SHA256);
                        _cachedEcdsa = ecdsa;
                    }
                }
                catch (Exception ex)
                {
                    ClearCacheInternal();
                    dto.FailedCheck = nameof(dto.CanSignTest);
                    dto.Reason = $"Không dùng được khóa bí mật để ký (token chưa cắm, PIN bị hủy, hoặc middleware chưa sẵn sàng): {ex.Message}";
                    return dto;
                }
                dto.CanSignTest = true;

                // ----- Bước 3: chain về CA Ban Cơ yếu đã ghim -----
                // Xét tại thời điểm hiện tại vì ta sắp ký NGAY BÂY GIỜ; dùng UtcNow (mốc tuyệt đối, không phụ
                // thuộc múi giờ máy) cho khớp cách SignatureVerifier dựng chain.
                if (!SignatureVerifier.IsTrustedSignerCert(cert, DateTime.UtcNow))
                {
                    ClearCacheInternal();
                    dto.FailedCheck = nameof(dto.TrustedChain);
                    dto.Reason = "Chứng thư số không thuộc CA Ban Cơ yếu (chain không về Root G1 lẫn Root G2 đã ghim). Ký bằng chứng thư này thì mọi file sẽ bị chặn khi đóng gói.";
                    return dto;
                }
                dto.TrustedChain = true;
                dto.Valid = true;

                _cachedThumbprint = thumbprint;
                _cachedCert = cert;
                _cachedResult = dto;
                return dto;
            }
        }

        /// <inheritdoc/>
        public byte[] SignData(string thumbprint, byte[] data)
        {
            lock (_cacheLock)
            {
                if (!string.Equals(_cachedThumbprint, thumbprint, StringComparison.OrdinalIgnoreCase)
                    || (_cachedRsa == null && _cachedEcdsa == null))
                {
                    // Không tự verify lại ở đây: tầng trên phải gọi Verify TRƯỚC khi vào lô. Ký mà chưa verify
                    // nghĩa là luồng gọi sai — báo lỗi rõ còn hơn âm thầm bật PIN giữa lúc chạy song song.
                    throw new InvalidOperationException(
                        "Chưa kiểm tra chứng thư số trước khi ký. Gọi Verify() trước khi bắt đầu lô ký.");
                }

                if (_cachedRsa != null)
                    return _cachedRsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

                return _cachedEcdsa!.SignData(data, HashAlgorithmName.SHA256);
            }
        }

        /// <summary>
        /// Chứng thư + KHÓA đã cache, cho PdfSigner (cùng assembly) dựng CMS dùng lại ĐÚNG handle này.
        /// Bắt buộc phải dùng lại: nếu để SignedCms.ComputeSignature tự lấy khóa từ cert thì nó mở handle MỚI
        /// mỗi file -> middleware hỏi PIN lại từng file, đúng thứ cache sinh ra để tránh.
        /// Trả (null, null) nếu chưa Verify hoặc thumbprint không khớp.
        /// </summary>
        internal (X509Certificate2? Cert, AsymmetricAlgorithm? Key) GetCached(string thumbprint)
        {
            lock (_cacheLock)
            {
                if (!string.Equals(_cachedThumbprint, thumbprint, StringComparison.OrdinalIgnoreCase))
                    return (null, null);

                return (_cachedCert, (AsymmetricAlgorithm?)_cachedRsa ?? _cachedEcdsa);
            }
        }

        /// <inheritdoc/>
        public bool IsTokenPresent(string thumbprint)
        {
            // CỐ TÌNH không đọc cache: cache chính là thứ cần kiểm chứng. Cũng không chạm khóa bí mật nên
            // job giám sát gọi liên tục cũng không bật hộp thoại PIN.
            return ScanStoreForCertificate(thumbprint) != null;
        }

        /// <inheritdoc/>
        public void ClearCache()
        {
            lock (_cacheLock)
            {
                ClearCacheInternal();
            }
        }

        /// <summary>
        /// Xóa cache + nhả handle khóa. Bất khả kháng phải tách: gọi từ 4 chỗ (đổi cert, ký thử hỏng,
        /// chain hỏng, ClearCache công khai), viết lồng vào từng chỗ là nhân bốn cùng một đoạn dispose.
        /// Người gọi phải đang giữ _cacheLock.
        /// </summary>
        private void ClearCacheInternal()
        {
            _cachedRsa?.Dispose();
            _cachedEcdsa?.Dispose();
            _cachedCert?.Dispose();
            _cachedRsa = null;
            _cachedEcdsa = null;
            _cachedCert = null;
            _cachedResult = null;
            _cachedThumbprint = null;
        }

        /// <summary>
        /// Quét cert store (cá nhân rồi tới máy) tìm chứng thư theo thumbprint. Luôn quét tươi, không cache.
        /// Bất khả kháng phải tách: Verify và IsTokenPresent đều cần, mà IsTokenPresent phải bỏ qua cache.
        /// </summary>
        internal static X509Certificate2? ScanStoreForCertificate(string? thumbprint)
        {
            if (string.IsNullOrWhiteSpace(thumbprint)) return null;

            foreach (var location in new[] { StoreLocation.CurrentUser, StoreLocation.LocalMachine })
            {
                try
                {
                    using var store = new X509Store(StoreName.My, location);
                    // KHÔNG dùng OpenExistingOnly: store "My" được Windows tạo lười, profile chưa từng có
                    // chứng thư cá nhân thì cờ này làm Open() ném lỗi và nhánh catch nuốt mất cả store.
                    // Xem docs/bug/cert-store-openexistingonly-skips-whole-store.md.
                    store.Open(OpenFlags.ReadOnly);
                    var found = store.Certificates
                        .OfType<X509Certificate2>()
                        .FirstOrDefault(c => string.Equals(c.Thumbprint, thumbprint, StringComparison.OrdinalIgnoreCase));
                    if (found != null) return found;
                }
                catch
                {
                    // Store mở lỗi (LocalMachine thường cần quyền) -> bỏ qua store đó, không hỏng cả lời gọi.
                }
            }

            return null;
        }
    }
}
