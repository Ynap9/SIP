using Microsoft.AspNetCore.Mvc;
using Sip.be.Api.Base;
using Sip.be.Application.Document.Dtos.Pack;
using Sip.be.Application.Document.Interfaces;
using Sip.be.Shared.Request;

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
    }
}
