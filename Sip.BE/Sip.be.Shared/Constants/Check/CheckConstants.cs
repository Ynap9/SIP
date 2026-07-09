using System;
using System.Collections.Generic;

namespace Sip.be.Shared.Constants.Check
{
    /// <summary>
    /// Các hằng số dùng cho luồng kiểm tra (check) thư mục hồ sơ.
    /// </summary>
    public static class CheckConstants
    {
        // Tên 2 thư mục con bắt buộc trong 1 thư mục hồ sơ
        public const string MetadataFolderName = "metadata";
        public const string AttachmentFolderName = "attachment";

        // Tên 2 sheet dữ liệu cần kiểm tra trong file Excel
        public const string SheetHoSo = "Ho_so";
        public const string SheetVanBan = "Van_ban";

        // Cột dùng để KHỚP với tên file đính kèm: tên file = mã định danh tài liệu (docId), vd "H49...160.1".
        public const string ColMaDinhDanhTaiLieu = "Mã định danh tài liệu";

        // Tên cột (header) dùng để đối chiếu mã
        public const string ColMaHoSo = "Mã hồ sơ";
        public const string ColMaLuuTruTaiLieu = "Mã lưu trữ của tài liệu";

        // Loại đối tượng được check (dùng cho FE để biết tick vào item nào)
        public const string TargetTypeFolder = "folder";
        public const string TargetTypeFile = "file";
        public const string TargetTypeSheet = "sheet";
        public const string TargetTypeField = "field";
        public const string TargetTypeCode = "code";
        public const string TargetTypeSummary = "summary";

        // Loại đối tượng cho luồng kiểm tra NHIỀU hồ sơ
        public const string TargetTypeParent = "parent";     // event ở cấp thư mục cha
        public const string TargetTypeHoSo = "hoso";         // event ở cấp 1 hồ sơ
        public const string TargetTypeDuplicate = "duplicate"; // event báo trùng lặp (mã hồ sơ...)
        public const string TargetTypeManifest = "manifest"; // event liệt kê sẵn danh sách hồ sơ con

        // Tên các giai đoạn (stage) để hiển thị
        public const string StageNameFolderStructure = "Kiểm tra cấu trúc thư mục";
        public const string StageNameCodeMatching = "Kiểm tra khớp mã hồ sơ và mã định danh tài liệu";
        public const string StageNameExcelContent = "Kiểm tra nội dung file Excel";

        // Tên các giai đoạn cho luồng kiểm tra NHIỀU hồ sơ
        public const string StageNameStructureValidation = "Kiểm tra cấu trúc thư mục và trùng lặp";
        public const string StageNameProcessing = "Kiểm tra nội dung các hồ sơ";
        public const string StageNameMultiSummary = "Hoàn tất kiểm tra nhiều hồ sơ";

        // Đuôi file Excel hợp lệ trong thư mục metadata
        public static readonly HashSet<string> ExcelExtensions =
            new(StringComparer.OrdinalIgnoreCase) { ".xlsx", ".xls", ".xlsm" };

        // Đuôi file hợp lệ trong thư mục attachment (PDF, hình ảnh, âm thanh, phim...)
        public static readonly HashSet<string> AllowedAttachmentExtensions =
            new(StringComparer.OrdinalIgnoreCase)
            {
                // Tài liệu
                ".pdf",
                // Hình ảnh
                ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tif", ".tiff", ".webp", ".heic",
                // Âm thanh
                ".mp3", ".wav", ".flac", ".aac", ".ogg", ".m4a", ".wma",
                // Phim
                ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".mpeg", ".mpg", ".m4v",
            };
    }
}
