using Sip.be.External.Documents.Interfaces;
using Sip.be.Shared.Constants.Documents;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;

namespace Sip.be.External.Documents.Implements
{
    /// <summary>
    /// Client RFC 3161 gọi TSA Ban Cơ yếu Chính phủ (tsa.ca.gov.vn) bằng System.Security.Cryptography.Pkcs
    /// của BCL — không cần thư viện ngoài. Xem .claude/be/plans/pdf-signing-be.plan.md step 2.
    /// </summary>
    public class TimestampClient : ITimestampClient
    {
        // 1 HttpClient dùng chung cho cả vòng đời process: tạo mới mỗi lần gọi sẽ cạn socket khi ký cả lô.
        private static readonly HttpClient Http = new()
        {
            Timeout = TimeSpan.FromSeconds(SigningConstants.TsaTimeoutSeconds)
        };

        /// <inheritdoc/>
        public async Task<byte[]> RequestTokenAsync(byte[] signatureValue, CancellationToken cancellationToken = default)
        {
            var hash = SHA256.HashData(signatureValue);

            // nonce: chống replay — ProcessResponse đối chiếu lại, nên TSA trả token của request khác sẽ bị bắt.
            // requestSignerCertificates: true -> TSA nhúng chain của nó vào token, nhờ đó verify được OFFLINE
            // về sau (SignatureVerifier dựng chain từ chính các cert nằm trong token).
            var nonce = RandomNumberGenerator.GetBytes(8);
            var request = Rfc3161TimestampRequest.CreateFromHash(
                hash,
                HashAlgorithmName.SHA256,
                requestedPolicyId: null,
                nonce: nonce,
                requestSignerCertificates: true);

            using var content = new ByteArrayContent(request.Encode());
            content.Headers.ContentType = new MediaTypeHeaderValue(SigningConstants.TsaContentType);

            using var response = await Http.PostAsync(SigningConstants.TsaUrl, content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"TSA trả về HTTP {(int)response.StatusCode}. Không lấy được dấu thời gian.");
            }

            var replyBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var token = request.ProcessResponse(replyBytes, out _);
            return token.AsSignedCms().Encode();
        }
    }
}
