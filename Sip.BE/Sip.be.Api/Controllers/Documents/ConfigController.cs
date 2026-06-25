using Microsoft.AspNetCore.Mvc;
using Sip.be.Api.Base;
using Sip.be.Application.Document.Dtos;
using Sip.be.Application.Document.Dtos.Config;
using Sip.be.Application.Document.Interfaces;
using Sip.be.Shared.Request;

namespace Sip.be.Api.Controllers.Documents
{
    [Route("api/core/document/config")]
    [ApiController]
    public class ConfigController : BaseController
    {
        private readonly IConfigService _configService;
        public ConfigController(IConfigService configService, ILogger<ConfigController> logger) : base(logger)
        {
            _configService = configService;
        }
        [HttpPost("ho-so")]
        public async Task<ApiResponse> ImportConfigHoSo(AddConfigHoSoDto dto)
        {
            try
            {
                await _configService.ImportConfigHoSoAsync(dto);
                return new();
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }
        [HttpPost("van-ban")]
        public async Task<ApiResponse> ImportConfigVan(AddConfigVanBanDto dto)
        {
            try
            {
                await _configService.ImportConfigVanBanAsync(dto);
                return new();
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        [HttpGet("ho-so")]
        public ApiResponse GetConfigHoSo()
        {
            try
            {
                var result =  _configService.GetConfigHoSo();
                return new(result);
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        [HttpGet("van-ban")]
        public ApiResponse GetConfigVanBan()
        {
            try
            {
                var result = _configService.GetConfigVanBan();
                return new(result);
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }
    }
}

    



