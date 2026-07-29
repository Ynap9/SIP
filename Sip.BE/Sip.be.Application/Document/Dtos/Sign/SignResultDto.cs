using Sip.be.Application.Document.Dtos.Check;
using Sip.be.External.Documents.Dtos;
using Sip.be.External.Documents.Interfaces;

namespace Sip.be.Application.Document.Dtos.Sign
{
    /// <summary>Kết quả ký của MỘT file.</summary>
    public class SignFileResultDto
    {
        /// <summary>Tên file nguồn.</summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>Đường dẫn tuyệt đối file đã ký (bản mới "{tên gốc}_daky_{...}" cạnh file gốc; rỗng nếu ký hỏng).</summary>
        public string? OutputPath { get; set; }

        /// <summary>Ký thành công hay không.</summary>
        public bool Success { get; set; }

        /// <summary>
        /// File bị BỎ QUA (không ký) vì không đạt gate PDF/A hai lớp — KHÁC lỗi khi ký.
        /// Cả 2 đều có Success=false; FE phân biệt: Skipped=true là bỏ qua, Skipped=false là lỗi ký thật.
        /// </summary>
        public bool Skipped { get; set; }

        /// <summary>Lý do hỏng khi ký, HOẶC lý do bỏ qua khi Skipped=true (rỗng nếu ký thành công).</summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Verdict khi đem chính file vừa ký cho ISignatureVerifier kiểm lại. Đây là vòng tự nghiệm thu:
        /// ký xong mà verifier không nhận thì coi như chưa xong, dù file đã ghi ra đĩa.
        /// </summary>
        public TimestampVerdict Verdict { get; set; }

        /// <summary>Mô tả verdict (nếu verifier không nhận).</summary>
        public string? VerifyReason { get; set; }

        /// <summary>
        /// Thời gian tin cậy lấy từ dấu thời gian TSA (genTime thô), "dd/MM/yyyy HH:mm:ss".
        /// Của chữ ký MỚI NHẤT — file ký chồng lên chữ ký có sẵn thì đây là chữ ký vừa tạo.
        /// </summary>
        public string? TrustedTime { get; set; }

        /// <summary>
        /// Giờ ký tự khai (signingTime) mà BÊN KÝ đặt vào CMS lúc ký — KHÁC giờ TSA (TrustedTime).
        /// Cùng format "dd/MM/yyyy HH:mm:ss" với TrustedTime để FE hiển thị 2 cột đồng nhất.
        /// Cũng là của chữ ký MỚI NHẤT.
        /// </summary>
        public string? SignedTime { get; set; }

        /// <summary>Tổng số chữ ký tìm thấy trong file sau khi ký (file có thể đã có chữ ký từ trước).</summary>
        public int SignatureCount { get; set; }

        /// <summary>Số chữ ký ĐẠT trong file đó.</summary>
        public int ValidSignatureCount { get; set; }

        /// <summary>
        /// Giờ ký + dấu thời gian của TỪNG chữ ký, xếp theo thứ tự ký (mới nhất ở cuối). Có danh sách này thì
        /// file nhiều chữ ký mới xem được đủ, thay vì chỉ thấy một cặp giờ cấp file.
        /// </summary>
        public List<SignatureDetailDto> Signatures { get; set; } = new();
    }

    /// <summary>Tổng kết cả lô ký — gắn vào event summary cuối cùng của SSE.</summary>
    public class SignResultDto
    {
        /// <summary>Thư mục NGUỒN — nơi các file đã ký ("{tên gốc}_daky_{...}") được ghi kèm cạnh file gốc.</summary>
        public string OutputFolder { get; set; } = string.Empty;

        /// <summary>Tổng số file PDF đưa vào ký.</summary>
        public int TotalFiles { get; set; }

        /// <summary>Số file ĐÃ KÝ thành công (signed).</summary>
        public int SuccessCount { get; set; }

        /// <summary>Số file BỎ QUA vì không đạt gate PDF/A hai lớp (skipped) — không ký, không tính là lỗi.</summary>
        public int SkippedCount { get; set; }

        /// <summary>Số file LỖI khi ký (failed) — khác skipped. Tổng SuccessCount+SkippedCount+FailedCount = TotalFiles.</summary>
        public int FailedCount { get; set; }
    }
}
