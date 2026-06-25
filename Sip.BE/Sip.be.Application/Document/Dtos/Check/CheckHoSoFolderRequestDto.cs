namespace Sip.be.Application.Document.Dtos.Check
{
    /// <summary>
    /// Request kiểm tra 1 thư mục hồ sơ.
    /// 1 thư mục = 1 hồ sơ, bên trong gồm 2 thư mục con: metadata và attachment.
    /// </summary>
    public class CheckHoSoFolderRequestDto
    {
        /// <summary>
        /// Đường dẫn tuyệt đối tới thư mục hồ sơ trên máy (app desktop chạy local).
        /// </summary>
        public string FolderPath { get; set; } = string.Empty;
    }
}
