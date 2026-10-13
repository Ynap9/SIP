using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sip.be.Application.Document.Dtos.SignTemplate;
using Sip.be.Application.Document.Interfaces;
using Sip.be.Domain.Documents;
using Sip.be.infratructure.Persistence;
using Sip.be.Shared.Constants;
using Sip.be.Shared.Constants.Documents;
using Sip.be.Shared.Request.AppException;
using Sip.be.Shared.Request.ErrorRequest;
using traobang.be.shared.HttpRequest.BaseRequest;

namespace Sip.be.Application.Document.Implements
{
    /// <summary>
    /// CRUD template cấu hình chữ ký trên DB RIÊNG của máy (sign-templates.db).
    ///
    /// KHÔNG kế thừa BaseService: BaseService gắn cứng với SipDbContext, mà toàn bộ mục đích của tính năng này
    /// là KHÔNG đụng vào sip.db - sip.db của máy dev bị đóng thành seed rồi phát tán theo bản cài.
    /// </summary>
    public class SignTemplateService : ISignTemplateService
    {
        private readonly SignTemplateDbContext _dbContext;
        private readonly ILogger<SignTemplateService> _logger;
        private readonly IMapper _mapper;

        public SignTemplateService(
            SignTemplateDbContext dbContext,
            ILogger<SignTemplateService> logger,
            IMapper mapper)
        {
            _dbContext = dbContext;
            _logger = logger;
            _mapper = mapper;
        }

        public async Task<ViewSignTemplateDto> CreateAsync(AddSignTemplateDto input)
        {
            _logger.LogInformation("Tạo template chữ ký: {Ten}", input.TenTemplate);

            var ten = (input.TenTemplate ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(ten))
            {
                throw new UserFriendlyException(ErrorCodes.BadRequest, "Tên template không được để trống.");
            }
            if (string.IsNullOrWhiteSpace(input.Thumbprint))
            {
                throw new UserFriendlyException(ErrorCodes.BadRequest, "Chưa chọn chứng thư số cho template.");
            }
            if (await _dbContext.SignTemplate.AnyAsync(x => !x.Deleted && x.TenTemplate == ten))
            {
                throw new UserFriendlyException(ErrorCodes.SignTemplateNameDuplicated,
                    $"Đã có template tên \"{ten}\" trên máy này.");
            }

            var entity = new SignTemplate
            {
                TenTemplate = ten,
                Thumbprint = input.Thumbprint.Trim(),
                TenChungThu = input.TenChungThu,
                LyDoKy = input.LyDoKy,
                NoiKy = input.NoiKy,
                AnhDauDoNguonPath = input.AnhDauDoPath,
                AnhChuKyTuoiNguonPath = input.AnhChuKyTuoiPath,
                FontChuKySo = input.FontChuKySo,
                CoChuKySo = input.CoChuKySo,
                MauChuKySo = input.MauChuKySo,
                CreatedDate = DateTimeConstants.VietnamNow,
                Positions = input.Positions.Select(p => new SignTemplatePosition
                {
                    Kind = p.Kind,
                    PageNumber = Math.Max(p.PageNumber, 1),
                    XRatio = p.XRatio,
                    YRatio = p.YRatio,
                    WidthRatio = p.WidthRatio,
                    HeightRatio = p.HeightRatio,
                }).ToList(),
            };

            _dbContext.SignTemplate.Add(entity);
            await _dbContext.SaveChangesAsync();

            // Copy ảnh SAU khi lưu vì thư mục ảnh đặt tên theo Id, mà Id chỉ có sau khi SaveChanges.
            entity.AnhDauDoPath = PersistImage(input.AnhDauDoPath, entity.Id, "dau-do");
            entity.AnhChuKyTuoiPath = PersistImage(input.AnhChuKyTuoiPath, entity.Id, "chu-ky-tuoi");
            await _dbContext.SaveChangesAsync();

            return await GetByIdAsync(entity.Id);
        }

        public async Task<ViewSignTemplateDto> UpdateAsync(UpdateSignTemplateDto input)
        {
            _logger.LogInformation("Sửa template chữ ký Id={Id}", input.Id);

            var entity = await _dbContext.SignTemplate
                .Include(x => x.Positions)
                .FirstOrDefaultAsync(x => x.Id == input.Id && !x.Deleted)
                ?? throw new UserFriendlyException(ErrorCodes.SignTemplateNotFound,
                    $"Không tìm thấy template Id={input.Id}.");

            var ten = (input.TenTemplate ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(ten))
            {
                throw new UserFriendlyException(ErrorCodes.BadRequest, "Tên template không được để trống.");
            }
            if (await _dbContext.SignTemplate.AnyAsync(x => !x.Deleted && x.Id != entity.Id && x.TenTemplate == ten))
            {
                throw new UserFriendlyException(ErrorCodes.SignTemplateNameDuplicated,
                    $"Đã có template tên \"{ten}\" trên máy này.");
            }

            entity.TenTemplate = ten;
            entity.Thumbprint = (input.Thumbprint ?? string.Empty).Trim();
            entity.TenChungThu = input.TenChungThu;
            entity.LyDoKy = input.LyDoKy;
            entity.NoiKy = input.NoiKy;
            entity.FontChuKySo = input.FontChuKySo;
            entity.CoChuKySo = input.CoChuKySo;
            entity.MauChuKySo = input.MauChuKySo;
            entity.ModifiedDate = DateTimeConstants.VietnamNow;

            // Ảnh: gửi lại đúng đường dẫn đã lưu = giữ nguyên; gửi đường dẫn khác = thay ảnh; gửi null = bỏ ảnh.
            if (!string.Equals(input.AnhDauDoPath, entity.AnhDauDoPath, StringComparison.OrdinalIgnoreCase))
            {
                entity.AnhDauDoPath = PersistImage(input.AnhDauDoPath, entity.Id, "dau-do");
                entity.AnhDauDoNguonPath = input.AnhDauDoPath;
            }
            if (!string.Equals(input.AnhChuKyTuoiPath, entity.AnhChuKyTuoiPath, StringComparison.OrdinalIgnoreCase))
            {
                entity.AnhChuKyTuoiPath = PersistImage(input.AnhChuKyTuoiPath, entity.Id, "chu-ky-tuoi");
                entity.AnhChuKyTuoiNguonPath = input.AnhChuKyTuoiPath;
            }

            // Toạ độ thay hết bằng danh sách mới: màn cấu hình luôn gửi trạng thái đầy đủ, vá từng khối sẽ
            // để sót khối người dùng vừa xoá trên màn hình.
            _dbContext.SignTemplatePosition.RemoveRange(entity.Positions);
            entity.Positions = input.Positions.Select(p => new SignTemplatePosition
            {
                SignTemplateId = entity.Id,
                Kind = p.Kind,
                PageNumber = Math.Max(p.PageNumber, 1),
                XRatio = p.XRatio,
                YRatio = p.YRatio,
                WidthRatio = p.WidthRatio,
                HeightRatio = p.HeightRatio,
            }).ToList();

            await _dbContext.SaveChangesAsync();

            return await GetByIdAsync(entity.Id);
        }

        public async Task DeleteAsync(int id)
        {
            _logger.LogInformation("Xoá template chữ ký Id={Id}", id);

            var entity = await _dbContext.SignTemplate.FirstOrDefaultAsync(x => x.Id == id && !x.Deleted)
                ?? throw new UserFriendlyException(ErrorCodes.SignTemplateNotFound,
                    $"Không tìm thấy template Id={id}.");

            entity.Deleted = true;
            entity.DeletedDate = DateTimeConstants.VietnamNow;
            await _dbContext.SaveChangesAsync();

            // Dọn luôn ảnh: app không có đường phục hồi template đã xoá, giữ lại ảnh chỉ để rác lớn dần.
            var assetDir = Path.Combine(SignTemplateConstants.AssetDirectory, id.ToString());
            try
            {
                if (Directory.Exists(assetDir))
                {
                    Directory.Delete(assetDir, recursive: true);
                }
            }
            catch (Exception ex)
            {
                // Xoá ảnh hỏng không được kéo theo thất bại cả thao tác: bản ghi đã xoá rồi, ảnh chỉ còn là rác.
                _logger.LogWarning(ex, "Không xoá được thư mục ảnh của template Id={Id}: {Dir}", id, assetDir);
            }
        }

        public async Task<ViewSignTemplateDto> GetByIdAsync(int id)
        {
            var entity = await _dbContext.SignTemplate
                .Include(x => x.Positions)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && !x.Deleted)
                ?? throw new UserFriendlyException(ErrorCodes.SignTemplateNotFound,
                    $"Không tìm thấy template Id={id}.");

            return _mapper.Map<ViewSignTemplateDto>(entity);
        }

        public async Task<BaseResponsePagingDto<ViewSignTemplateDto>> FindPagingAsync(BaseRequestPagingDto input)
        {
            var query = _dbContext.SignTemplate
                .Include(x => x.Positions)
                .AsNoTracking()
                .Where(x => !x.Deleted);

            if (!string.IsNullOrWhiteSpace(input.Keyword))
            {
                var keyword = input.Keyword;
                query = query.Where(x => x.TenTemplate.Contains(keyword)
                    || (x.TenChungThu != null && x.TenChungThu.Contains(keyword)));
            }

            // Đếm TRƯỚC khi cắt trang: TotalItems là tổng số bản ghi khớp lọc, không phải số dòng của trang này.
            var totalItems = await query.CountAsync();

            // Template mới tạo lên đầu - người dùng vừa lưu xong là thấy ngay, không phải lật trang đi tìm.
            var items = await query
                .OrderByDescending(x => x.CreatedDate)
                .ThenByDescending(x => x.Id)
                .Paging(input)
                .ToListAsync();

            return new BaseResponsePagingDto<ViewSignTemplateDto>
            {
                Items = _mapper.Map<List<ViewSignTemplateDto>>(items),
                TotalItems = totalItems,
            };
        }

        public SampleFileDto GetSampleFile()
        {
            var path = SignTemplateConstants.SamplePdfPath;
            return new SampleFileDto
            {
                FilePath = path,
                FileName = Path.GetFileName(path),
                Exists = File.Exists(path),
            };
        }

        public (Stream Stream, string ContentType) OpenImageForPreview(string path)
        {
            _logger.LogInformation($"{nameof(OpenImageForPreview)}, path={path}");

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                throw new UserFriendlyException(ErrorCodes.SignTemplateImageInvalid,
                    $"Không tìm thấy file ảnh: {path}");
            }

            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (!SignTemplateConstants.AllowedImageExtensions.Contains(ext))
            {
                throw new UserFriendlyException(ErrorCodes.SignTemplateImageInvalid,
                    $"Định dạng ảnh {ext} không được hỗ trợ.");
            }

            var contentType = ext == ".png" ? "image/png" : "image/jpeg";

            return (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read), contentType);
        }

        /// <summary>
        /// Kiểm tra rồi COPY ảnh người dùng chọn về %AppData%/SipPack/template-assets/{id}, trả về đường dẫn
        /// đã copy. Trả NULL khi <paramref name="sourcePath"/> rỗng (nghĩa là template không dùng ảnh này) -
        /// và khi đó ảnh cũ của đúng loại đó bị xoá đi.
        ///
        /// Vì sao copy chứ không lưu đường dẫn người dùng chọn: file gốc nằm trong thư mục làm việc của họ,
        /// đổi tên hay dọn thư mục một cái là template mất ảnh mà không có dấu hiệu gì.
        /// </summary>
        private string? PersistImage(string? sourcePath, int templateId, string tenFileKhongDuoi)
        {
            var assetDir = Path.Combine(SignTemplateConstants.AssetDirectory, templateId.ToString());

            // Dọn bản cũ của đúng loại ảnh này trước, để đổi từ .png sang .jpg không để lại file mồ côi.
            if (Directory.Exists(assetDir))
            {
                foreach (var old in Directory.GetFiles(assetDir, tenFileKhongDuoi + ".*"))
                {
                    try
                    {
                        File.Delete(old);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Không xoá được ảnh cũ {File}", old);
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return null;
            }

            if (!File.Exists(sourcePath))
            {
                throw new UserFriendlyException(ErrorCodes.SignTemplateImageInvalid,
                    $"Không tìm thấy file ảnh: {sourcePath}");
            }

            var ext = Path.GetExtension(sourcePath).ToLowerInvariant();
            if (!SignTemplateConstants.AllowedImageExtensions.Contains(ext))
            {
                throw new UserFriendlyException(ErrorCodes.SignTemplateImageInvalid,
                    $"Định dạng ảnh {ext} không được hỗ trợ. Chỉ nhận: " +
                    string.Join(", ", SignTemplateConstants.AllowedImageExtensions));
            }

            var length = new FileInfo(sourcePath).Length;
            if (length > SignTemplateConstants.MaxImageBytes)
            {
                throw new UserFriendlyException(ErrorCodes.SignTemplateImageInvalid,
                    $"Ảnh {Path.GetFileName(sourcePath)} nặng {length / 1024 / 1024} MB, vượt mức cho phép " +
                    $"{SignTemplateConstants.MaxImageBytes / 1024 / 1024} MB.");
            }

            Directory.CreateDirectory(assetDir);
            var target = Path.Combine(assetDir, tenFileKhongDuoi + ext);
            File.Copy(sourcePath, target, overwrite: true);
            return target;
        }
    }
}
