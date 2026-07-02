using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Sip.be.Api.Base;
using Sip.be.Application.Document.Dtos.Pack;
using Sip.be.Application.Document.Interfaces;
using Sip.be.Shared.Request;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Sip.be.Api.Controllers.Documents
{
    [Route("api/core/document/pack")]
    [ApiController]
    public class PackController: BaseController
    {
        private readonly IPackService _packService;
        public PackController(IPackService packService, ILogger<PackController> logger) : base(logger)
        {
            _packService = packService;
        }

        [HttpPost("ho-so-folder")]
        public async Task<ApiResponse> PackHoSoFolder(PackHoSoFolderRequestDto input)
        {
            try
            {
                var result = await _packService.PackHoSoFolderAsync(input, HttpContext.RequestAborted);
                return new(result);
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        // Đóng gói NHIỀU hồ sơ trong 1 thư mục cha, stream realtime qua SSE. Mỗi hồ sơ đóng xong
        // (hoặc lỗi) đẩy 1 MultiPackProgressDto; event cuối là tổng kết + đường dẫn thư mục cha output.
        [HttpPost("multi-folder")]
        public async Task PackMultiFolder(PackMultiFolderRequestDto input)
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

            await foreach (var progress in _packService.PackMultiFolderAsync(input, HttpContext.RequestAborted))
            {
                var json = JsonSerializer.Serialize(progress, jsonOptions);
                await Response.WriteAsync($"data: {json}\n\n", HttpContext.RequestAborted);
                await Response.Body.FlushAsync(HttpContext.RequestAborted);
            }
        }
    }
}
