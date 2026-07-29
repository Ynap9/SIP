using Sip.be.Shared.Request;

namespace Sip.be.Application.Document.Dtos.Sign
{
    /// <summary>
    /// 1 event stream qua SSE trong quá trình ký, cùng khuôn với CheckProgressDto của luồng check
    /// để FE tái sử dụng được cách đọc stream và cách tick từng dòng.
    /// TargetType: manifest (seed sẵn danh sách file) | file (ký xong 1 file) | summary (tổng kết).
    /// </summary>
    public class SignProgressDto
    {
        /// <summary>Loại event: manifest | file | summary.</summary>
        public string TargetType { get; set; } = string.Empty;

        /// <summary>Định danh đối tượng (tên file) để FE biết tick dòng nào.</summary>
        public string Target { get; set; } = string.Empty;

        /// <summary>Trạng thái: Success = 1, Error = 0.</summary>
        public StatusCodeE Status { get; set; }

        /// <summary>200 nếu ổn, hoặc ErrorCode nếu lỗi.</summary>
        public int Code { get; set; }

        /// <summary>Thông điệp hiển thị.</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>True nếu đây là event tổng kết cuối cùng.</summary>
        public bool Completed { get; set; }

        /// <summary>Số file đã xử lý tới event này.</summary>
        public int Current { get; set; }

        /// <summary>Tổng số file phải ký.</summary>
        public int Total { get; set; }

        /// <summary>
        /// Phần trăm tiến trình (0-100) cho thanh progress bên FE. Tính theo SỐ FILE chứ không theo dung lượng:
        /// chi phí mỗi file bị chi phối bởi vòng gọi TSA gần như cố định, nên đếm file bám sát thời gian
        /// cảm nhận hơn là đếm byte.
        /// </summary>
        public int Percent => Total > 0 ? (int)System.Math.Round(Current * 100.0 / Total) : 0;

        /// <summary>Danh sách tên file (chỉ có ở event manifest) để FE seed sẵn các dòng.</summary>
        public List<string>? Files { get; set; }

        /// <summary>Kết quả ký của 1 file (chỉ có ở event file).</summary>
        public SignFileResultDto? File { get; set; }

        /// <summary>Tổng kết cả lô (chỉ có ở event summary).</summary>
        public SignResultDto? Summary { get; set; }
    }
}
