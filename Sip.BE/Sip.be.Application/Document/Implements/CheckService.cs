using AutoMapper;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sip.be.Application.Base;
using Sip.be.Application.Document.Dtos.Check;
using Sip.be.Application.Document.Interfaces;
using Sip.be.Domain.Document;
using Sip.be.Domain.Documents;
using Sip.be.infratructure.Persistence;
using Sip.be.Shared.Constants.Check;
using Sip.be.Shared.Constants.Documents;
using Sip.be.Shared.Request;
using Sip.be.Shared.Request.ErrorRequest;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Sip.be.Application.Document.Implements
{
    public class CheckService : BaseService, ICheckService
    {
        // Pattern nhận diện 1 dòng "mã: nhãn" trong mô tả (vd: "22: Công văn")
        private const string OptionPattern = @"^([A-Za-z0-9]{1,4})\s*[:：]\s*(.+)$";

        public CheckService(SipDbContext sipDbContext, ILogger<BaseService> logger, IMapper mapper) : base(sipDbContext, logger, mapper)
        {
        }

        // Wrapper: ước lượng tổng số bước rồi đóng dấu Current/Total (=> Percent) lên từng event để FE vẽ thanh tiến trình realtime.
        public async IAsyncEnumerable<CheckProgressDto> CheckHoSoFolderAsync(CheckHoSoFolderRequestDto input, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var total = 1;
            var folderPath = input?.FolderPath?.Trim();
            if (!string.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath))
            {
                var metadataDir = Directory.GetDirectories(folderPath)
                    .FirstOrDefault(d => string.Equals(new DirectoryInfo(d).Name, CheckConstants.MetadataFolderName, StringComparison.OrdinalIgnoreCase));
                var attachmentDir = Directory.GetDirectories(folderPath)
                    .FirstOrDefault(d => string.Equals(new DirectoryInfo(d).Name, CheckConstants.AttachmentFolderName, StringComparison.OrdinalIgnoreCase));
                var excelPath = metadataDir == null
                    ? null
                    : Directory.GetFiles(metadataDir).FirstOrDefault(f => CheckConstants.ExcelExtensions.Contains(Path.GetExtension(f)));

                if (attachmentDir != null && excelPath != null)
                {
                    var attachmentCount = Directory.GetFiles(attachmentDir).Length;
                    var vanBanRows = 0;
                    try
                    {
                        using var wb = new XLWorkbook(excelPath);
                        if (wb.Worksheets.TryGetWorksheet(CheckConstants.SheetVanBan, out var vanBanSheet))
                        {
                            vanBanRows = vanBanSheet.RowsUsed().Skip(1).Count();
                        }
                    }
                    catch { /* lỗi đọc excel sẽ được StreamEventsAsync báo, total để mặc định */ }

                    var configHoSo = await _sipDbContext.ConfigHoSo.Where(x => !x.Deleted).CountAsync(cancellationToken);
                    var configVanBan = await _sipDbContext.ConfigVanBan.Where(x => !x.Deleted).CountAsync(cancellationToken);

                    // structure(5) + attachment ticks(2 lượt) + maHoSo(1) + nội dung(hoSo + vanBan*dòng) + summaries(3) + done(1)
                    total = 5 + attachmentCount * 2 + 1 + configHoSo + configVanBan * vanBanRows + 3 + 1;
                }
            }

            var current = 0;
            await foreach (var ev in StreamEventsAsync(input, cancellationToken))
            {
                current++;
                if (ev.Completed) total = current;                 // chốt đúng 100% ở event cuối
                ev.Current = current;
                ev.Total = total < current ? current : total;      // tránh vượt 100%
                yield return ev;
            }
        }

        private async IAsyncEnumerable<CheckProgressDto> StreamEventsAsync(CheckHoSoFolderRequestDto input, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _logger.LogInformation($"{nameof(CheckHoSoFolderAsync)}, folderPath = {input?.FolderPath}");

            var folderPath = input?.FolderPath?.Trim();
            var errorCount = 0;

            // ========================= TIỀN ĐIỀU KIỆN =========================
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                yield return new CheckProgressDto
                {
                    Stage = (int)CheckStage.FolderStructure,
                    StageName = CheckConstants.StageNameFolderStructure,
                    TargetType = CheckConstants.TargetTypeFolder,
                    Target = folderPath ?? string.Empty,
                    Status = StatusCodeE.Error,
                    Code = ErrorCodes.FolderNotFound,
                    Message = $"Không tìm thấy thư mục hồ sơ: '{folderPath}'.",
                    Completed = true
                };
                yield break;
            }

            var hoSoFolderName = new DirectoryInfo(folderPath).Name;

            // ===================== GIAI ĐOẠN 1: CẤU TRÚC ======================

            // 1.1 Thư mục metadata
            var metadataDir = Directory.GetDirectories(folderPath)
                .FirstOrDefault(d => string.Equals(new DirectoryInfo(d).Name, CheckConstants.MetadataFolderName, StringComparison.OrdinalIgnoreCase));
            if (metadataDir == null)
            {
                yield return new CheckProgressDto
                {
                    Stage = (int)CheckStage.FolderStructure,
                    StageName = CheckConstants.StageNameFolderStructure,
                    TargetType = CheckConstants.TargetTypeFolder,
                    Target = CheckConstants.MetadataFolderName,
                    Status = StatusCodeE.Error,
                    Code = ErrorCodes.MetadataFolderMissing,
                    Message = $"Thiếu thư mục '{CheckConstants.MetadataFolderName}' trong hồ sơ.",
                    Completed = true
                };
                yield break;
            }
            yield return new CheckProgressDto
            {
                Stage = (int)CheckStage.FolderStructure,
                StageName = CheckConstants.StageNameFolderStructure,
                TargetType = CheckConstants.TargetTypeFolder,
                Target = CheckConstants.MetadataFolderName,
                Status = StatusCodeE.Success,
                Code = 200,
                Message = "Thư mục metadata hợp lệ."
            };

            // 1.2 Thư mục attachment
            var attachmentDir = Directory.GetDirectories(folderPath)
                .FirstOrDefault(d => string.Equals(new DirectoryInfo(d).Name, CheckConstants.AttachmentFolderName, StringComparison.OrdinalIgnoreCase));
            if (attachmentDir == null)
            {
                yield return new CheckProgressDto
                {
                    Stage = (int)CheckStage.FolderStructure,
                    StageName = CheckConstants.StageNameFolderStructure,
                    TargetType = CheckConstants.TargetTypeFolder,
                    Target = CheckConstants.AttachmentFolderName,
                    Status = StatusCodeE.Error,
                    Code = ErrorCodes.AttachmentFolderMissing,
                    Message = $"Thiếu thư mục '{CheckConstants.AttachmentFolderName}' trong hồ sơ.",
                    Completed = true
                };
                yield break;
            }
            yield return new CheckProgressDto
            {
                Stage = (int)CheckStage.FolderStructure,
                StageName = CheckConstants.StageNameFolderStructure,
                TargetType = CheckConstants.TargetTypeFolder,
                Target = CheckConstants.AttachmentFolderName,
                Status = StatusCodeE.Success,
                Code = 200,
                Message = "Thư mục attachment hợp lệ."
            };

            // Manifest: load tên file đính kèm để FE hiển thị bảng TRƯỚC khi gọi quá trình xử lý khác
            yield return new CheckProgressDto
            {
                Stage = (int)CheckStage.FolderStructure,
                StageName = CheckConstants.StageNameFolderStructure,
                TargetType = "manifest",
                Files = Directory.GetFiles(attachmentDir).Select(Path.GetFileName).ToList(),
                Status = StatusCodeE.Success,
                Code = 200
            };

            // 1.3 File Excel trong metadata
            var excelPath = Directory.GetFiles(metadataDir)
                .FirstOrDefault(f => CheckConstants.ExcelExtensions.Contains(Path.GetExtension(f)));
            if (excelPath == null)
            {
                yield return new CheckProgressDto
                {
                    Stage = (int)CheckStage.FolderStructure,
                    StageName = CheckConstants.StageNameFolderStructure,
                    TargetType = CheckConstants.TargetTypeFile,
                    Target = CheckConstants.MetadataFolderName,
                    Status = StatusCodeE.Error,
                    Code = ErrorCodes.ExcelFileMissing,
                    Message = $"Thư mục '{CheckConstants.MetadataFolderName}' không chứa file Excel.",
                    Completed = true
                };
                yield break;
            }

            // Mở workbook (try/catch không chứa yield)
            XLWorkbook workbook = null;
            var openError = false;
            try
            {
                workbook = new XLWorkbook(excelPath);
            }
            catch
            {
                openError = true;
            }
            if (openError || workbook == null)
            {
                yield return new CheckProgressDto
                {
                    Stage = (int)CheckStage.FolderStructure,
                    StageName = CheckConstants.StageNameFolderStructure,
                    TargetType = CheckConstants.TargetTypeFile,
                    Target = Path.GetFileName(excelPath),
                    Status = StatusCodeE.Error,
                    Code = ErrorCodes.ExcelFileUnreadable,
                    Message = $"Không đọc được nội dung file Excel '{Path.GetFileName(excelPath)}'.",
                    Completed = true
                };
                yield break;
            }

            using (workbook)
            {
                yield return new CheckProgressDto
                {
                    Stage = (int)CheckStage.FolderStructure,
                    StageName = CheckConstants.StageNameFolderStructure,
                    TargetType = CheckConstants.TargetTypeFile,
                    Target = Path.GetFileName(excelPath),
                    Status = StatusCodeE.Success,
                    Code = 200,
                    Message = "Đọc được nội dung file Excel."
                };

                // 1.4 File đính kèm trong attachment
                var attachmentFiles = Directory.GetFiles(attachmentDir);
                if (attachmentFiles.Length == 0)
                {
                    yield return new CheckProgressDto
                    {
                        Stage = (int)CheckStage.FolderStructure,
                        StageName = CheckConstants.StageNameFolderStructure,
                        TargetType = CheckConstants.TargetTypeFolder,
                        Target = CheckConstants.AttachmentFolderName,
                        Status = StatusCodeE.Error,
                        Code = ErrorCodes.AttachmentEmpty,
                        Message = $"Thư mục '{CheckConstants.AttachmentFolderName}' không có file đính kèm nào.",
                        Completed = true
                    };
                    yield break;
                }

                foreach (var file in attachmentFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var fileName = Path.GetFileName(file);
                    if (!CheckConstants.AllowedAttachmentExtensions.Contains(Path.GetExtension(file)))
                    {
                        errorCount++;
                        yield return new CheckProgressDto
                        {
                            Stage = (int)CheckStage.FolderStructure,
                            StageName = CheckConstants.StageNameFolderStructure,
                            TargetType = CheckConstants.TargetTypeFile,
                            Target = fileName,
                            Status = StatusCodeE.Error,
                            Code = ErrorCodes.AttachmentInvalidType,
                            Message = $"File '{fileName}' có định dạng không được hỗ trợ (chỉ chấp nhận PDF, hình ảnh, âm thanh, phim)."
                        };
                    }
                    else
                    {
                        yield return new CheckProgressDto
                        {
                            Stage = (int)CheckStage.FolderStructure,
                            StageName = CheckConstants.StageNameFolderStructure,
                            TargetType = CheckConstants.TargetTypeFile,
                            Target = fileName,
                            Status = StatusCodeE.Success,
                            Code = 200,
                            Message = "Định dạng file đính kèm hợp lệ."
                        };
                    }
                }

                // 1.5 Đủ 2 sheet Ho_so và Van_ban
                if (!workbook.Worksheets.TryGetWorksheet(CheckConstants.SheetHoSo, out var hoSoSheet))
                {
                    yield return new CheckProgressDto
                    {
                        Stage = (int)CheckStage.FolderStructure,
                        StageName = CheckConstants.StageNameFolderStructure,
                        TargetType = CheckConstants.TargetTypeSheet,
                        Target = CheckConstants.SheetHoSo,
                        Status = StatusCodeE.Error,
                        Code = ErrorCodes.SheetMissing,
                        Message = $"File Excel thiếu sheet '{CheckConstants.SheetHoSo}'.",
                        Completed = true
                    };
                    yield break;
                }
                yield return new CheckProgressDto
                {
                    Stage = (int)CheckStage.FolderStructure,
                    StageName = CheckConstants.StageNameFolderStructure,
                    TargetType = CheckConstants.TargetTypeSheet,
                    Target = CheckConstants.SheetHoSo,
                    Status = StatusCodeE.Success,
                    Code = 200,
                    Message = $"Tồn tại sheet '{CheckConstants.SheetHoSo}'."
                };

                if (!workbook.Worksheets.TryGetWorksheet(CheckConstants.SheetVanBan, out var vanBanSheet))
                {
                    yield return new CheckProgressDto
                    {
                        Stage = (int)CheckStage.FolderStructure,
                        StageName = CheckConstants.StageNameFolderStructure,
                        TargetType = CheckConstants.TargetTypeSheet,
                        Target = CheckConstants.SheetVanBan,
                        Status = StatusCodeE.Error,
                        Code = ErrorCodes.SheetMissing,
                        Message = $"File Excel thiếu sheet '{CheckConstants.SheetVanBan}'.",
                        Completed = true
                    };
                    yield break;
                }
                yield return new CheckProgressDto
                {
                    Stage = (int)CheckStage.FolderStructure,
                    StageName = CheckConstants.StageNameFolderStructure,
                    TargetType = CheckConstants.TargetTypeSheet,
                    Target = CheckConstants.SheetVanBan,
                    Status = StatusCodeE.Success,
                    Code = 200,
                    Message = $"Tồn tại sheet '{CheckConstants.SheetVanBan}'."
                };

                // Tổng kết giai đoạn 1
                yield return new CheckProgressDto
                {
                    Stage = (int)CheckStage.FolderStructure,
                    StageName = CheckConstants.StageNameFolderStructure,
                    TargetType = CheckConstants.TargetTypeSummary,
                    Target = CheckConstants.StageNameFolderStructure,
                    Status = errorCount == 0 ? StatusCodeE.Success : StatusCodeE.Error,
                    Code = errorCount == 0 ? 200 : ErrorCodes.CheckHasErrors,
                    Message = errorCount == 0 ? "Cấu trúc thư mục chính xác." : "Cấu trúc thư mục có lỗi, xem chi tiết các file."
                };

                // Map cột theo header (header có thể null nếu sheet trống)
                var hoSoColumns = hoSoSheet.FirstRowUsed() is { } hoSoHeader
                    ? hoSoHeader.CellsUsed()
                        .Where(c => !string.IsNullOrWhiteSpace(c.GetString()))
                        .GroupBy(c => EadFieldMapping.NormalizeHeader(c.GetString()), StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(g => g.Key, g => g.First().Address.ColumnNumber, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                var vanBanColumns = vanBanSheet.FirstRowUsed() is { } vanBanHeader
                    ? vanBanHeader.CellsUsed()
                        .Where(c => !string.IsNullOrWhiteSpace(c.GetString()))
                        .GroupBy(c => EadFieldMapping.NormalizeHeader(c.GetString()), StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(g => g.Key, g => g.First().Address.ColumnNumber, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                var hoSoDataRow = hoSoSheet.RowsUsed().Skip(1).FirstOrDefault();
                var vanBanDataRows = vanBanSheet.RowsUsed().Skip(1).ToList();

                // ==================== GIAI ĐOẠN 2: KHỚP MÃ ====================

                // 2.1 Mã hồ sơ trong sheet Ho_so khớp với tên thư mục
                if (hoSoDataRow == null || !hoSoColumns.TryGetValue(EadFieldMapping.NormalizeHeader(CheckConstants.ColMaHoSo), out var maHoSoCol))
                {
                    errorCount++;
                    yield return new CheckProgressDto
                    {
                        Stage = (int)CheckStage.CodeMatching,
                        StageName = CheckConstants.StageNameCodeMatching,
                        TargetType = CheckConstants.TargetTypeCode,
                        Target = CheckConstants.ColMaHoSo,
                        Status = StatusCodeE.Error,
                        Code = ErrorCodes.ColumnMissing,
                        Message = $"Sheet '{CheckConstants.SheetHoSo}' thiếu cột '{CheckConstants.ColMaHoSo}' hoặc không có dữ liệu."
                    };
                }
                else
                {
                    var maHoSo = hoSoDataRow.Cell(maHoSoCol).GetString().Trim();
                    if (!string.Equals(maHoSo, hoSoFolderName, StringComparison.OrdinalIgnoreCase))
                    {
                        errorCount++;
                        yield return new CheckProgressDto
                        {
                            Stage = (int)CheckStage.CodeMatching,
                            StageName = CheckConstants.StageNameCodeMatching,
                            TargetType = CheckConstants.TargetTypeCode,
                            Target = maHoSo,
                            Status = StatusCodeE.Error,
                            Code = ErrorCodes.HoSoCodeMismatch,
                            Message = $"Mã hồ sơ trong file ('{maHoSo}') không khớp với tên thư mục ('{hoSoFolderName}')."
                        };
                    }
                    else
                    {
                        yield return new CheckProgressDto
                        {
                            Stage = (int)CheckStage.CodeMatching,
                            StageName = CheckConstants.StageNameCodeMatching,
                            TargetType = CheckConstants.TargetTypeCode,
                            Target = maHoSo,
                            Status = StatusCodeE.Success,
                            Code = 200,
                            Message = $"Mã hồ sơ khớp với tên thư mục ('{hoSoFolderName}')."
                        };
                    }
                }

                // 2.2 Mã định danh tài liệu khớp với tên file đính kèm (không thừa, không thiếu)
                if (!vanBanColumns.TryGetValue(EadFieldMapping.NormalizeHeader(CheckConstants.ColMaLuuTruTaiLieu), out var maTaiLieuCol))
                {
                    errorCount++;
                    yield return new CheckProgressDto
                    {
                        Stage = (int)CheckStage.CodeMatching,
                        StageName = CheckConstants.StageNameCodeMatching,
                        TargetType = CheckConstants.TargetTypeCode,
                        Target = CheckConstants.ColMaLuuTruTaiLieu,
                        Status = StatusCodeE.Error,
                        Code = ErrorCodes.ColumnMissing,
                        Message = $"Sheet '{CheckConstants.SheetVanBan}' thiếu cột '{CheckConstants.ColMaLuuTruTaiLieu}'."
                    };
                }
                else
                {
                    var maTaiLieuSet = new HashSet<string>(
                        vanBanDataRows
                            .Select(r => r.Cell(maTaiLieuCol).GetString().Trim())
                            .Where(v => !string.IsNullOrWhiteSpace(v)),
                        StringComparer.OrdinalIgnoreCase);

                    var fileNameSet = new HashSet<string>(
                        attachmentFiles.Select(f => Path.GetFileNameWithoutExtension(f)),
                        StringComparer.OrdinalIgnoreCase);

                    // Mỗi file đính kèm phải có mã định danh tương ứng (phát hiện file thừa)
                    foreach (var file in attachmentFiles)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var fileName = Path.GetFileName(file);
                        var nameNoExt = Path.GetFileNameWithoutExtension(file);
                        if (maTaiLieuSet.Contains(nameNoExt))
                        {
                            yield return new CheckProgressDto
                            {
                                Stage = (int)CheckStage.CodeMatching,
                                StageName = CheckConstants.StageNameCodeMatching,
                                TargetType = CheckConstants.TargetTypeFile,
                                Target = fileName,
                                Status = StatusCodeE.Success,
                                Code = 200,
                                Message = "File đính kèm khớp với mã định danh tài liệu."
                            };
                        }
                        else
                        {
                            errorCount++;
                            yield return new CheckProgressDto
                            {
                                Stage = (int)CheckStage.CodeMatching,
                                StageName = CheckConstants.StageNameCodeMatching,
                                TargetType = CheckConstants.TargetTypeFile,
                                Target = fileName,
                                Status = StatusCodeE.Error,
                                Code = ErrorCodes.AttachmentExtraFile,
                                Message = $"File '{fileName}' không có mã định danh tài liệu tương ứng trong sheet '{CheckConstants.SheetVanBan}'."
                            };
                        }
                    }

                    // Mỗi mã định danh phải có file (phát hiện file thiếu)
                    foreach (var ma in maTaiLieuSet)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!fileNameSet.Contains(ma))
                        {
                            errorCount++;
                            yield return new CheckProgressDto
                            {
                                Stage = (int)CheckStage.CodeMatching,
                                StageName = CheckConstants.StageNameCodeMatching,
                                TargetType = CheckConstants.TargetTypeCode,
                                Target = ma,
                                Status = StatusCodeE.Error,
                                Code = ErrorCodes.AttachmentMissingForCode,
                                Message = $"Mã định danh tài liệu '{ma}' không có file đính kèm tương ứng trong thư mục attachment."
                            };
                        }
                    }
                }

                // Tổng kết giai đoạn 2
                yield return new CheckProgressDto
                {
                    Stage = (int)CheckStage.CodeMatching,
                    StageName = CheckConstants.StageNameCodeMatching,
                    TargetType = CheckConstants.TargetTypeSummary,
                    Target = CheckConstants.StageNameCodeMatching,
                    Status = errorCount == 0 ? StatusCodeE.Success : StatusCodeE.Error,
                    Code = errorCount == 0 ? 200 : ErrorCodes.CheckHasErrors,
                    Message = errorCount == 0 ? "Mã hồ sơ và mã định danh tài liệu khớp chính xác." : "Có sai lệch về mã hồ sơ / mã định danh tài liệu, xem chi tiết."
                };

                // ================== GIAI ĐOẠN 3: NỘI DUNG EXCEL ==================

                // Mô tả chuẩn lấy từ 2 bảng config trong DB (chưa bị xoá mềm)
                var configHoSo = await _sipDbContext.ConfigHoSo.Where(x => !x.Deleted).ToListAsync(cancellationToken);
                var configVanBan = await _sipDbContext.ConfigVanBan.Where(x => !x.Deleted).ToListAsync(cancellationToken);

                // 3.1 Sheet Ho_so (1 hồ sơ = 1 dòng dữ liệu)
                if (configHoSo.Count == 0)
                {
                    yield return new CheckProgressDto
                    {
                        Stage = (int)CheckStage.ExcelContent,
                        StageName = CheckConstants.StageNameExcelContent,
                        TargetType = CheckConstants.TargetTypeSheet,
                        Target = CheckConstants.SheetHoSo,
                        Status = StatusCodeE.Error,
                        Code = ErrorCodes.ConfigEmpty,
                        Message = $"Chưa có cấu hình mô tả cho '{CheckConstants.SheetHoSo}' trong hệ thống để đối chiếu."
                    };
                }
                else if (hoSoDataRow == null)
                {
                    errorCount++;
                    yield return new CheckProgressDto
                    {
                        Stage = (int)CheckStage.ExcelContent,
                        StageName = CheckConstants.StageNameExcelContent,
                        TargetType = CheckConstants.TargetTypeSheet,
                        Target = CheckConstants.SheetHoSo,
                        Status = StatusCodeE.Error,
                        Code = ErrorCodes.RequiredFieldEmpty,
                        Message = $"Sheet '{CheckConstants.SheetHoSo}' không có dòng dữ liệu."
                    };
                }
                else
                {
                    foreach (var cfg in configHoSo)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var isRequired = cfg.BatBuoc == ConfigConstants.BatBuoc;

                        // Không có cột: lỗi nếu bắt buộc, ngược lại bỏ qua
                        if (!hoSoColumns.TryGetValue(EadFieldMapping.NormalizeHeader(cfg.TruongThongTin), out var col))
                        {
                            if (isRequired)
                            {
                                errorCount++;
                                yield return new CheckProgressDto
                                {
                                    Stage = (int)CheckStage.ExcelContent,
                                    StageName = CheckConstants.StageNameExcelContent,
                                    TargetType = CheckConstants.TargetTypeField,
                                    Target = cfg.TruongThongTin,
                                    Status = StatusCodeE.Error,
                                    Code = ErrorCodes.ColumnMissing,
                                    Message = $"Sheet '{CheckConstants.SheetHoSo}' thiếu cột bắt buộc '{cfg.TruongThongTin}'."
                                };
                            }
                            continue;
                        }

                        var value = hoSoDataRow.Cell(col).GetString().Trim();

                        // Trống: lỗi nếu bắt buộc, ngược lại bỏ qua
                        if (string.IsNullOrWhiteSpace(value))
                        {
                            if (isRequired)
                            {
                                errorCount++;
                                yield return new CheckProgressDto
                                {
                                    Stage = (int)CheckStage.ExcelContent,
                                    StageName = CheckConstants.StageNameExcelContent,
                                    TargetType = CheckConstants.TargetTypeField,
                                    Target = cfg.TruongThongTin,
                                    Status = StatusCodeE.Error,
                                    Code = ErrorCodes.RequiredFieldEmpty,
                                    Message = $"Trường bắt buộc '{cfg.TruongThongTin}' đang để trống."
                                };
                            }
                            continue;
                        }

                        // Có dữ liệu (kể cả trường không bắt buộc): đối chiếu nội dung với mô tả.
                        // Nếu mô tả là danh sách giá trị ("mã: nhãn") thì giá trị phải nằm trong danh sách.
                        var options = (cfg.MoTa ?? string.Empty).Split('\n')
                            .Select(line => Regex.Match(line.Trim(), OptionPattern))
                            .Where(m => m.Success)
                            .ToList();
                        var isEnum = options.Count >= 2;

                        if (isEnum)
                        {
                            var allowed = new HashSet<string>(
                                options.SelectMany(m => new[]
                                {
                                    m.Groups[1].Value.Trim(),
                                    m.Groups[2].Value.Trim(),
                                    m.Groups[1].Value.Trim() + ":" + m.Groups[2].Value.Trim()
                                })
                                .Select(x => string.Join(' ', x.ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Replace(": ", ":").Replace(" :", ":")),
                                StringComparer.Ordinal);

                            var normalizedValue = string.Join(' ', value.ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Replace(": ", ":").Replace(" :", ":");

                            if (!allowed.Contains(normalizedValue))
                            {
                                errorCount++;
                                yield return new CheckProgressDto
                                {
                                    Stage = (int)CheckStage.ExcelContent,
                                    StageName = CheckConstants.StageNameExcelContent,
                                    TargetType = CheckConstants.TargetTypeField,
                                    Target = cfg.TruongThongTin,
                                    Status = StatusCodeE.Error,
                                    Code = ErrorCodes.FieldContentInvalid,
                                    Message = $"Trường '{cfg.TruongThongTin}' có giá trị '{value}' không nằm trong danh sách giá trị cho phép theo mô tả."
                                };
                                continue;
                            }
                        }

                        yield return new CheckProgressDto
                        {
                            Stage = (int)CheckStage.ExcelContent,
                            StageName = CheckConstants.StageNameExcelContent,
                            TargetType = CheckConstants.TargetTypeField,
                            Target = cfg.TruongThongTin,
                            Status = StatusCodeE.Success,
                            Code = 200,
                            Message = $"Trường '{cfg.TruongThongTin}' hợp lệ."
                        };
                    }
                }

                // 3.2 Sheet Van_ban (nhiều dòng = nhiều tài liệu)
                if (configVanBan.Count == 0)
                {
                    yield return new CheckProgressDto
                    {
                        Stage = (int)CheckStage.ExcelContent,
                        StageName = CheckConstants.StageNameExcelContent,
                        TargetType = CheckConstants.TargetTypeSheet,
                        Target = CheckConstants.SheetVanBan,
                        Status = StatusCodeE.Error,
                        Code = ErrorCodes.ConfigEmpty,
                        Message = $"Chưa có cấu hình mô tả cho '{CheckConstants.SheetVanBan}' trong hệ thống để đối chiếu."
                    };
                }
                else
                {
                    var rowIndex = 0;
                    foreach (var row in vanBanDataRows)
                    {
                        rowIndex++;
                        var rowLabel = vanBanColumns.TryGetValue(EadFieldMapping.NormalizeHeader(CheckConstants.ColMaLuuTruTaiLieu), out var idCol)
                            ? row.Cell(idCol).GetString().Trim()
                            : string.Empty;
                        if (string.IsNullOrWhiteSpace(rowLabel)) rowLabel = $"Dòng {rowIndex}";

                        foreach (var cfg in configVanBan)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            var isRequired = cfg.BatBuoc == ConfigConstants.BatBuoc;
                            var target = $"{rowLabel} / {cfg.TruongThongTin}";

                            // Không có cột: lỗi nếu bắt buộc, ngược lại bỏ qua
                            if (!vanBanColumns.TryGetValue(EadFieldMapping.NormalizeHeader(cfg.TruongThongTin), out var col))
                            {
                                if (isRequired)
                                {
                                    errorCount++;
                                    yield return new CheckProgressDto
                                    {
                                        Stage = (int)CheckStage.ExcelContent,
                                        StageName = CheckConstants.StageNameExcelContent,
                                        TargetType = CheckConstants.TargetTypeField,
                                        Target = target,
                                        Status = StatusCodeE.Error,
                                        Code = ErrorCodes.ColumnMissing,
                                        Message = $"File '{rowLabel}' trong sheet '{CheckConstants.SheetVanBan}' đang thiếu cột '{cfg.TruongThongTin}'."
                                    };
                                }
                                continue;
                            }

                            var value = row.Cell(col).GetString().Trim();

                            // Trống: lỗi nếu bắt buộc, ngược lại bỏ qua
                            if (string.IsNullOrWhiteSpace(value))
                            {
                                if (isRequired)
                                {
                                    errorCount++;
                                    yield return new CheckProgressDto
                                    {
                                        Stage = (int)CheckStage.ExcelContent,
                                        StageName = CheckConstants.StageNameExcelContent,
                                        TargetType = CheckConstants.TargetTypeField,
                                        Target = target,
                                        Status = StatusCodeE.Error,
                                        Code = ErrorCodes.RequiredFieldEmpty,
                                        Message = $"File '{rowLabel}' trong sheet '{CheckConstants.SheetVanBan}' đang thiếu cột '{cfg.TruongThongTin}'."
                                    };
                                }
                                continue;
                            }

                            // Có dữ liệu (kể cả trường không bắt buộc): đối chiếu nội dung với mô tả.
                            var options = (cfg.MoTa ?? string.Empty).Split('\n')
                                .Select(line => Regex.Match(line.Trim(), OptionPattern))
                                .Where(m => m.Success)
                                .ToList();
                            var isEnum = options.Count >= 2;

                            if (isEnum)
                            {
                                var allowed = new HashSet<string>(
                                    options.SelectMany(m => new[]
                                    {
                                        m.Groups[1].Value.Trim(),
                                        m.Groups[2].Value.Trim(),
                                        m.Groups[1].Value.Trim() + ":" + m.Groups[2].Value.Trim()
                                    })
                                    .Select(x => string.Join(' ', x.ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Replace(": ", ":").Replace(" :", ":")),
                                    StringComparer.Ordinal);

                                var normalizedValue = string.Join(' ', value.ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Replace(": ", ":").Replace(" :", ":");

                                if (!allowed.Contains(normalizedValue))
                                {
                                    errorCount++;
                                    yield return new CheckProgressDto
                                    {
                                        Stage = (int)CheckStage.ExcelContent,
                                        StageName = CheckConstants.StageNameExcelContent,
                                        TargetType = CheckConstants.TargetTypeField,
                                        Target = target,
                                        Status = StatusCodeE.Error,
                                        Code = ErrorCodes.FieldContentInvalid,
                                        Message = $"File '{rowLabel}' trong sheet '{CheckConstants.SheetVanBan}': trường '{cfg.TruongThongTin}' có giá trị '{value}' không hợp lệ theo mô tả."
                                    };
                                    continue;
                                }
                            }

                            yield return new CheckProgressDto
                            {
                                Stage = (int)CheckStage.ExcelContent,
                                StageName = CheckConstants.StageNameExcelContent,
                                TargetType = CheckConstants.TargetTypeField,
                                Target = target,
                                Status = StatusCodeE.Success,
                                Code = 200,
                                Message = $"Trường '{cfg.TruongThongTin}' hợp lệ."
                            };
                        }
                    }
                }

                // Tổng kết giai đoạn 3
                yield return new CheckProgressDto
                {
                    Stage = (int)CheckStage.ExcelContent,
                    StageName = CheckConstants.StageNameExcelContent,
                    TargetType = CheckConstants.TargetTypeSummary,
                    Target = CheckConstants.StageNameExcelContent,
                    Status = errorCount == 0 ? StatusCodeE.Success : StatusCodeE.Error,
                    Code = errorCount == 0 ? 200 : ErrorCodes.CheckHasErrors,
                    Message = errorCount == 0 ? "Nội dung file Excel chính xác." : "Nội dung file Excel có lỗi, xem chi tiết."
                };
            }

            // ========================= TỔNG KẾT =========================
            yield return new CheckProgressDto
            {
                Stage = (int)CheckStage.ExcelContent,
                StageName = "Hoàn tất kiểm tra",
                TargetType = CheckConstants.TargetTypeSummary,
                Target = "result",
                Status = errorCount == 0 ? StatusCodeE.Success : StatusCodeE.Error,
                Code = errorCount == 0 ? 200 : ErrorCodes.CheckHasErrors,
                Message = errorCount == 0
                    ? "Đủ điều kiện để đóng gói SIP theo chuẩn thông tư TT05."
                    : "Không đủ điều kiện để đóng gói SIP theo chuẩn thông tư TT05.",
                Completed = true
            };
        }

        // =====================================================================
        // ============== KIỂM TRA NHIỀU HỒ SƠ (multi) — luồng riêng =============
        // =====================================================================

        /// <summary>Kết quả kiểm tra nội dung 1 hồ sơ (thuần, không đụng DB — an toàn khi chạy song song).</summary>
        public class HoSoCheckResult
        {
            public string HoSoName { get; set; } = string.Empty;   // = tên thư mục hồ sơ
            public bool Ok { get; set; }                           // true nếu không có lỗi nào
            public List<string> Errors { get; } = new();           // chi tiết lỗi cấp hồ sơ (nếu có)
            public int FileCount { get; set; }                     // số file đính kèm
            public string? MaHoSo { get; set; }                    // mã hồ sơ đọc từ sheet Ho_so (dò trùng toàn batch)
            public List<FileCheckResultDto> Files { get; } = new(); // kết quả kiểm tra từng file (bảng con per-file)
        }

        public async IAsyncEnumerable<MultiCheckProgressDto> CheckMultiFolderAsync(
            CheckMultiFolderRequestDto input,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _logger.LogInformation($"{nameof(CheckMultiFolderAsync)}, parentFolderPath = {input?.ParentFolderPath}");

            var parentPath = input?.ParentFolderPath?.Trim();

            // ===================== TIỀN ĐIỀU KIỆN: THƯ MỤC CHA =====================
            if (string.IsNullOrWhiteSpace(parentPath) || !Directory.Exists(parentPath))
            {
                yield return new MultiCheckProgressDto
                {
                    Phase = (int)MultiCheckPhase.StructureValidation,
                    PhaseName = CheckConstants.StageNameStructureValidation,
                    TargetType = CheckConstants.TargetTypeParent,
                    Status = StatusCodeE.Error,
                    Code = ErrorCodes.ParentFolderNotFound,
                    Message = $"Không tìm thấy thư mục cha: '{parentPath}'.",
                    Completed = true
                };
                yield break;
            }

            var childDirs = Directory.GetDirectories(parentPath);
            if (childDirs.Length == 0)
            {
                yield return new MultiCheckProgressDto
                {
                    Phase = (int)MultiCheckPhase.StructureValidation,
                    PhaseName = CheckConstants.StageNameStructureValidation,
                    TargetType = CheckConstants.TargetTypeParent,
                    Status = StatusCodeE.Error,
                    Code = ErrorCodes.NoHoSoInParent,
                    Message = "Thư mục cha không chứa hồ sơ con nào.",
                    Completed = true
                };
                yield break;
            }

            var total = childDirs.Length;
            var completed = 0;

            // Manifest: liệt kê sẵn tên tất cả hồ sơ con để FE seed các dòng (spinner) trước khi có kết quả.
            yield return new MultiCheckProgressDto
            {
                Phase = (int)MultiCheckPhase.StructureValidation,
                PhaseName = CheckConstants.StageNameStructureValidation,
                TargetType = CheckConstants.TargetTypeManifest,
                HoSoNames = childDirs.Select(d => new DirectoryInfo(d).Name).ToList(),
                Status = StatusCodeE.Success,
                Code = 200,
                TotalHoSo = total
            };

            // ================= PHASE 1: CẤU TRÚC + TRÙNG LẶP (tuần tự) =================

            // Trùng tên thư mục hồ sơ (bắt biến thể hoa/thường; cùng thư mục cha OS đã chặn trùng tuyệt đối)
            var dupFolderNames = childDirs
                .GroupBy(d => new DirectoryInfo(d).Name, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Cấu trúc từng hồ sơ + gom tên file đính kèm để dò trùng (chỉ đọc TÊN, không nạp nội dung -> không tốn RAM)
            var structuralError = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); // hoSoName -> lý do
            var fileOwners = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase); // tên file -> các hồ sơ chứa nó

            foreach (var dir in childDirs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var name = new DirectoryInfo(dir).Name;

                var metadataDir = Directory.GetDirectories(dir)
                    .FirstOrDefault(d => string.Equals(new DirectoryInfo(d).Name, CheckConstants.MetadataFolderName, StringComparison.OrdinalIgnoreCase));
                var attachmentDir = Directory.GetDirectories(dir)
                    .FirstOrDefault(d => string.Equals(new DirectoryInfo(d).Name, CheckConstants.AttachmentFolderName, StringComparison.OrdinalIgnoreCase));

                if (metadataDir == null || attachmentDir == null)
                {
                    structuralError[name] = $"Thiếu thư mục '{CheckConstants.MetadataFolderName}' hoặc '{CheckConstants.AttachmentFolderName}'.";
                    continue;
                }

                var hasExcel = Directory.GetFiles(metadataDir)
                    .Any(f => CheckConstants.ExcelExtensions.Contains(Path.GetExtension(f)));
                if (!hasExcel)
                {
                    structuralError[name] = $"Thư mục '{CheckConstants.MetadataFolderName}' không chứa file Excel.";
                    continue;
                }

                foreach (var file in Directory.GetFiles(attachmentDir))
                {
                    var fileName = Path.GetFileName(file);
                    if (!fileOwners.TryGetValue(fileName, out var owners))
                    {
                        owners = new List<string>();
                        fileOwners[fileName] = owners;
                    }
                    owners.Add(name);
                }
            }

            // Tên file trùng (giữa các hồ sơ): 1 tên file xuất hiện ở >1 hồ sơ -> mọi hồ sơ liên quan đều lỗi
            var dupFilesByHoSo = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in fileOwners)
            {
                if (kv.Value.Count <= 1) continue;
                foreach (var owner in kv.Value.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (!dupFilesByHoSo.TryGetValue(owner, out var set))
                    {
                        set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        dupFilesByHoSo[owner] = set;
                    }
                    set.Add(kv.Key);
                }
            }

            // Phát event lỗi cho hồ sơ không hợp lệ (Phase 1) + chốt danh sách hồ sơ hợp lệ cho Phase 2
            var validFolders = new List<string>();
            foreach (var dir in childDirs)
            {
                var name = new DirectoryInfo(dir).Name;
                var errors = new List<string>();

                if (dupFolderNames.Contains(name))
                    errors.Add($"Tên thư mục hồ sơ '{name}' bị trùng lặp trong thư mục cha.");
                if (structuralError.TryGetValue(name, out var reason))
                    errors.Add(reason);
                if (dupFilesByHoSo.TryGetValue(name, out var dupFiles))
                    errors.Add($"Tên file bị trùng lặp với hồ sơ khác: {string.Join(", ", dupFiles.OrderBy(x => x))}.");

                if (errors.Count > 0)
                {
                    completed++;
                    yield return new MultiCheckProgressDto
                    {
                        Phase = (int)MultiCheckPhase.StructureValidation,
                        PhaseName = CheckConstants.StageNameStructureValidation,
                        TargetType = CheckConstants.TargetTypeHoSo,
                        HoSoName = name,
                        Status = StatusCodeE.Error,
                        Code = ErrorCodes.CheckHasErrors,
                        Message = "Hồ sơ không hợp lệ ở bước kiểm tra cấu trúc/trùng lặp.",
                        Errors = errors,
                        TotalHoSo = total,
                        CompletedHoSo = completed
                    };
                }
                else
                {
                    validFolders.Add(dir);
                }
            }

            // ===== Nạp cấu hình 1 LẦN trước khi chia luồng (DbContext KHÔNG thread-safe) =====
            var configHoSoList = await _sipDbContext.ConfigHoSo.Where(x => !x.Deleted).ToListAsync(cancellationToken);
            var configVanBanList = await _sipDbContext.ConfigVanBan.Where(x => !x.Deleted).ToListAsync(cancellationToken);

            // ================= PHASE 2: KIỂM TRA NỘI DUNG (song song, giới hạn luồng) =================
            var results = new ConcurrentBag<HoSoCheckResult>();
            if (validFolders.Count > 0)
            {
                var degree = input.MaxDegreeOfParallelism.GetValueOrDefault(Environment.ProcessorCount);
                if (degree < 1) degree = 1;

                // Channel gom event từ nhiều luồng về 1 stream SSE (lock-free -> tránh deadlock)
                var channel = Channel.CreateUnbounded<MultiCheckProgressDto>(new UnboundedChannelOptions
                {
                    SingleReader = true
                });

                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = degree,
                    CancellationToken = cancellationToken
                };

                var producer = Task.Run(async () =>
                {
                    try
                    {
                        await Parallel.ForEachAsync(validFolders, parallelOptions, async (folder, token) =>
                        {
                            // Mỗi hồ sơ: mở -> đọc -> giải phóng workbook ngay trong InspectHoSo (đỉnh RAM ~ degree × 1 workbook)
                            var r = InspectHoSo(folder, configHoSoList, configVanBanList, token);
                            results.Add(r);

                            var done = Interlocked.Increment(ref completed);
                            await channel.Writer.WriteAsync(new MultiCheckProgressDto
                            {
                                Phase = (int)MultiCheckPhase.Processing,
                                PhaseName = CheckConstants.StageNameProcessing,
                                TargetType = CheckConstants.TargetTypeHoSo,
                                HoSoName = r.HoSoName,
                                Status = r.Ok ? StatusCodeE.Success : StatusCodeE.Error,
                                Code = r.Ok ? 200 : ErrorCodes.CheckHasErrors,
                                Message = r.Ok ? "Hồ sơ hợp lệ." : "Hồ sơ có lỗi, xem chi tiết.",
                                Errors = r.Ok ? null : r.Errors,
                                Files = r.Files.Count > 0 ? r.Files : null,
                                FileCount = r.FileCount,
                                TotalHoSo = total,
                                CompletedHoSo = done
                            }, token);
                        });
                    }
                    finally
                    {
                        channel.Writer.Complete();
                    }
                }, cancellationToken);

                await foreach (var ev in channel.Reader.ReadAllAsync(cancellationToken))
                {
                    yield return ev;
                }

                await producer; // quan sát lỗi/huỷ từ vùng song song
            }

            // ===== Trùng mã hồ sơ giữa các hồ sơ (đối chiếu sau khi đã đọc nội dung ở Phase 2) =====
            var dupCodeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var codeGroups = results
                .Where(r => !string.IsNullOrWhiteSpace(r.MaHoSo))
                .GroupBy(r => r.MaHoSo!, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1);

            foreach (var g in codeGroups)
            {
                var names = g.Select(r => r.HoSoName).OrderBy(x => x).ToList();
                foreach (var name in names) dupCodeNames.Add(name);

                yield return new MultiCheckProgressDto
                {
                    Phase = (int)MultiCheckPhase.Processing,
                    PhaseName = CheckConstants.StageNameProcessing,
                    TargetType = CheckConstants.TargetTypeDuplicate,
                    Status = StatusCodeE.Error,
                    Code = ErrorCodes.DuplicateHoSoCode,
                    Message = $"Mã hồ sơ '{g.Key}' bị trùng giữa các hồ sơ: {string.Join(", ", names)}.",
                    Errors = names.Select(n => $"Hồ sơ '{n}' dùng mã hồ sơ trùng '{g.Key}'.").ToList(),
                    TotalHoSo = total,
                    CompletedHoSo = completed
                };
            }

            // ============================= TỔNG KẾT =============================
            var okCount = results.Count(r => r.Ok && !dupCodeNames.Contains(r.HoSoName));
            var errorCount = total - okCount;

            yield return new MultiCheckProgressDto
            {
                Phase = (int)MultiCheckPhase.Done,
                PhaseName = CheckConstants.StageNameMultiSummary,
                TargetType = CheckConstants.TargetTypeSummary,
                Status = errorCount == 0 ? StatusCodeE.Success : StatusCodeE.Error,
                Code = errorCount == 0 ? 200 : ErrorCodes.CheckHasErrors,
                Message = errorCount == 0
                    ? $"Tất cả {total} hồ sơ đủ điều kiện đóng gói SIP theo chuẩn thông tư TT05."
                    : $"Có {errorCount}/{total} hồ sơ chưa đạt, xem chi tiết từng hồ sơ.",
                TotalHoSo = total,
                CompletedHoSo = total,
                OkCount = okCount,
                ErrorCount = errorCount,
                Completed = true
            };
        }

        /// <summary>
        /// Kiểm tra nội dung 1 hồ sơ theo 3 giai đoạn (cấu trúc -> khớp mã -> nội dung Excel), gom lỗi vào kết quả.
        /// THUẦN: không truy cập DB, không state chia sẻ -> an toàn khi gọi song song từ nhiều luồng.
        /// Cấu hình (configHoSo/configVanBan) phải được nạp sẵn từ trước và truyền vào (bất biến).
        /// </summary>
        public HoSoCheckResult InspectHoSo(
            string folderPath,
            IReadOnlyList<ConfigHoSo> configHoSo,
            IReadOnlyList<ConfigVanBan> configVanBan,
            CancellationToken cancellationToken)
        {
            var result = new HoSoCheckResult { HoSoName = new DirectoryInfo(folderPath).Name };

            // Gán lỗi vào 1 file trong bảng con (khớp theo tên đầy đủ HOẶC tên không đuôi = docCode); lỗi đầu tiên thắng.
            void MarkFileError(string key, string reason)
            {
                var row = result.Files.FirstOrDefault(f =>
                    string.Equals(f.Name, key, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(Path.GetFileNameWithoutExtension(f.Name), key, StringComparison.OrdinalIgnoreCase));
                if (row != null && row.Status != StatusCodeE.Error)
                {
                    row.Status = StatusCodeE.Error;
                    row.Reason = reason;
                }
            }

            // ===== Giai đoạn 1: cấu trúc (lỗi ở đây là nghiêm trọng -> dừng kiểm tra hồ sơ này) =====
            var metadataDir = Directory.GetDirectories(folderPath)
                .FirstOrDefault(d => string.Equals(new DirectoryInfo(d).Name, CheckConstants.MetadataFolderName, StringComparison.OrdinalIgnoreCase));
            if (metadataDir == null)
            {
                result.Errors.Add($"Thiếu thư mục '{CheckConstants.MetadataFolderName}'.");
                return result;
            }

            var attachmentDir = Directory.GetDirectories(folderPath)
                .FirstOrDefault(d => string.Equals(new DirectoryInfo(d).Name, CheckConstants.AttachmentFolderName, StringComparison.OrdinalIgnoreCase));
            if (attachmentDir == null)
            {
                result.Errors.Add($"Thiếu thư mục '{CheckConstants.AttachmentFolderName}'.");
                return result;
            }

            var excelPath = Directory.GetFiles(metadataDir)
                .FirstOrDefault(f => CheckConstants.ExcelExtensions.Contains(Path.GetExtension(f)));
            if (excelPath == null)
            {
                result.Errors.Add($"Thư mục '{CheckConstants.MetadataFolderName}' không chứa file Excel.");
                return result;
            }

            var attachmentFiles = Directory.GetFiles(attachmentDir);
            result.FileCount = attachmentFiles.Length;
            // Seed 1 dòng/file (mặc định Success); các bước sau đánh dấu lỗi vào đúng file.
            foreach (var f in attachmentFiles)
            {
                result.Files.Add(new FileCheckResultDto { Name = Path.GetFileName(f), Status = StatusCodeE.Success });
            }
            if (attachmentFiles.Length == 0)
            {
                result.Errors.Add($"Thư mục '{CheckConstants.AttachmentFolderName}' không có file đính kèm.");
                return result;
            }

            XLWorkbook? workbook;
            try
            {
                workbook = new XLWorkbook(excelPath);
            }
            catch
            {
                result.Errors.Add($"Không đọc được file Excel '{Path.GetFileName(excelPath)}'.");
                return result;
            }

            using (workbook)
            {
                // File đính kèm sai định dạng (không nghiêm trọng -> gom lỗi, kiểm tra tiếp)
                foreach (var file in attachmentFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!CheckConstants.AllowedAttachmentExtensions.Contains(Path.GetExtension(file)))
                    {
                        result.Errors.Add($"File '{Path.GetFileName(file)}' có định dạng không được hỗ trợ.");
                        MarkFileError(Path.GetFileName(file), "Định dạng file không được hỗ trợ.");
                    }
                }

                if (!workbook.Worksheets.TryGetWorksheet(CheckConstants.SheetHoSo, out var hoSoSheet))
                {
                    result.Errors.Add($"File Excel thiếu sheet '{CheckConstants.SheetHoSo}'.");
                    return result;
                }
                if (!workbook.Worksheets.TryGetWorksheet(CheckConstants.SheetVanBan, out var vanBanSheet))
                {
                    result.Errors.Add($"File Excel thiếu sheet '{CheckConstants.SheetVanBan}'.");
                    return result;
                }

                var hoSoColumns = BuildColumnMap(hoSoSheet);
                var vanBanColumns = BuildColumnMap(vanBanSheet);
                var hoSoDataRow = hoSoSheet.RowsUsed().Skip(1).FirstOrDefault();
                var vanBanDataRows = vanBanSheet.RowsUsed().Skip(1).ToList();

                // ===== Giai đoạn 2: khớp mã hồ sơ / mã định danh tài liệu =====
                if (hoSoDataRow == null || !hoSoColumns.TryGetValue(EadFieldMapping.NormalizeHeader(CheckConstants.ColMaHoSo), out var maHoSoCol))
                {
                    result.Errors.Add($"Sheet '{CheckConstants.SheetHoSo}' thiếu cột '{CheckConstants.ColMaHoSo}' hoặc không có dữ liệu.");
                }
                else
                {
                    var maHoSo = hoSoDataRow.Cell(maHoSoCol).GetString().Trim();
                    result.MaHoSo = maHoSo;
                    if (!string.Equals(maHoSo, result.HoSoName, StringComparison.OrdinalIgnoreCase))
                    {
                        result.Errors.Add($"Mã hồ sơ '{maHoSo}' không khớp với tên thư mục '{result.HoSoName}'.");
                    }
                }

                if (!vanBanColumns.TryGetValue(EadFieldMapping.NormalizeHeader(CheckConstants.ColMaLuuTruTaiLieu), out var maTaiLieuCol))
                {
                    result.Errors.Add($"Sheet '{CheckConstants.SheetVanBan}' thiếu cột '{CheckConstants.ColMaLuuTruTaiLieu}'.");
                }
                else
                {
                    var maTaiLieuSet = new HashSet<string>(
                        vanBanDataRows.Select(r => r.Cell(maTaiLieuCol).GetString().Trim()).Where(v => !string.IsNullOrWhiteSpace(v)),
                        StringComparer.OrdinalIgnoreCase);
                    var fileNameSet = new HashSet<string>(
                        attachmentFiles.Select(Path.GetFileNameWithoutExtension),
                        StringComparer.OrdinalIgnoreCase);

                    foreach (var file in attachmentFiles)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!maTaiLieuSet.Contains(Path.GetFileNameWithoutExtension(file)))
                        {
                            result.Errors.Add($"File '{Path.GetFileName(file)}' không có mã định danh tài liệu tương ứng trong sheet '{CheckConstants.SheetVanBan}'.");
                            MarkFileError(Path.GetFileName(file), $"Không có mã định danh tương ứng trong sheet '{CheckConstants.SheetVanBan}'.");
                        }
                    }
                    foreach (var ma in maTaiLieuSet)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!fileNameSet.Contains(ma))
                        {
                            result.Errors.Add($"Mã định danh tài liệu '{ma}' không có file đính kèm tương ứng.");
                        }
                    }
                }

                // ===== Giai đoạn 3: nội dung Excel so với cấu hình (bỏ qua nếu chưa có cấu hình) =====
                if (configHoSo.Count > 0 && hoSoDataRow != null)
                {
                    foreach (var cfg in configHoSo)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        ValidateField(cfg.TruongThongTin, cfg.MoTa, cfg.BatBuoc == ConfigConstants.BatBuoc,
                            hoSoColumns, hoSoDataRow, CheckConstants.SheetHoSo, null, result.Errors);
                    }
                }

                if (configVanBan.Count > 0)
                {
                    var rowIndex = 0;
                    foreach (var row in vanBanDataRows)
                    {
                        rowIndex++;
                        var rowLabel = vanBanColumns.TryGetValue(EadFieldMapping.NormalizeHeader(CheckConstants.ColMaLuuTruTaiLieu), out var idCol)
                            ? row.Cell(idCol).GetString().Trim()
                            : string.Empty;
                        if (string.IsNullOrWhiteSpace(rowLabel)) rowLabel = $"Dòng {rowIndex}";

                        var rowErrors = new List<string>();
                        foreach (var cfg in configVanBan)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            ValidateField(cfg.TruongThongTin, cfg.MoTa, cfg.BatBuoc == ConfigConstants.BatBuoc,
                                vanBanColumns, row, CheckConstants.SheetVanBan, rowLabel, rowErrors);
                        }
                        if (rowErrors.Count > 0)
                        {
                            result.Errors.AddRange(rowErrors);
                            MarkFileError(rowLabel, rowErrors[0]); // rowLabel = mã lưu trữ = tên file (không đuôi)
                        }
                    }
                }
            }

            result.Ok = result.Errors.Count == 0;
            return result;
        }

        /// <summary>Map header (đã trim) -> số cột của 1 sheet. Helper dùng chung cho luồng multi.</summary>
        public Dictionary<string, int> BuildColumnMap(IXLWorksheet sheet)
        {
            var header = sheet.FirstRowUsed();
            if (header == null) return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            return header.CellsUsed()
                .Where(c => !string.IsNullOrWhiteSpace(c.GetString()))
                .GroupBy(c => EadFieldMapping.NormalizeHeader(c.GetString()), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Address.ColumnNumber, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Kiểm tra 1 trường của 1 dòng dữ liệu theo cấu hình (bắt buộc/trống/enum), gom lỗi vào <paramref name="errors"/>.
        /// <paramref name="rowLabel"/> = null cho sheet Ho_so (1 dòng), hoặc nhãn dòng (mã lưu trữ) cho sheet Van_ban.
        /// </summary>
        public void ValidateField(
            string truongThongTin, string? moTa, bool isRequired,
            Dictionary<string, int> columns, IXLRow row,
            string sheetName, string? rowLabel, List<string> errors)
        {
            var prefix = rowLabel == null ? $"Sheet '{sheetName}'" : $"File '{rowLabel}' (sheet '{sheetName}')";

            if (!columns.TryGetValue(EadFieldMapping.NormalizeHeader(truongThongTin), out var col))
            {
                if (isRequired)
                    errors.Add($"{prefix} thiếu cột bắt buộc '{truongThongTin}'.");
                return;
            }

            var value = row.Cell(col).GetString().Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                if (isRequired)
                    errors.Add($"{prefix}: trường bắt buộc '{truongThongTin}' đang để trống.");
                return;
            }

            if (!IsFieldValueValid(value, moTa))
            {
                errors.Add($"{prefix}: trường '{truongThongTin}' có giá trị '{value}' không hợp lệ theo mô tả.");
            }
        }

        /// <summary>
        /// Trường có giá trị hợp lệ so với mô tả không. Nếu mô tả là danh sách "mã: nhãn" (>=2 dòng) thì giá trị
        /// phải nằm trong danh sách; ngược lại (mô tả tự do) luôn hợp lệ. Chuẩn hoá giống luồng kiểm tra 1 hồ sơ.
        /// </summary>
        public bool IsFieldValueValid(string value, string? moTa)
        {
            var options = (moTa ?? string.Empty).Split('\n')
                .Select(line => Regex.Match(line.Trim(), OptionPattern))
                .Where(m => m.Success)
                .ToList();
            if (options.Count < 2) return true; // không phải danh sách enum -> chấp nhận

            var allowed = new HashSet<string>(
                options.SelectMany(m => new[]
                {
                    m.Groups[1].Value.Trim(),
                    m.Groups[2].Value.Trim(),
                    m.Groups[1].Value.Trim() + ":" + m.Groups[2].Value.Trim()
                })
                .Select(NormalizeOption),
                StringComparer.Ordinal);

            return allowed.Contains(NormalizeOption(value));
        }

        /// <summary>Chuẩn hoá 1 giá trị enum để so khớp (lowercase, gộp khoảng trắng, bỏ khoảng quanh dấu ':').</summary>
        private static string NormalizeOption(string value)
        {
            return string.Join(' ', value.ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                .Replace(": ", ":").Replace(" :", ":");
        }
    }
}
