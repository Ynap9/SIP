namespace Sip.be.Application.Document.Dtos.Sign
{
    /// <summary>1 file PDF sẽ được ký, dùng để FE liệt kê trước khi bấm ký.</summary>
    public class SignFileItemDto
    {
        /// <summary>Tên file (kèm đuôi).</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Đường dẫn tuyệt đối.</summary>
        public string FullPath { get; set; } = string.Empty;

        /// <summary>Kích thước (byte).</summary>
        public long SizeBytes { get; set; }

        /// <summary>
        /// File đã có chữ ký số sẵn hay chưa. Ký thêm là hợp lệ — SIPPACK ký theo kiểu nối bản nên chữ ký cũ
        /// vẫn nguyên vẹn (kiểm thực trên 14 file mẫu); cờ này chỉ để FE cảnh báo cho người dùng biết.
        /// </summary>
        public bool AlreadySigned { get; set; }
    }

    /// <summary>Danh sách file PDF trong nguồn cần ký.</summary>
    public class SignFileListDto
    {
        /// <summary>Đường dẫn nguồn đã chuẩn hóa.</summary>
        public string SourcePath { get; set; } = string.Empty;

        /// <summary>Nguồn là thư mục (true) hay 1 file lẻ (false).</summary>
        public bool IsFolder { get; set; }

        /// <summary>Các file PDF tìm được. File không phải PDF bị BỎ QUA, không liệt kê.</summary>
        public List<SignFileItemDto> Files { get; set; } = new();

        /// <summary>Tổng số file PDF.</summary>
        public int Total => Files.Count;
    }
}
