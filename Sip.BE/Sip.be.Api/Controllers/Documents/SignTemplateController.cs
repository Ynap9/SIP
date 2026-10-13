using Microsoft.AspNetCore.Mvc;
using Sip.be.Api.Base;
using Sip.be.Application.Document.Dtos.SignTemplate;
using Sip.be.Application.Document.Interfaces;
using Sip.be.Shared.Request;
using traobang.be.shared.HttpRequest.BaseRequest;

namespace Sip.be.Api.Controllers.Documents
{
    /// <summary>
    /// Template cấu hình chữ ký - bộ cấu hình dựng sẵn (chứng thư số, lý do/nơi ký, ảnh dấu đỏ, ảnh chữ ký
    /// tươi, toạ độ từng khối) lưu RIÊNG trên máy đang chạy app.
    /// </summary>
    [Route("api/core/document/template-chu-ky")]
    [ApiController]
    public class SignTemplateController : BaseController
    {
        private readonly ISignTemplateService _signTemplateService;

        public SignTemplateController(
            ISignTemplateService signTemplateService,
            ILogger<SignTemplateController> logger) : base(logger)
        {
            _signTemplateService = signTemplateService;
        }

        /// <summary>Tạo template mới.</summary>
        [HttpPost]
        public async Task<ApiResponse> Create(AddSignTemplateDto dto)
        {
            try
            {
                var result = await _signTemplateService.CreateAsync(dto);
                return new(result);
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        /// <summary>Ghi đè toàn bộ nội dung một template.</summary>
        [HttpPut]
        public async Task<ApiResponse> Update(UpdateSignTemplateDto dto)
        {
            try
            {
                var result = await _signTemplateService.UpdateAsync(dto);
                return new(result);
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        /// <summary>Xoá template và dọn ảnh của nó.</summary>
        [HttpDelete("{id:int}")]
        public async Task<ApiResponse> Delete(int id)
        {
            try
            {
                await _signTemplateService.DeleteAsync(id);
                return new();
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        /// <summary>
        /// Đường dẫn file PDF mẫu đi kèm app. Đặt TRƯỚC route "{id:int}" cho dễ đọc; segment cố định vẫn
        /// thắng route tham số nên không có xung đột.
        /// </summary>
        [HttpGet("file-mau")]
        public ApiResponse GetSampleFile()
        {
            try
            {
                var result = _signTemplateService.GetSampleFile();
                return new(result);
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        [HttpGet("xem-anh")]
        public IActionResult ViewImage([FromQuery] string path)
        {
            try
            {
                var (stream, contentType) = _signTemplateService.OpenImageForPreview(path);
                return File(stream, contentType);
            }
            catch (Exception)
            {
                return NotFound();
            }
        }

        /// <summary>Danh sách template có phân trang. PageSize = -1 để lấy hết.</summary>
        [HttpGet("find-paging")]
        public async Task<ApiResponse> FindPaging([FromQuery] BaseRequestPagingDto input)
        {
            try
            {
                var result = await _signTemplateService.FindPagingAsync(input);
                return new(result);
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        /// <summary>Lấy một template kèm toàn bộ toạ độ.</summary>
        [HttpGet("{id:int}")]
        public async Task<ApiResponse> GetById(int id)
        {
            try
            {
                var result = await _signTemplateService.GetByIdAsync(id);
                return new(result);
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }
    }
}
