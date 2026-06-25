using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sip.be.Shared.Request.ErrorRequest
{
    public static class ErrorCodes
    {
        //Các mã lỗi căn bản
        public const int System = 1;
        public const int BadRequest = 400;
        //public const int Unauthorized = 401;
        public const int NotFound = 404;
        public const int Found = 409;
        public const int InternalServerError = 500;

        //Các mã lỗi cho luồng kiểm tra (check) thư mục hồ sơ
        public const int FolderNotFound = 1001;
        public const int MetadataFolderMissing = 1002;
        public const int AttachmentFolderMissing = 1003;
        public const int ExcelFileMissing = 1004;
        public const int ExcelFileUnreadable = 1005;
        public const int AttachmentEmpty = 1006;
        public const int AttachmentInvalidType = 1007;
        public const int SheetMissing = 1008;
        public const int ColumnMissing = 1009;
        public const int HoSoCodeMismatch = 1010;
        public const int AttachmentExtraFile = 1011;
        public const int AttachmentMissingForCode = 1012;
        public const int ConfigEmpty = 1013;
        public const int RequiredFieldEmpty = 1014;
        public const int FieldContentInvalid = 1015;
        public const int CheckHasErrors = 1016;
    }
}
