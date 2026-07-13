using Sip.be.Application.Document.Dtos.Pack;
using System.Collections.Generic;
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

        /// <summary>
        /// Đóng gói NHIỀU hồ sơ trong 1 thư mục CHA, stream tiến trình theo từng hồ sơ (cho SSE).
        /// Tự nhận diện cả 3 cấu trúc như CheckService: loại 1 (mỗi hồ sơ có metadata/ + attachment/) và loại 2/3
        /// (file data trực tiếp trong hồ sơ + 1 Excel dùng CHUNG ở thư mục cha / parent/metadata) — chọn nhánh qua
        /// ResolveSharedExcelPath. BE tạo 1 thư mục cha output "{tên thư mục cha}_{timestamp}" bên trong OutputPath,
        /// mỗi hồ sơ đóng thành 1 file ZIP đặt vào đó (chạy song song, giới hạn luồng). Nạp config 1 lần trước khi
        /// chia luồng; hồ sơ nào lỗi thì đánh dấu lỗi và vẫn tiếp tục các hồ sơ còn lại.
        /// Mỗi hồ sơ đóng xong yield 1 MultiPackProgressDto; event cuối là tổng kết toàn batch.
        /// </summary>
        IAsyncEnumerable<MultiPackProgressDto> PackMultiFolderAsync(PackMultiFolderRequestDto input, CancellationToken cancellationToken = default);
    }
}
