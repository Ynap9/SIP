using System.Collections.Generic;

namespace Sip.be.Application.Document.Dtos.Check
{
    /// <summary>
    /// Yêu cầu gom các file CHƯA KÝ SỐ của 1 hồ sơ (single) vào 1 thư mục để người dùng xem.
    /// FE gửi sẵn danh sách tên file chưa ký (đã biết từ kết quả check) -> BE chỉ việc copy.
    /// </summary>
    public class CollectUnsignedRequestDto
    {
        /// <summary>Đường dẫn thư mục hồ sơ (đã kiểm tra).</summary>
        public string FolderPath { get; set; } = string.Empty;

        /// <summary>Tên các file chưa ký số cần gom (theo tên file trong bảng kết quả).</summary>
        public List<string> FileNames { get; set; } = new();
    }

    /// <summary>1 hồ sơ con và danh sách file chưa ký số của nó (dùng cho luồng multi).</summary>
    public class CollectUnsignedItemDto
    {
        /// <summary>Tên hồ sơ (tên thư mục con = mã hồ sơ).</summary>
        public string HoSoName { get; set; } = string.Empty;

        /// <summary>Tên các file chưa ký số của hồ sơ này.</summary>
        public List<string> FileNames { get; set; } = new();
    }

    /// <summary>
    /// Yêu cầu gom file chưa ký số của NHIỀU hồ sơ trong 1 thư mục cha.
    /// </summary>
    public class CollectUnsignedMultiRequestDto
    {
        /// <summary>Đường dẫn thư mục CHA (chứa các hồ sơ con).</summary>
        public string ParentFolderPath { get; set; } = string.Empty;

        /// <summary>Danh sách hồ sơ con kèm file chưa ký số của từng hồ sơ.</summary>
        public List<CollectUnsignedItemDto> Items { get; set; } = new();
    }

    /// <summary>
    /// Kết quả gom file chưa ký số: đường dẫn thư mục cha "chưa ký số_{date}" (để FE mở cho người dùng xem).
    /// </summary>
    public class CollectUnsignedResultDto
    {
        /// <summary>Thư mục cha "chưa ký số_{date}" đã tạo (chứa các thư mục con theo mã hồ sơ).</summary>
        public string ParentPath { get; set; } = string.Empty;

        /// <summary>Số hồ sơ có file chưa ký được gom.</summary>
        public int HoSoCount { get; set; }

        /// <summary>Tổng số file chưa ký đã gom.</summary>
        public int FileCount { get; set; }

        /// <summary>Thông điệp kết quả.</summary>
        public string Message { get; set; } = string.Empty;
    }
}
