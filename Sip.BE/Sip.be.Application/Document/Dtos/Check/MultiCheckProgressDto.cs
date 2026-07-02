using Sip.be.Shared.Request;

namespace Sip.be.Application.Document.Dtos.Check
{
    /// <summary>
    /// Giai đoạn của luồng kiểm tra nhiều hồ sơ.
    /// </summary>
    public enum MultiCheckPhase
    {
        /// <summary>Phase 1: kiểm tra cấu trúc thư mục CHA + trùng lặp (tuần tự, fail-fast ở cấp cha).</summary>
        StructureValidation = 1,

        /// <summary>Phase 2: kiểm tra nội dung từng hồ sơ (chạy song song).</summary>
        Processing = 2,

        /// <summary>Tổng kết toàn batch.</summary>
        Done = 3
    }

    /// <summary>
    /// 1 sự kiện tiến trình của luồng kiểm tra nhiều hồ sơ (stream qua SSE).
    /// Khác luồng 1 hồ sơ: mỗi event tương ứng 1 HỒ SƠ (không phải từng file/trường) để tránh
    /// bắn quá nhiều event khi batch lớn. Chi tiết lỗi của hồ sơ gộp trong <see cref="Errors"/>.
    /// </summary>
    public class MultiCheckProgressDto
    {
        /// <summary>Giai đoạn (1 = cấu trúc, 2 = xử lý, 3 = tổng kết) - theo <see cref="MultiCheckPhase"/>.</summary>
        public int Phase { get; set; }

        /// <summary>Tên giai đoạn để hiển thị.</summary>
        public string PhaseName { get; set; } = string.Empty;

        /// <summary>Loại đối tượng: parent | hoso | duplicate | summary.</summary>
        public string TargetType { get; set; } = string.Empty;

        /// <summary>Tên hồ sơ (tên thư mục con) mà event liên quan; rỗng nếu là event cấp cha/tổng kết.</summary>
        public string HoSoName { get; set; } = string.Empty;

        /// <summary>Trạng thái: 1 = Success, 0 = Error.</summary>
        public StatusCodeE Status { get; set; }

        /// <summary>Mã: 200 nếu hợp lệ hoặc ErrorCode nếu lỗi.</summary>
        public int Code { get; set; }

        /// <summary>Thông điệp tổng quát của event.</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>Chi tiết các lỗi của 1 hồ sơ (chỉ có khi hồ sơ lỗi).</summary>
        public List<string>? Errors { get; set; }

        /// <summary>Kết quả kiểm tra từng file trong hồ sơ (cho bảng con); chỉ có ở event xử lý 1 hồ sơ.</summary>
        public List<FileCheckResultDto>? Files { get; set; }

        /// <summary>Danh sách tên tất cả hồ sơ con (chỉ có ở event manifest đầu batch) để FE seed sẵn các dòng.</summary>
        public List<string>? HoSoNames { get; set; }

        /// <summary>Số file đính kèm của hồ sơ (khi áp dụng).</summary>
        public int FileCount { get; set; }

        /// <summary>Tổng số hồ sơ con trong thư mục CHA.</summary>
        public int TotalHoSo { get; set; }

        /// <summary>Số hồ sơ đã xử lý xong tính tới event này (dùng tính %).</summary>
        public int CompletedHoSo { get; set; }

        /// <summary>Số hồ sơ đạt (chỉ điền ở event tổng kết).</summary>
        public int OkCount { get; set; }

        /// <summary>Số hồ sơ lỗi (chỉ điền ở event tổng kết).</summary>
        public int ErrorCount { get; set; }

        /// <summary>Phần trăm tiến trình (0-100) = CompletedHoSo / TotalHoSo.</summary>
        public int Percent => TotalHoSo > 0 ? (int)System.Math.Round(CompletedHoSo * 100.0 / TotalHoSo) : 0;

        /// <summary>True nếu đây là event tổng kết cuối cùng.</summary>
        public bool Completed { get; set; }
    }
}
