using Sip.be.Application.Document.Dtos.Check;
using System.Collections.Generic;
using System.Threading;

namespace Sip.be.Application.Document.Interfaces
{
    public interface ICheckService
    {
        /// <summary>
        /// Kiểm tra 1 thư mục hồ sơ theo 3 giai đoạn, stream kết quả theo từng sự kiện (cho SSE):
        /// Giai đoạn 1: cấu trúc thư mục (metadata + attachment, file Excel đọc được, file đính kèm hợp lệ, đủ 2 sheet Ho_so/Van_ban).
        /// Giai đoạn 2: khớp mã hồ sơ với tên thư mục và mã định danh tài liệu với tên file đính kèm.
        /// Giai đoạn 3: nội dung từng trường trong sheet Ho_so/Van_ban so với mô tả trong 2 bảng config.
        /// Mỗi bước check xong sẽ yield 1 CheckProgressDto (đúng hoặc lỗi) để FE tick realtime.
        /// </summary>
        IAsyncEnumerable<CheckProgressDto> CheckHoSoFolderAsync(CheckHoSoFolderRequestDto input, CancellationToken cancellationToken = default);

        /// <summary>
        /// Kiểm tra NHIỀU hồ sơ trong 1 thư mục CHA, stream tiến trình theo từng hồ sơ (cho SSE).
        /// Phase 1 (tuần tự): kiểm tra cấu trúc thư mục cha + trùng lặp (tên thư mục hồ sơ, tên file);
        /// lỗi cấp cha thì dừng ngay, hồ sơ con lỗi cấu trúc thì đánh dấu lỗi và vẫn tiếp tục.
        /// Phase 2 (song song, giới hạn luồng): kiểm tra nội dung từng hồ sơ hợp lệ + phát hiện trùng mã hồ sơ.
        /// Mỗi hồ sơ xử lý xong yield 1 MultiCheckProgressDto; event cuối là tổng kết toàn batch.
        /// </summary>
        IAsyncEnumerable<MultiCheckProgressDto> CheckMultiFolderAsync(CheckMultiFolderRequestDto input, CancellationToken cancellationToken = default);
    }
}
