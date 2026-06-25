using Sip.be.Application.Document.Dtos.Pack;
using System.Threading;
using System.Threading.Tasks;

namespace Sip.be.Application.Document.Interfaces
{
    public interface IPackService
    {
        /// <summary>
        /// Đóng gói 1 thư mục hồ sơ (đã qua CheckService) thành gói SIP_tailieu theo chuẩn TT05.
        /// Đọc file Excel trong metadata/ (sheet Ho_so + Van_ban), map trường theo 2 bảng config
        /// (ConfigHoSo/ConfigVanBan) trong DB, sinh EAD.xml + EAD_DOC_*.xml + METS.xml (gốc và bản đại diện),
        /// copy tài liệu từ attachment/ vào representations/rep1/data/ và schemas (.xsd), cuối cùng nén ZIP.
        /// Không lưu dữ liệu gói vào DB.
        /// </summary>
        Task<PackResultDto> PackHoSoFolderAsync(PackHoSoFolderRequestDto input, CancellationToken cancellationToken = default);
    }
}
