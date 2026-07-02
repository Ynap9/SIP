using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Sip.be.Shared.Constants.Documents
{
    /// <summary>
    /// Map tên cột (header) trong Excel/config (tiếng Việt) sang tên phần tử EAD.
    /// Dùng khi đọc sheet Ho_so/Van_ban để tích hợp dữ liệu vào EAD.xml / EAD_doc.xml.
    /// Giá trị mã hoá trong Excel ở dạng "01: Nhãn" -> khi ghi vào EAD chỉ lấy phần mã (xem TachMa).
    /// Nguồn: file H49.61.8.2017.01.xlsx (sheet "Mô tả_Ho_so" / "Mô tả_Van_ban") = nguồn của 2 bảng config.
    /// </summary>
    public static class EadFieldMapping
    {
        /// <summary>Hồ sơ: header sheet "Ho_so" -> phần tử trong EAD.xml (ead.xsd, 18 trường).</summary>
        public static readonly IReadOnlyDictionary<string, string> HoSo =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Mã hồ sơ"] = "fileCode",
                ["Tiêu đề hồ sơ"] = "title",
                ["Thời hạn bảo quản"] = "maintenance",
                ["Chế độ sử dụng"] = "mode",
                ["Ngôn ngữ"] = "language",
                ["Thời gian bắt đầu"] = "startDate",
                ["Thời gian kết thúc"] = "endDate",
                ["Từ khóa"] = "keyword",
                ["Tổng số tài liệu trong hồ sơ"] = "totalDoc",
                ["Số lượng tờ"] = "numberOfPaper",
                ["Số lượng trang"] = "numberOfPage",
                ["Tình trạng vật lý (nếu có)"] = "format",
                ["Ký hiệu thông tin (nếu có)"] = "inforSign",
                ["Mức độ tin cậy"] = "confidenceLevel",
                ["Mã hồ sơ gốc giấy (nếu có)"] = "paperFileCode",
                ["Chế độ dự phòng"] = "riskRecovery",
                ["Tình trạng dự phòng"] = "riskRecoveryStatus",
                ["Ghi chú"] = "description",
            };

        /// <summary>Văn bản: header sheet "Van_ban" -> phần tử trong EAD_doc.xml (ead_doc.xsd, 21 trường).</summary>
        public static readonly IReadOnlyDictionary<string, string> VanBan =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Mã định danh tài liệu"] = "docId",
                ["Mã lưu trữ của tài liệu"] = "docCode",
                ["Thời hạn bảo quản"] = "maintenance",
                ["Tên loại tài liệu"] = "typeName",
                ["Số của tài liệu"] = "codeNumber",
                ["Ký hiệu của tài liệu"] = "codeNotation",
                ["Ngày, tháng, năm tài liệu"] = "issuedDate",
                ["Tên cơ quan, tổ chức, cá nhân ban hành tài liệu"] = "organName",
                ["Trích yếu nội dung"] = "subject",
                ["Ngôn ngữ"] = "language",
                ["Số lượng trang"] = "numberOfPage",
                ["Ký hiệu thông tin"] = "inforSign",
                ["Từ khóa"] = "keyword",
                ["Chế độ sử dụng"] = "mode",
                ["Mức độ tin cậy"] = "confidenceLevel",
                ["Bút tích (nếu có)"] = "autograph",
                ["Tình trạng vật lý (nếu có)"] = "format",
                ["Chế độ dự phòng"] = "riskRecovery",
                ["Tình trạng dự phòng"] = "riskRecoveryStatus",
                ["Ghi chú"] = "description",
                ["Đường dẫn tài liệu Quy trình xử lý (nếu có)"] = "process",
            };

        /// <summary>
        /// Tách phần mã từ giá trị Excel dạng "01: Vĩnh viễn" -> "01".
        /// Nếu không có dấu ":" thì trả nguyên giá trị (đã trim).
        /// </summary>
        public static string TachMa(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var idx = value.IndexOf(':');
            return (idx > 0 ? value.Substring(0, idx) : value).Trim();
        }

        /// <summary>
        /// Chuẩn hoá 1 header/tên trường về dạng canonical để so khớp key ổn định:
        /// NFC (tiền tổ hợp) + đổi non-breaking space (U+00A0) thành space thường + gộp khoảng trắng + trim.
        /// Áp dụng tại MỌI ranh giới nạp header từ nguồn ngoài (Excel/DB) để tránh lệch Unicode (NFC/NFD, NBSP)
        /// khiến lookup ordinal thất bại. Các key trong map này vốn đã NFC nên không đổi.
        /// </summary>
        public static string NormalizeHeader(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var normalized = value.Normalize(NormalizationForm.FormC);
            return Regex.Replace(normalized, @"\s+", " ").Trim();
        }
    }
}
