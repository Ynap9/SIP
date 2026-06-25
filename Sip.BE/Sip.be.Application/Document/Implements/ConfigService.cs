using AutoMapper;
using AutoMapper.QueryableExtensions;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sip.be.Application.Base;
using Sip.be.Application.Document.Dtos;
using Sip.be.Application.Document.Dtos.Config;
using Sip.be.Application.Document.Interfaces;
using Sip.be.Domain.Document;
using Sip.be.Domain.Documents;
using Sip.be.infratructure.Persistence;
using Sip.be.Shared.Constants.Documents;
using Sip.be.Shared.Request.AppException;
using Sip.be.Shared.Request.ErrorRequest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Sip.be.Application.Document.Implements
{
    public class ConfigService : BaseService, IConfigService
    {
        // Tên sheet cần đọc trong file Excel
        private const string SheetName = "Mô tả_Ho_so";
        private const string SheetNameVanBan = "Mô tả_Van_ban";

        // Tên header của các cột trong sheet (đổi tại đây nếu file Excel dùng tên khác)
        private const string ColTruongThongTin = "Trường thông tin";
        private const string ColMoTa = "Mô tả";
        private const string ColBatBuoc = "Bắt buộc";

        //private readonly SipDbContext _sipDbContext;

        public ConfigService(SipDbContext sipDbContext, ILogger<BaseService> logger, IMapper mapper) : base(sipDbContext, logger, mapper)
        {
            //_sipDbContext = sipDbContext;
        }

        public async Task<int> ImportConfigHoSoAsync(AddConfigHoSoDto input)
        {
            _logger.LogInformation($"{nameof(ImportConfigHoSoAsync)}, fileName = {input?.File?.FileName}");

            if (input?.File == null || input.File.Length == 0)
            {
                throw new UserFriendlyException(ErrorCodes.BadRequest, "File Excel không hợp lệ.");
            }

            using var stream = input.File.OpenReadStream();
            using var workbook = new XLWorkbook(stream);

            if (!workbook.Worksheets.TryGetWorksheet(SheetName, out var worksheet))
            {
                throw new UserFriendlyException(ErrorCodes.NotFound, $"Không tìm thấy sheet '{SheetName}' trong file Excel.");
            }

            // Dòng đầu tiên là header -> map tên header sang số thứ tự cột
            var headerRow = worksheet.FirstRowUsed();
            if (headerRow == null)
            {
                throw new UserFriendlyException(ErrorCodes.BadRequest, $"Sheet '{SheetName}' không có dữ liệu.");
            }

            var columnMap = headerRow.CellsUsed()
                .Where(cell => !string.IsNullOrEmpty(cell.GetString().Trim()))
                .ToDictionary(cell => cell.GetString().Trim(), cell => cell.Address.ColumnNumber, StringComparer.OrdinalIgnoreCase);

            // Cột "Trường thông tin" là bắt buộc
            if (!columnMap.ContainsKey(ColTruongThongTin))
            {
                throw new UserFriendlyException(ErrorCodes.BadRequest, $"Sheet '{SheetName}' thiếu cột '{ColTruongThongTin}'.");
            }

            // Đọc dữ liệu từ các dòng sau header và map sang entity (bỏ qua dòng trống)
            var entities = worksheet.RowsUsed().Skip(1)
                .Select(row => new ConfigHoSo
                {
                    TruongThongTin = columnMap.TryGetValue(ColTruongThongTin, out var colTen) ? row.Cell(colTen).GetString().Trim() : string.Empty,
                    MoTa = columnMap.TryGetValue(ColMoTa, out var colMoTa) ? row.Cell(colMoTa).GetString().Trim() : string.Empty,
                    BatBuoc = (columnMap.TryGetValue(ColBatBuoc, out var colBatBuoc)
                        ? row.Cell(colBatBuoc).GetString().Trim().ToLowerInvariant()
                        : string.Empty) is "1" or "x" or "có" or "co" or "true"
                            ? ConfigConstants.BatBuoc
                            : ConfigConstants.KhongBatBuoc,
                })
                .Where(entity => !string.IsNullOrWhiteSpace(entity.TruongThongTin))
                .ToList();

            if (entities.Count == 0)
            {
                throw new UserFriendlyException(ErrorCodes.BadRequest, "Không có dòng dữ liệu hợp lệ để import.");
            }

            await _sipDbContext.ConfigHoSo.AddRangeAsync(entities);
            await _sipDbContext.SaveChangesAsync();

            return entities.Count;
        }

        public async Task<int> ImportConfigVanBanAsync(AddConfigVanBanDto input)
        {
            _logger.LogInformation($"{nameof(ImportConfigVanBanAsync)}, fileName = {input?.File?.FileName}");

            if (input?.File == null || input.File.Length == 0)
            {
                throw new UserFriendlyException(ErrorCodes.BadRequest, "File Excel không hợp lệ.");
            }

            using var stream = input.File.OpenReadStream();
            using var workbook = new XLWorkbook(stream);

            if (!workbook.Worksheets.TryGetWorksheet(SheetNameVanBan, out var worksheet))
            {
                throw new UserFriendlyException(ErrorCodes.NotFound, $"Không tìm thấy sheet '{SheetNameVanBan}' trong file Excel.");
            }

            // Dòng đầu tiên là header -> map tên header sang số thứ tự cột
            var headerRow = worksheet.FirstRowUsed();
            if (headerRow == null)
            {
                throw new UserFriendlyException(ErrorCodes.BadRequest, $"Sheet '{SheetNameVanBan}' không có dữ liệu.");
            }

            var columnMap = headerRow.CellsUsed()
                .Where(cell => !string.IsNullOrEmpty(cell.GetString().Trim()))
                .ToDictionary(cell => cell.GetString().Trim(), cell => cell.Address.ColumnNumber, StringComparer.OrdinalIgnoreCase);

            // Cột "Trường thông tin" là bắt buộc
            if (!columnMap.ContainsKey(ColTruongThongTin))
            {
                throw new UserFriendlyException(ErrorCodes.BadRequest, $"Sheet '{SheetNameVanBan}' thiếu cột '{ColTruongThongTin}'.");
            }

            // Đọc dữ liệu từ các dòng sau header và map sang entity (bỏ qua dòng trống)
            var entities = worksheet.RowsUsed().Skip(1)
                .Select(row => new ConfigVanBan
                {
                    TruongThongTin = columnMap.TryGetValue(ColTruongThongTin, out var colTen) ? row.Cell(colTen).GetString().Trim() : string.Empty,
                    MoTa = columnMap.TryGetValue(ColMoTa, out var colMoTa) ? row.Cell(colMoTa).GetString().Trim() : string.Empty,
                    BatBuoc = (columnMap.TryGetValue(ColBatBuoc, out var colBatBuoc)
                        ? row.Cell(colBatBuoc).GetString().Trim().ToLowerInvariant()
                        : string.Empty) is "1" or "x" or "có" or "co" or "true"
                            ? ConfigConstants.BatBuoc
                            : ConfigConstants.KhongBatBuoc,
                })
                .Where(entity => !string.IsNullOrWhiteSpace(entity.TruongThongTin))
                .ToList();

            if (entities.Count == 0)
            {
                throw new UserFriendlyException(ErrorCodes.BadRequest, "Không có dòng dữ liệu hợp lệ để import.");
            }

            await _sipDbContext.ConfigVanBan.AddRangeAsync(entities);
            await _sipDbContext.SaveChangesAsync();

            return entities.Count;
        }

        public List<ViewConfigHoSoDto> GetConfigHoSo()
        {
            _logger.LogInformation($"{nameof(GetConfigHoSo)}");
            return _sipDbContext.ConfigHoSo
                .Where(x => !x.Deleted)
                .Select(x => new ViewConfigHoSoDto
                {
                    Id = x.Id,
                    TruongThongTin = x.TruongThongTin,
                    MoTa = x.MoTa,
                    BatBuoc = x.BatBuoc,
                    CreatedDate = x.CreatedDate,
                    ModifiedDate = x.ModifiedDate,
                    DeletedDate = x.DeletedDate,
                    Deleted = x.Deleted
                })
                .ToList();
        }

        public List<ViewConfigVanBanDto> GetConfigVanBan()
        {
            _logger.LogInformation($"{nameof(GetConfigVanBan)}");
            return _sipDbContext.ConfigVanBan
                .Where(x => !x.Deleted)
                .Select(x => new ViewConfigVanBanDto
                {
                    Id = x.Id,
                    TruongThongTin = x.TruongThongTin,
                    MoTa = x.MoTa,
                    BatBuoc = x.BatBuoc,
                    CreatedDate = x.CreatedDate,
                    ModifiedDate = x.ModifiedDate,
                    DeletedDate = x.DeletedDate,
                    Deleted = x.Deleted
                })
                .ToList();
        }
    }
}
