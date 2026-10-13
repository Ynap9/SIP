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

        //Các mã lỗi cho luồng kiểm tra NHIỀU hồ sơ (multi)
        public const int ParentFolderNotFound = 1017;
        public const int NoHoSoInParent = 1018;
        public const int DuplicateHoSoFolder = 1019;
        public const int DuplicateFileName = 1020;
        public const int DuplicateHoSoCode = 1021;

        //Các mã lỗi cho luồng dịch ngược (unpack) gói SIP
        public const int UnpackSourceNotFound = 1022;
        public const int UnpackInvalidPackage = 1023;

        //Tên file đính kèm không thuộc hồ sơ (không bắt đầu bằng "{mã hồ sơ}.")
        public const int FileNotBelongToHoSo = 1024;

        //Dấu thời gian/chữ ký số của file PDF không hợp lệ (không có / TSA giả / lệch giờ)
        public const int TimestampInvalid = 1025;

        //Nội dung file PDF đã bị sửa sau khi ký (chữ ký không khớp nội dung / có byte ngoài vùng ký)
        public const int SignatureInvalid = 1026;

        //Các mã lỗi cho luồng KÝ SỐ
        //Không đọc được chứng thư số nào (chưa cắm token / chưa cài middleware / store rỗng)
        public const int CertificateNotFound = 1027;
        //Có chứng thư số nhưng không cái nào ký được (thiếu khóa bí mật / hết hạn / KeyUsage không cho ký)
        public const int CertificateNotSignable = 1028;

        //Không tìm thấy file/thư mục nguồn cần ký
        public const int SignSourceNotFound = 1029;
        //Nguồn không có file PDF nào để ký
        public const int SignNoPdfFound = 1030;
        //Chứng thư số không dùng để ký được (kiểm tra trước khi ký cả lô) -> dừng ngay, không ký file nào
        public const int SignTokenInvalid = 1031;
        //Ký file thất bại
        public const int SignFailed = 1032;

        //PDF được mã hóa (/Encrypt) -> ngoài phạm vi, không ký được mà không giải mã
        public const int SignPdfEncrypted = 1033;
        //PDF có chữ ký chứng thực /DocMDP /P 1 -> tác giả cấm mọi thay đổi sau đó, ký thêm là phá chữ ký cũ
        public const int SignPdfLocked = 1034;
        //Cấu trúc PDF không đọc được để ký nối bản (xref hỏng / dạng chưa hỗ trợ)
        public const int SignPdfStructureUnsupported = 1035;

        //File không đạt gate định dạng trước ký (không phải PDF / không phải PDF/A / không đủ 2 lớp / đọc hỏng)
        public const int PdfFormatInvalid = 1036;

        //Vị trí đặt chữ ký người dùng chọn không dùng được (thiếu tọa độ / sai trang / tràn ra ngoài trang)
        public const int SignPositionInvalid = 1037;

        //Các mã lỗi cho luồng TEMPLATE CẤU HÌNH CHỮ KÝ
        //Không tìm thấy template theo Id (đã bị xoá hoặc Id sai)
        public const int SignTemplateNotFound = 1038;
        //Tên template bị trùng với template khác trên cùng máy
        public const int SignTemplateNameDuplicated = 1039;
        //Ảnh dấu đỏ / chữ ký tươi không dùng được (không tồn tại / sai đuôi / quá lớn)
        public const int SignTemplateImageInvalid = 1040;
        //Thiếu file PDF mẫu đi kèm app (bản cài bị thiếu file, không phải lỗi thao tác của người dùng)
        public const int SignTemplateSampleMissing = 1041;
    }
}
