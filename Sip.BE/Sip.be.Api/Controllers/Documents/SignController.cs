using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Sip.be.Api.Base;
using Sip.be.Application.Document.Dtos.Sign;
using Sip.be.External.Documents.Dtos;
using Sip.be.Application.Document.Interfaces;
using Sip.be.Shared.Request;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Sip.be.Api.Controllers.Documents
{
    [Route("api/core/document/sign")]
    [ApiController]
    public class SignController : BaseController
    {
        private readonly ISignService _signService;
        public SignController(ISignService signService, ILogger<SignController> logger) : base(logger)
        {
            _signService = signService;
        }

        /// <summary>
        /// Liệt kê chứng thư số trên máy: cert cá nhân, cert cấp máy và cert trên USB token.
        /// onlySignable=true -> chỉ trả cert ký được; mặc định trả hết kèm cờ canSign + reason.
        /// </summary>
        [HttpGet("chung-thu-so")]
        public ApiResponse GetCertificates([FromQuery] SignCertQueryDto query)
        {
            try
            {
                var result = _signService.GetCertificates(query);
                return new(result);
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        /// <summary>
        /// Kiểm tra chứng thư số trước khi ký. Đây cũng là lúc hộp thoại nhập PIN của token bật lên;
        /// kết quả + handle khóa được cache cho cả lô ký ngay sau đó.
        /// </summary>
        [HttpPost("kiem-tra-token")]
        public ApiResponse VerifyToken([FromBody] SignRequestDto input)
        {
            try
            {
                var result = _signService.VerifyToken(input.Thumbprint);
                return new(result);
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        /// <summary>
        /// Kiểm tra chữ ký số + dấu thời gian tin cậy của 1 file lẻ, không cần cấu trúc hồ sơ đầy đủ.
        /// Trả nguyên TimestampVerifyDto: verdict cấp file, lý do, TSA, giờ tin cậy và chi tiết TỪNG chữ ký.
        /// </summary>
        [HttpPost("kiem-tra-chu-ky")]
        public ApiResponse VerifySignature([FromBody] SignatureCheckRequestDto input)
        {
            try
            {
                var result = _signService.VerifySignature(input.FilePath);
                return new(result);
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        /// <summary>
        /// Điều hướng xem trước PDF ở bước cấu hình chữ ký: trả file thứ <paramref name="index"/> (đếm từ 0)
        /// trong nguồn kèm cờ HasPrev/HasNext để FE khoá nút trang trước/trang sau.
        /// Bấm quá đầu/cuối danh sách -> Files rỗng, KHÔNG phải lỗi.
        /// </summary>
        [HttpGet("xem-truoc")]
        public ApiResponse GetPreview([FromQuery] string sourcePath, [FromQuery] int index = 0)
        {
            try
            {
                var result = _signService.GetPreviewAt(sourcePath, index);
                return new(result);
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        /// <summary>
        /// Trả NỘI DUNG 1 file PDF cho FE xem trước ở bước cấu hình chữ ký (kéo thả chọn vị trí).
        /// KHÁC mọi endpoint còn lại: trả BYTE THÔ chứ không bọc ApiResponse, vì đầu ra là file cho pdf.js đọc —
        /// bọc JSON vào là trình đọc PDF không parse được. Lỗi vì thế cũng trả HTTP 404 thay vì envelope.
        /// Bấm file trước/file sau: FE tự đi trong danh sách của "danh-sach-file", ở đây chỉ mở đúng 1 file.
        /// </summary>
        [HttpGet("xem-file")]
        public IActionResult ViewPdf([FromQuery] string path)
        {
            try
            {
                var stream = _signService.OpenPdfForPreview(path);
                return File(stream, "application/pdf");
            }
            catch (Exception)
            {
                // Không dùng OkException: endpoint này trả file, không trả envelope.
                return NotFound();
            }
        }

        /// <summary>Liệt kê file PDF trong thư mục/file nguồn để FE hiển thị trước khi ký.</summary>
        [HttpGet("danh-sach-file")]
        public ApiResponse GetFiles([FromQuery] string path)
        {
            try
            {
                var result = _signService.ListPdfFiles(path);
                return new(result);
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        /// <summary>
        /// Kiểm vị trí chữ ký trên mọi file trong nguồn: khối có tràn ra ngoài trang, có nằm ở trang không tồn
        /// tại, có nhỏ quá không. Chỉ BÁO CÁO để người dùng biết trước file nào sẽ bị chỉnh — không chặn ký.
        /// </summary>
        [HttpPost("kiem-tra-vi-tri")]
        public ApiResponse CheckPositions(SignPositionCheckRequestDto input)
        {
            try
            {
                var result = _signService.CheckPositions(input);
                return new(result);
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        // Bước GATE trước khi ký: kiểm định dạng (PDF/A hai lớp) mọi file trong nguồn, stream realtime qua SSE.
        // Mỗi file kiểm xong đẩy 1 event PdfFormatCheckProgressDto (verdict + message) để FE tick dòng; file nào
        // không Valid thì FE chặn không cho sang bước ký-thư-mục. Khung SSE giống hệt SignFolder.
        [HttpPost("kiem-tra-dinh-dang")]
        public async Task CheckPdfFormat(PdfFormatCheckRequestDto input)
        {
            Response.Headers.ContentType = "text/event-stream";
            Response.Headers.CacheControl = "no-cache";
            Response.Headers.Connection = "keep-alive";

            // Tắt buffering để đẩy từng event ngay khi có (realtime)
            HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            await foreach (var progress in _signService.CheckPdfFormatAsync(input, HttpContext.RequestAborted))
            {
                var json = JsonSerializer.Serialize(progress, jsonOptions);
                await Response.WriteAsync($"data: {json}\n\n", HttpContext.RequestAborted);
                await Response.Body.FlushAsync(HttpContext.RequestAborted);
            }
        }

        // Ký cả thư mục, stream realtime qua SSE (text/event-stream). Mỗi file ký xong đẩy 1 event
        // SignProgressDto để FE tick dòng đó và cập nhật thanh tiến trình; event cuối là tổng kết.
        [HttpPost("ky-thu-muc")]
        public async Task SignFolder(SignRequestDto input)
        {
            Response.Headers.ContentType = "text/event-stream";
            Response.Headers.CacheControl = "no-cache";
            Response.Headers.Connection = "keep-alive";

            // Tắt buffering để đẩy từng event ngay khi có (realtime)
            HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            await foreach (var progress in _signService.SignAsync(input, HttpContext.RequestAborted))
            {
                var json = JsonSerializer.Serialize(progress, jsonOptions);
                await Response.WriteAsync($"data: {json}\n\n", HttpContext.RequestAborted);
                await Response.Body.FlushAsync(HttpContext.RequestAborted);
            }
        }
    }
}
