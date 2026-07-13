using System.Collections.Generic;

namespace Sip.be.Application.Document.Dtos.Pack
{
    /// <summary>
    /// Request đóng gói NHIỀU hồ sơ cùng lúc.
    /// Đầu vào là 1 thư mục CHA chứa nhiều thư mục hồ sơ con (đã qua kiểm tra). BE tự nhận diện cấu trúc (khớp
    /// CheckService): loại 1 = mỗi hồ sơ con có metadata/ (Excel Ho_so/Van_ban) + attachment/; loại 2/3 = file
    /// data nằm TRỰC TIẾP trong hồ sơ con và dùng CHUNG 1 Excel ở thư mục cha (loại 2) hoặc trong parent/metadata
    /// (loại 3). Mỗi hồ sơ được đóng thành 1 file ZIP riêng, đặt trong 1 thư mục cha output do BE tạo.
    /// </summary>
    public class PackMultiFolderRequestDto
    {
        /// <summary>Đường dẫn tuyệt đối tới thư mục CHA chứa các thư mục hồ sơ con.</summary>
        public string ParentFolderPath { get; set; } = string.Empty;

        /// <summary>
        /// Thư mục gốc để ghi kết quả (bắt buộc). BE tạo 1 thư mục con
        /// "{tên thư mục cha}_{timestamp}" bên trong để chứa toàn bộ file ZIP của batch.
        /// </summary>
        public string OutputPath { get; set; } = string.Empty;

        /// <summary>Thư mục chứa các file schema (.xsd) để copy vào schemas/ của mỗi gói. Bỏ trống -> dùng bộ template mặc định.</summary>
        public string? SchemaSourceDir { get; set; }

        /// <summary>Mã phông (IDENTIFICATIONCODE trong agent của METS). Vd "P_KHCN".</summary>
        public string? MaPhong { get; set; }

        /// <summary>Tên người tạo (agent name của METS bản đại diện).</summary>
        public string? NguoiTao { get; set; }

        /// <summary>
        /// Số luồng đóng gói song song tối đa. Bỏ trống -> mặc định theo số nhân CPU.
        /// Là đòn bẩy điều tiết RAM: mỗi hồ sơ chỉ mở 1 workbook + buffer nén zip rồi giải phóng ngay.
        /// </summary>
        public int? MaxDegreeOfParallelism { get; set; }

        /// <summary>
        /// Danh sách tên thư mục hồ sơ được phép đóng gói (allowlist, thường là các hồ sơ đã pass kiểm tra).
        /// Bỏ trống/null -> đóng gói tất cả hồ sơ con.
        /// </summary>
        public List<string>? HoSoNames { get; set; }
    }
}
