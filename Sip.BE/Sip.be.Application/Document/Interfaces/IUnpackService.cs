using Sip.be.Application.Document.Dtos.Unpack;
using System.Threading;
using System.Threading.Tasks;

namespace Sip.be.Application.Document.Interfaces
{
    public interface IUnpackService
    {
        /// <summary>
        /// Dịch ngược 1 gói SIP (file .zip hoặc thư mục gói) về thư mục hồ sơ (metadata/ + attachment/).
        /// Đọc EAD.xml/EAD_DOC_*.xml, dựng file Excel Ho_so+Van_ban theo template: header lấy từ
        /// ConfigHoSo/ConfigVanBan (map qua EadFieldMapping) - trường bắt buộc luôn có cột, trường không
        /// bắt buộc chỉ có cột khi gói có dữ liệu; bung mã enum "01" -> "01: nhãn" theo config.
        /// Copy tài liệu từ representations/rep1/data/ sang attachment/. Trả kèm cây thư mục để FE hiển thị library.
        /// </summary>
        Task<UnpackResultDto> UnpackOneAsync(UnpackRequestDto input, CancellationToken cancellationToken = default);
    }
}
