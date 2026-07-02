using Microsoft.AspNetCore.Mvc;
using Sip.be.Api.Base;
using Sip.be.Application.Document.Dtos.Unpack;
using Sip.be.Application.Document.Interfaces;
using Sip.be.Shared.Request;

namespace Sip.be.Api.Controllers.Documents
{
    [Route("api/core/document/unpack")]
    [ApiController]
    public class UnpackController : BaseController
    {
        private readonly IUnpackService _unpackService;
        public UnpackController(IUnpackService unpackService, ILogger<UnpackController> logger) : base(logger)
        {
            _unpackService = unpackService;
        }

        // Dịch ngược 1 gói SIP (file .zip hoặc thư mục gói) về thư mục hồ sơ (metadata/ + attachment/).
        // Trả kết quả kèm cây thư mục (folder hồ sơ -> metadata/attachment -> file) để FE hiển thị dạng library.
        [HttpPost("ho-so")]
        public async Task<ApiResponse> UnpackHoSo(UnpackRequestDto input)
        {
            try
            {
                var result = await _unpackService.UnpackOneAsync(input, HttpContext.RequestAborted);
                return new(result);
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }
    }
}
