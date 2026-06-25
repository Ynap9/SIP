namespace Sip.be.Application.Document.Dtos.Pack
{
    /// <summary>
    /// Request đóng gói 1 thư mục hồ sơ thành gói SIP_tailieu.
    /// Thư mục đầu vào là thư mục ĐÃ qua kiểm tra (CheckService): bên trong có
    /// metadata/ (chứa file Excel Ho_so/Van_ban) và attachment/ (chứa tài liệu thật).
    /// </summary>
    public class PackHoSoFolderRequestDto
    {
        /// <summary>Đường dẫn tuyệt đối tới thư mục hồ sơ (app desktop chạy local).</summary>
        public string FolderPath { get; set; } = string.Empty;

        /// <summary>
        /// Thư mục gốc để ghi gói SIP ra (gói nằm trong thư mục con tên = OBJID).
        /// Bỏ trống thì dùng thư mục cha của FolderPath.
        /// </summary>
        public string? OutputPath { get; set; }

        /// <summary>Thư mục chứa các file schema (.xsd) để copy vào schemas/ của gói.</summary>
        public string? SchemaSourceDir { get; set; }

        /// <summary>Mã phông (IDENTIFICATIONCODE trong agent của METS). Vd "P_KHCN".</summary>
        public string? MaPhong { get; set; }

        /// <summary>Tên người tạo (agent name của METS bản đại diện).</summary>
        public string? NguoiTao { get; set; }
    }
}
