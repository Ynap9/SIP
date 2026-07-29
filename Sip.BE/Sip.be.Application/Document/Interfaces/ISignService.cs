using Sip.be.Application.Document.Dtos.Check;
using Sip.be.External.Documents.Dtos;
using Sip.be.Application.Document.Dtos.Sign;

namespace Sip.be.Application.Document.Interfaces
{
    /// <summary>
    /// Luồng KÝ SỐ tài liệu. Xem .claude/be/plans/pdf-signing-be.plan.md (+ .decisions.md).
    /// </summary>
    public interface ISignService
    {
        /// <summary>
        /// Lấy danh sách chứng thư số dùng được để ký, gồm cert cá nhân (CurrentUser\My), cert cấp máy
        /// (LocalMachine\My) và cert trên USB token — token do middleware (bit4id xPKI/VGCA PKI Manager)
        /// bắc cầu vào store nên đọc chung một đường, không cần PKCS#11.
        /// Mặc định trả HẾT kèm cờ CanSign + Reason để người dùng biết vì sao một cert không chọn được;
        /// <paramref name="query"/>.OnlySignable = true thì chỉ trả cert ký được.
        /// Không đọc được cert nào -> ném UserFriendlyException(ErrorCodes.CertificateNotFound).
        /// </summary>
        List<SignCertDto> GetCertificates(SignCertQueryDto query);

        /// <summary>
        /// Liệt kê các file PDF trong nguồn (1 file lẻ hoặc 1 thư mục) để FE hiển thị trước khi ký.
        /// File không phải PDF bị BỎ QUA, không liệt kê và cũng không tính là lỗi.
        /// </summary>
        SignFileListDto ListPdfFiles(string sourcePath);

        /// <summary>
        /// Điều hướng xem trước: lấy file PDF thứ <paramref name="index"/> (đếm từ 0) trong nguồn, kèm cờ còn
        /// file trước/sau để FE khoá nút. Thứ tự y hệt <see cref="ListPdfFiles"/> nên số thứ tự luôn khớp.
        /// Index ngoài danh sách -> trả Files RỖNG, KHÔNG ném lỗi: bấm quá đầu/cuối là thao tác bình thường
        /// của người dùng, không phải sự cố.
        /// </summary>
        SignPreviewDto GetPreviewAt(string sourcePath, int index);

        /// <summary>
        /// Mở 1 file PDF để FE XEM TRƯỚC ở bước cấu hình chữ ký (kéo thả chọn vị trí trên đúng trang thật).
        /// FE không đọc được ổ đĩa nên phải xin byte qua API; danh sách file để bấm trang trước/trang sau lấy
        /// từ <see cref="ListPdfFiles"/>, endpoint này chỉ lo trả nội dung 1 file.
        /// Trả Stream (không phải byte[]) để file vài chục MB không phải nằm hết trong RAM; ASP.NET tự đóng.
        /// Không phải PDF hoặc không tồn tại -> ném UserFriendlyException(ErrorCodes.SignSourceNotFound).
        /// </summary>
        Stream OpenPdfForPreview(string filePath);

        /// <summary>
        /// Kiểm tra chữ ký số + dấu thời gian tin cậy của MỘT file bất kỳ, không cần cấu trúc hồ sơ.
        /// Dùng để soi nhanh 1 file (kể cả file vừa ký xong) — luồng check hồ sơ đòi đủ thư mục nên không soi lẻ được.
        /// File không tồn tại -> ném UserFriendlyException(ErrorCodes.SignSourceNotFound).
        /// KHÔNG tự chặn file không phải PDF: verifier trả NotApplicable, giữ đúng ngữ nghĩa của luồng check.
        /// </summary>
        TimestampVerifyDto VerifySignature(string filePath);

        /// <summary>
        /// Kiểm tra chứng thư số trước khi ký (FE gọi ngay khi người dùng chọn chứng thư).
        /// Đây cũng là lúc hộp thoại nhập PIN của token bật lên; kết quả + handle khóa được cache cho cả lô.
        /// </summary>
        TokenVerifyDto VerifyToken(string thumbprint);

        /// <summary>
        /// Bước GATE trước khi ký: kiểm ĐỊNH DẠNG mọi file trong nguồn (1 file hoặc 1 thư mục), stream tiến
        /// trình qua SSE. Verdict theo thứ tự dừng-ở-lỗi-đầu: NotPdf -> NotPdfA -> NotTwoLayer -> Valid;
        /// đọc hỏng -> ReadFailed (fail-closed). Chỉ file Valid mới được sang bước ký — FE dùng kết quả này
        /// để chặn ký file không hợp lệ.
        /// KHÁC ListPdfFiles: file không phải PDF KHÔNG bị bỏ qua mà phải báo NotPdf để người dùng biết.
        /// Nguồn không tồn tại -> ném UserFriendlyException(ErrorCodes.SignSourceNotFound).
        /// Trình tự: manifest (seed đủ dòng) -> mỗi file 1 event -> summary (đạt/không đạt).
        /// </summary>
        IAsyncEnumerable<PdfFormatCheckProgressDto> CheckPdfFormatAsync(PdfFormatCheckRequestDto request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Kiểm VỊ TRÍ chữ ký trên mọi file trong nguồn trước khi ký: khối có tràn ra ngoài trang không, trang
        /// người dùng chọn có tồn tại trong file đó không, khối có nhỏ quá không.
        /// Chỉ ĐO rồi báo cáo, KHÔNG chặn và KHÔNG ném lỗi vì một khối lệch: bộ ký sẽ tự dịch/phóng cho vừa
        /// (xem <c>SignService.ToPlacements</c> + <c>ISignaturePlacementResolver</c>), nên đây là bước cho
        /// người dùng BIẾT TRƯỚC file nào sẽ bị chỉnh chứ không phải một cổng chặn thứ hai.
        /// Nguồn không tồn tại -> ném UserFriendlyException(ErrorCodes.SignSourceNotFound).
        /// </summary>
        SignPositionCheckResultDto CheckPositions(SignPositionCheckRequestDto request);

        /// <summary>
        /// Ký toàn bộ PDF trong nguồn, stream tiến trình qua SSE.
        /// Trình tự: kiểm chứng thư TRƯỚC (hỏng -> ném UserFriendlyException, không ký file nào) -> gom file
        /// -> bắn event manifest seed đủ dòng -> ký song song (tầng ký khóa tuần tự, tầng TSA song song);
        /// mỗi file đã ký ghi CẠNH file gốc trong thư mục nguồn tên "{tên gốc}_daky_{yyyyMMdd_HHmmss}{đuôi}"
        /// -> bắn event summary.
        /// Có job giám sát chạy kèm: người dùng rút token giữa chừng thì DỪNG NGAY, các file còn lại báo lỗi,
        /// và cache bị xóa. Lỗi của từng file KHÔNG làm dừng cả lô.
        /// </summary>
        IAsyncEnumerable<SignProgressDto> SignAsync(SignRequestDto request,
            CancellationToken cancellationToken = default);
    }
}
