using System.Collections.Generic;

namespace Sip.be.Application.Document.Dtos.Unpack
{
    /// <summary>Loại nút trong cây thư mục kết quả dịch ngược.</summary>
    public static class FileNodeTypes
    {
        public const string Folder = "folder";
        public const string File = "file";
    }

    /// <summary>
    /// 1 nút trong cây thư mục hồ sơ dựng lại (dạng library): thư mục hoặc file, đều có đường dẫn
    /// tuyệt đối để FE desktop bấm mở trực tiếp. Thư mục có Children; file thì Children = null.
    /// </summary>
    public class FileNodeDto
    {
        /// <summary>Tên thư mục/file (không kèm đường dẫn).</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Đường dẫn tuyệt đối (FE desktop mở trực tiếp được).</summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>Loại nút: folder | file (theo <see cref="FileNodeTypes"/>).</summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>Danh sách con (chỉ có với thư mục; null nếu là file).</summary>
        public List<FileNodeDto>? Children { get; set; }
    }
}
