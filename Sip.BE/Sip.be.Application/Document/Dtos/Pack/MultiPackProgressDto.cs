using System.Collections.Generic;
using Sip.be.Shared.Request;

namespace Sip.be.Application.Document.Dtos.Pack
{
    /// <summary>
    /// 1 sự kiện tiến trình của luồng đóng gói nhiều hồ sơ (stream qua SSE).
    /// Mỗi event tương ứng 1 HỒ SƠ đóng gói xong (thành công hoặc lỗi); event cuối là tổng kết toàn batch.
    /// </summary>
    public class MultiPackProgressDto
    {
        /// <summary>Loại đối tượng: hoso | summary.</summary>
        public string TargetType { get; set; } = string.Empty;

        /// <summary>Tên hồ sơ (tên thư mục con) mà event liên quan; rỗng nếu là event tổng kết.</summary>
        public string HoSoName { get; set; } = string.Empty;

        /// <summary>Trạng thái: 1 = Success, 0 = Error.</summary>
        public StatusCodeE Status { get; set; }

        /// <summary>Mã: 200 nếu thành công hoặc ErrorCode nếu lỗi.</summary>
        public int Code { get; set; }

        /// <summary>Thông điệp tổng quát của event.</summary>
        public string Message { get; set; } = string.Empty;

        // ----- Thông tin gói của 1 hồ sơ (khi TargetType = hoso, thành công) -----

        /// <summary>OBJID của gói (= tên thư mục gói bên trong zip), dạng uuid-...</summary>
        public string? Objid { get; set; }

        /// <summary>Mã hồ sơ (fileCode) lấy từ sheet Ho_so.</summary>
        public string? FileCode { get; set; }

        /// <summary>Đường dẫn file ZIP của hồ sơ này (FE desktop mở trực tiếp được).</summary>
        public string? ZipPath { get; set; }

        /// <summary>Tổng số tài liệu đã đóng trong gói hồ sơ này.</summary>
        public int TotalDoc { get; set; }

        /// <summary>Cảnh báo phát sinh khi đóng gói hồ sơ này (vd thiếu schema).</summary>
        public List<string>? Warnings { get; set; }

        /// <summary>Chi tiết lỗi khi đóng gói hồ sơ này thất bại.</summary>
        public string? Error { get; set; }

        // ----- Tiến trình / tổng kết toàn batch -----

        /// <summary>Thư mục cha output chứa toàn bộ file ZIP (điền ở event tổng kết; FE mở cả lô).</summary>
        public string? OutputParentPath { get; set; }

        /// <summary>Danh sách tên hồ sơ đóng gói LỖI (điền ở event tổng kết).</summary>
        public List<string>? FailedHoSo { get; set; }

        /// <summary>Tổng số hồ sơ con trong thư mục CHA.</summary>
        public int TotalHoSo { get; set; }

        /// <summary>Số hồ sơ đã đóng gói xong tính tới event này (dùng tính %).</summary>
        public int CompletedHoSo { get; set; }

        /// <summary>Số hồ sơ đóng gói thành công (chỉ điền ở event tổng kết).</summary>
        public int OkCount { get; set; }

        /// <summary>Số hồ sơ đóng gói lỗi (chỉ điền ở event tổng kết).</summary>
        public int ErrorCount { get; set; }

        /// <summary>Phần trăm tiến trình (0-100) = CompletedHoSo / TotalHoSo.</summary>
        public int Percent => TotalHoSo > 0 ? (int)System.Math.Round(CompletedHoSo * 100.0 / TotalHoSo) : 0;

        /// <summary>True nếu đây là event tổng kết cuối cùng.</summary>
        public bool Completed { get; set; }
    }
}
