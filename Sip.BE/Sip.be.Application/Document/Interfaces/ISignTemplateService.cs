using Sip.be.Application.Document.Dtos.SignTemplate;
using traobang.be.shared.HttpRequest.BaseRequest;

namespace Sip.be.Application.Document.Interfaces
{
    /// <summary>
    /// Quản lý TEMPLATE CẤU HÌNH CHỮ KÝ: bộ cấu hình dựng sẵn gồm chứng thư số, lý do/nơi ký, ảnh dấu đỏ,
    /// ảnh chữ ký tươi và toạ độ từng khối, để người dùng không phải kéo thả lại từ đầu mỗi lần ký.
    ///
    /// Tách hẳn khỏi <see cref="ISignService"/>: đây là CRUD trên dữ liệu cục bộ, không đụng gì tới chứng thư,
    /// TSA hay việc ghi file PDF. Service này cũng dùng DB RIÊNG (sign-templates.db) chứ không dùng sip.db.
    /// </summary>
    public interface ISignTemplateService
    {
        /// <summary>Tạo template mới. Ảnh (nếu có) được copy về %AppData% trước khi lưu.</summary>
        Task<ViewSignTemplateDto> CreateAsync(AddSignTemplateDto input);

        /// <summary>
        /// Ghi đè toàn bộ nội dung một template theo payload, kể cả danh sách toạ độ.
        /// Trường ảnh để NULL nghĩa là bỏ ảnh đó khỏi template.
        /// </summary>
        Task<ViewSignTemplateDto> UpdateAsync(UpdateSignTemplateDto input);

        /// <summary>Xoá mềm template và dọn luôn thư mục ảnh của nó.</summary>
        Task DeleteAsync(int id);

        /// <summary>Lấy một template kèm toàn bộ toạ độ.</summary>
        Task<ViewSignTemplateDto> GetByIdAsync(int id);

        /// <summary>
        /// Danh sách template có phân trang, lọc theo <c>Keyword</c> trên tên template và tên chứng thư.
        /// Theo quy ước của <c>PagingExtension</c>: <c>PageSize = -1</c> là lấy hết, không phân trang.
        /// </summary>
        Task<BaseResponsePagingDto<ViewSignTemplateDto>> FindPagingAsync(BaseRequestPagingDto input);

        /// <summary>Đường dẫn file PDF mẫu đi kèm app để FE xem trước khi đặt vị trí.</summary>
        SampleFileDto GetSampleFile();

        (Stream Stream, string ContentType) OpenImageForPreview(string path);
    }
}
