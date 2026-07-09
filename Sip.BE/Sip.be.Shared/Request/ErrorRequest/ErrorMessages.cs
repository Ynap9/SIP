using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sip.be.Shared.Request.ErrorRequest
{
    public static class ErrorMessages
    {
        private static readonly Dictionary<int, string> _messages = new()
        {
            { ErrorCodes.System, "Lỗi hệ thống" },
            { ErrorCodes.InternalServerError, "Lỗi server" },
            { ErrorCodes.BadRequest, "Request không hợp lệ" },
            { ErrorCodes.NotFound, "Không tìm thấy trong hệ thống" },
            { ErrorCodes.Found, "Đã tồn tại trong hệ thống" },

            { ErrorCodes.FolderNotFound, "Không tìm thấy thư mục hồ sơ" },
            { ErrorCodes.MetadataFolderMissing, "Thiếu thư mục metadata" },
            { ErrorCodes.AttachmentFolderMissing, "Thiếu thư mục attachment" },
            { ErrorCodes.ExcelFileMissing, "Thư mục metadata không chứa file Excel" },
            { ErrorCodes.ExcelFileUnreadable, "Không đọc được nội dung file Excel" },
            { ErrorCodes.AttachmentEmpty, "Thư mục attachment không có file đính kèm" },
            { ErrorCodes.AttachmentInvalidType, "File đính kèm có định dạng không được hỗ trợ" },
            { ErrorCodes.SheetMissing, "File Excel thiếu sheet cần thiết" },
            { ErrorCodes.ColumnMissing, "Sheet thiếu cột cần thiết" },
            { ErrorCodes.HoSoCodeMismatch, "Mã hồ sơ không khớp với tên thư mục" },
            { ErrorCodes.AttachmentExtraFile, "File đính kèm không có mã định danh tài liệu tương ứng" },
            { ErrorCodes.AttachmentMissingForCode, "Mã định danh tài liệu không có file đính kèm tương ứng" },
            { ErrorCodes.ConfigEmpty, "Chưa có cấu hình mô tả trong hệ thống để đối chiếu" },
            { ErrorCodes.RequiredFieldEmpty, "Trường bắt buộc đang để trống" },
            { ErrorCodes.FieldContentInvalid, "Nội dung trường không đúng với quy định trong mô tả" },
            { ErrorCodes.CheckHasErrors, "Quá trình kiểm tra phát hiện lỗi" },

            { ErrorCodes.ParentFolderNotFound, "Không tìm thấy thư mục cha" },
            { ErrorCodes.NoHoSoInParent, "Thư mục cha không chứa hồ sơ con nào" },
            { ErrorCodes.DuplicateHoSoFolder, "Tên thư mục hồ sơ bị trùng lặp" },
            { ErrorCodes.DuplicateFileName, "Tên file bị trùng lặp" },
            { ErrorCodes.DuplicateHoSoCode, "Mã hồ sơ bị trùng lặp giữa các hồ sơ" },

            { ErrorCodes.UnpackSourceNotFound, "Không tìm thấy gói SIP nguồn" },
            { ErrorCodes.UnpackInvalidPackage, "Không nhận diện được gói SIP hợp lệ" },

            { ErrorCodes.TimestampInvalid, "Dấu thời gian/chữ ký số của file không hợp lệ" },
        };

        public static string GetMessage(int code)
        {
            return _messages.TryGetValue(code, out var message) ? message : "Unknown error.";
        }
    }
}
