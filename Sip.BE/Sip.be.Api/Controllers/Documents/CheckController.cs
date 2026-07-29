using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Sip.be.Api.Base;
using Sip.be.Application.Document.Dtos.Check;
using Sip.be.External.Documents.Dtos;
using Sip.be.Application.Document.Interfaces;
using Sip.be.Shared.Request;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Sip.be.Api.Controllers.Documents
{
    [Route("api/core/document/check")]
    [ApiController]
    public class CheckController: BaseController
    {
        private readonly ICheckService _checkService;
        public CheckController(ICheckService checkService, ILogger<CheckController> logger) : base(logger)
        {
            _checkService = checkService;
        }

        // Stream realtime qua SSE (text/event-stream). Mỗi bước check (kể cả từng file) đẩy 1 event
        // CheckProgressDto để FE tick xanh ngay khi có kết quả.
        [HttpPost("ho-so-folder")]
        public async Task CheckHoSoFolder(CheckHoSoFolderRequestDto input)
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

            await foreach (var progress in _checkService.CheckHoSoFolderAsync(input, HttpContext.RequestAborted))
            {
                var json = JsonSerializer.Serialize(progress, jsonOptions);
                await Response.WriteAsync($"data: {json}\n\n", HttpContext.RequestAborted);
                await Response.Body.FlushAsync(HttpContext.RequestAborted);
            }
        }

        // Kiểm tra NHIỀU hồ sơ trong 1 thư mục cha, stream realtime qua SSE. Mỗi hồ sơ xử lý xong
        // (hoặc lỗi cấu trúc/trùng lặp) đẩy 1 MultiCheckProgressDto; event cuối là tổng kết toàn batch.
        [HttpPost("multi-folder")]
        public async Task CheckMultiFolder(CheckMultiFolderRequestDto input)
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

            await foreach (var progress in _checkService.CheckMultiFolderAsync(input, HttpContext.RequestAborted))
            {
                var json = JsonSerializer.Serialize(progress, jsonOptions);
                await Response.WriteAsync($"data: {json}\n\n", HttpContext.RequestAborted);
                await Response.Body.FlushAsync(HttpContext.RequestAborted);
            }
        }

        // Gom các file CHƯA KÝ SỐ của 1 hồ sơ vào thư mục "chưa ký số_{date}" (cùng cấp thư mục hồ sơ) để người dùng xem.
        [HttpPost("collect-unsigned")]
        public async Task<ApiResponse> CollectUnsigned(CollectUnsignedRequestDto input)
        {
            try
            {
                var result = await _checkService.CollectUnsignedFilesAsync(input, HttpContext.RequestAborted);
                return new(result);
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        // Gom file CHƯA KÝ SỐ của NHIỀU hồ sơ vào thư mục "chưa ký số_{date}" (mỗi hồ sơ 1 thư mục con) để người dùng xem.
        [HttpPost("collect-unsigned-multi")]
        public async Task<ApiResponse> CollectUnsignedMulti(CollectUnsignedMultiRequestDto input)
        {
            try
            {
                var result = await _checkService.CollectUnsignedMultiAsync(input, HttpContext.RequestAborted);
                return new(result);
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }
    }
}
