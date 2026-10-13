using Sip.be.External.Documents.Dtos;
using Sip.be.Shared.Request;

namespace Sip.be.Application.Document.Dtos.Sign
{
    /// <summary>Yêu cầu kiểm tra định dạng: 1 file PDF hoặc 1 thư mục (kiểm MỌI file, kể cả file không phải PDF).</summary>
    public class PdfFormatCheckRequestDto
    {
        /// <summary>Đường dẫn tuyệt đối tới 1 file hoặc 1 thư mục cần kiểm tra trước khi ký.</summary>
        public string SourcePath { get; set; } = string.Empty;
    }

    /// <summary>Kết quả kiểm định dạng của 1 file, nhúng trong event "file" của stream.</summary>
    public class PdfFormatFileResultDto
    {
        /// <summary>Tên file (kèm đuôi).</summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>Đường dẫn tuyệt đối.</summary>
        public string FullPath { get; set; } = string.Empty;

        /// <summary>Verdict cấp file.</summary>
        public PdfFormatVerdict Verdict { get; set; }

        public bool CanSign { get; set; }

        /// <summary>Thông điệp tiếng Việt hiển thị cho người dùng (ok hoặc lý do chặn).</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>Phần PDF/A (1/2/3) nếu đọc được.</summary>
        public string? PdfAPart { get; set; }

        /// <summary>Mức tuân thủ PDF/A (A/B/U) nếu đọc được.</summary>
        public string? PdfAConformance { get; set; }
    }

    /// <summary>Tổng kết cả lô kiểm định dạng (chỉ có ở event summary).</summary>
    public class PdfFormatCheckSummaryDto
    {
        /// <summary>Tổng số file đã kiểm.</summary>
        public int TotalFiles { get; set; }

        public int ValidCount { get; set; }

        /// <summary>Số file KHÔNG ký được — đếm theo CanSign = false.</summary>
        public int InvalidCount { get; set; }

        /// <summary>True khi mọi file đều ký được -> cả lô được phép sang bước ký.</summary>
        public bool AllValid => InvalidCount == 0;
    }

    /// <summary>
    /// 1 event stream qua SSE trong quá trình kiểm định dạng, cùng khuôn với SignProgressDto để FE tái dùng
    /// cách đọc stream và tick từng dòng.
    /// TargetType: manifest (seed danh sách file) | file (kiểm xong 1 file) | summary (tổng kết).
    /// </summary>
    public class PdfFormatCheckProgressDto
    {
        /// <summary>Loại event: manifest | file | summary.</summary>
        public string TargetType { get; set; } = string.Empty;

        /// <summary>Định danh đối tượng (tên file / thư mục nguồn) để FE biết tick dòng nào.</summary>
        public string Target { get; set; } = string.Empty;

        /// <summary>Trạng thái: Success = 1, Error = 0.</summary>
        public StatusCodeE Status { get; set; }

        /// <summary>200 nếu ổn, hoặc ErrorCode nếu file không ký được.</summary>
        public int Code { get; set; }

        /// <summary>Thông điệp hiển thị.</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>True nếu đây là event tổng kết cuối cùng.</summary>
        public bool Completed { get; set; }

        /// <summary>Số file đã kiểm tới event này.</summary>
        public int Current { get; set; }

        /// <summary>Tổng số file phải kiểm.</summary>
        public int Total { get; set; }

        /// <summary>Phần trăm tiến trình (0-100) cho thanh progress, tính theo SỐ FILE.</summary>
        public int Percent => Total > 0 ? (int)System.Math.Round(Current * 100.0 / Total) : 0;

        /// <summary>Danh sách tên file (chỉ có ở event manifest) để FE seed sẵn các dòng.</summary>
        public List<string>? Files { get; set; }

        /// <summary>Kết quả kiểm của 1 file (chỉ có ở event file).</summary>
        public PdfFormatFileResultDto? File { get; set; }

        /// <summary>Tổng kết cả lô (chỉ có ở event summary).</summary>
        public PdfFormatCheckSummaryDto? Summary { get; set; }
    }
}
