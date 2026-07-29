using Sip.be.External.Documents.Dtos;

namespace Sip.be.Application.Document.Dtos.Sign
{
    /// <summary>Tham số lọc cho API liệt kê chứng thư số.</summary>
    public class SignCertQueryDto
    {
        /// <summary>Chỉ trả cert ký được (<see cref="SignCertDto.CanSign"/>). Mặc định false = trả hết kèm lý do.</summary>
        public bool OnlySignable { get; set; } = false;
    }
}
