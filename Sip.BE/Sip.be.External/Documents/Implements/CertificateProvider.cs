using Sip.be.External.Documents.Dtos;
using Sip.be.External.Documents.Interfaces;
using Sip.be.Shared.Constants;
using Sip.be.Shared.Constants.Documents;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Sip.be.External.Documents.Implements
{
    /// <summary>
    /// Đọc chứng thư số từ Windows certificate store. Cert cá nhân, cert cấp máy và cert trên USB token
    /// đều nằm trong store (token được middleware bắc cầu qua minidriver) -> một đường đọc duy nhất, zero-dep.
    /// Xem docs/plans/pdf-signing.plan.md step 2.
    /// </summary>
    public class CertificateProvider : ICertificateProvider
    {
        // Store cần quét: (vị trí, nguồn mặc định khi khóa KHÔNG nằm ở phần cứng). Token xuất hiện ở CurrentUser.
        private static readonly (StoreLocation Location, CertSource Source)[] Stores =  
        {
            (StoreLocation.CurrentUser, CertSource.Local),
            (StoreLocation.LocalMachine, CertSource.Server),
        };

        /// <inheritdoc/>
        public List<SignCertDto> GetCertificates()
        {
            var result = new List<SignCertDto>();
            var now = DateTimeConstants.VietnamNow;

            foreach (var (location, defaultSource) in Stores)
            {
                // Store mở lỗi -> bỏ qua store đó, KHÔNG ném: LocalMachine thường không mở được khi chạy
                // quyền user thường, đó là trường hợp bình thường chứ không phải lỗi hỏng cả lời gọi.
                var certs = new List<X509Certificate2>();
                try
                {
                    using var store = new X509Store(StoreName.My, location);
                    store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
                    certs = store.Certificates.OfType<X509Certificate2>().ToList();
                }
                catch
                {
                    continue;
                }

                foreach (var cert in certs)
                {
                    // Tên CSP/KSP giữ private key -> căn cứ suy ra cert nằm trên token hay trong phần mềm.
                    // CHỈ đọc metadata của khóa (không ký, không xuất) nên không bật hộp thoại nhập PIN.
                    string? keyProvider = null;
                    if (cert.HasPrivateKey)
                    {
                        try
                        {
                            using var rsa = cert.GetRSAPrivateKey();
                            if (rsa is RSACng rsaCng)
                            {
                                keyProvider = rsaCng.Key.Provider?.Provider;
                            }
#pragma warning disable SYSLIB0028 // CAPI đời cũ: một số middleware token đăng ký khóa qua CSP, không phải KSP.
                            else if (rsa is RSACryptoServiceProvider rsaCapi)
                            {
                                keyProvider = rsaCapi.CspKeyContainerInfo.ProviderName;
                            }
#pragma warning restore SYSLIB0028 
                            else
                            {
                                using var ecdsa = cert.GetECDsaPrivateKey();
                                if (ecdsa is ECDsaCng ecdsaCng) keyProvider = ecdsaCng.Key.Provider?.Provider;
                            }
                        }
                        catch
                        {
                            // Token rút giữa chừng / middleware chưa sẵn sàng -> không xác định được provider.
                            keyProvider = null;
                        }
                    }

                    // Khóa ở provider PHẦN CỨNG -> UsbToken. Phải loại danh sách provider phần mềm TRƯỚC vì
                    // "Microsoft Software Key Storage Provider" cũng chứa chữ "key storage" nhưng là khóa mềm.
                    var isHardwareKey = !string.IsNullOrWhiteSpace(keyProvider)
                        && !SigningConstants.SoftwareKeyProviderMarkers.Any(marker =>
                            keyProvider.Contains(marker, StringComparison.OrdinalIgnoreCase))
                        && SigningConstants.HardwareKeyProviderMarkers.Any(marker =>
                            keyProvider.Contains(marker, StringComparison.OrdinalIgnoreCase));

                    // KeyUsage có cho ký không. Cert KHÔNG khai KeyUsage -> coi là CHO PHÉP (RFC 5280: thiếu
                    // extension nghĩa là không giới hạn mục đích) — chặn ở đây là loại oan cert hợp lệ.
                    var keyUsage = cert.Extensions.OfType<X509KeyUsageExtension>().FirstOrDefault();
                    var allowsSigning = keyUsage == null
                        || keyUsage.KeyUsages.HasFlag(X509KeyUsageFlags.DigitalSignature)
                        || keyUsage.KeyUsages.HasFlag(X509KeyUsageFlags.NonRepudiation);

                    var dto = new SignCertDto
                    {
                        Subject = cert.Subject,
                        CommonName = cert.GetNameInfo(X509NameType.SimpleName, false) ?? string.Empty,
                        Issuer = cert.Issuer,
                        IssuerCommonName = cert.GetNameInfo(X509NameType.SimpleName, true) ?? string.Empty,
                        SerialNumber = cert.SerialNumber,
                        Thumbprint = cert.Thumbprint,
                        Source = isHardwareKey ? CertSource.UsbToken : defaultSource,
                        KeyProvider = keyProvider,
                        ValidFrom = cert.NotBefore.ToString("dd/MM/yyyy HH:mm:ss"),
                        ValidTo = cert.NotAfter.ToString("dd/MM/yyyy HH:mm:ss"),
                        HasPrivateKey = cert.HasPrivateKey,
                        IsExpired = now < cert.NotBefore || now > cert.NotAfter,
                        AllowsSigning = allowsSigning,
                    };

                    // Nêu lý do ĐẦU TIÊN chặn việc ký; null = ký được.
                    dto.Reason = !dto.HasPrivateKey
                        ? "Chứng thư số không có khóa bí mật trên máy này (chỉ xem được, không ký được)."
                        : dto.IsExpired
                            ? $"Chứng thư số ngoài thời hạn hiệu lực ({dto.ValidFrom} - {dto.ValidTo})."
                            : !dto.AllowsSigning
                                ? "Chứng thư số không được cấp quyền ký (KeyUsage thiếu digitalSignature/nonRepudiation)."
                                : null;

                    result.Add(dto);
                }
            }

            // Cùng 1 cert có thể nằm ở cả 2 store -> gộp theo thumbprint, giữ bản ký được.
            return result
                .GroupBy(c => c.Thumbprint, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(c => c.CanSign).First())
                .OrderByDescending(c => c.CanSign)
                .ThenBy(c => c.CommonName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
    }
}
