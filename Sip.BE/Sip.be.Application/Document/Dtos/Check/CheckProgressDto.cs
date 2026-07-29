using Sip.be.Shared.Request;
using Sip.be.External.Documents.Dtos;

namespace Sip.be.Application.Document.Dtos.Check
{
    /// <summary>
    /// Giai đoạn kiểm tra. Quá trình check chia làm 3 giai đoạn theo yêu cầu:
    /// 1) Cấu trúc thư mục, 2) Khớp mã hồ sơ / mã định danh tài liệu, 3) Nội dung Excel.
    /// </summary>
    public enum CheckStage
    {
        FolderStructure = 1,
        CodeMatching = 2,
        ExcelContent = 3
    }

    /// <summary>
    /// 1 sự kiện (event) trả về cho FE theo từng bước check, dùng để stream qua SSE.
    /// Mỗi khi check xong 1 thư mục / 1 file / 1 trường... sẽ bắn 1 event để FE tick xanh.
    /// </summary>
    public class CheckProgressDto
    {
        /// <summary>Giai đoạn check (1, 2, 3).</summary>
        public int Stage { get; set; }

        /// <summary>Tên giai đoạn để hiển thị.</summary>
        public string StageName { get; set; } = string.Empty;

        /// <summary>Loại đối tượng được check: folder | file | sheet | field | code | summary.</summary>
        public string TargetType { get; set; } = string.Empty;

        /// <summary>Định danh đối tượng được check (vd: tên file, tên trường, mã...) để FE biết tick item nào.</summary>
        public string Target { get; set; } = string.Empty;

        /// <summary>Trạng thái: Success = 1, Error = 0.</summary>
        public StatusCodeE Status { get; set; }

        /// <summary>Mã: 200 nếu hợp lệ, hoặc ErrorCode nếu lỗi.</summary>
        public int Code { get; set; }

        /// <summary>Thông điệp: "Chính xác" khi đúng, hoặc nguyên nhân lỗi khi sai.</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>True nếu đây là event tổng kết cuối cùng (đã check xong toàn bộ).</summary>
        public bool Completed { get; set; }

        /// <summary>Số bước đã xử lý tới event này (dùng tính % tiến trình).</summary>
        public int Current { get; set; }

        /// <summary>Tổng số bước ước lượng của toàn bộ quá trình check.</summary>
        public int Total { get; set; }

        /// <summary>Phần trăm tiến trình realtime (0-100), tính từ Current/Total.</summary>
        public int Percent => Total > 0 ? (int)System.Math.Round(Current * 100.0 / Total) : 0;

        /// <summary>Danh sách tên file đính kèm (chỉ có ở event manifest đầu giai đoạn 1) để FE seed sẵn các dòng file.</summary>
        public List<string>? Files { get; set; }

        /// <summary>Thông tin ký số của file (chỉ có ở event kiểm tra 1 file đính kèm, giai đoạn 1).</summary>
        public SignatureInfoDto? Signature { get; set; }
    }
}
