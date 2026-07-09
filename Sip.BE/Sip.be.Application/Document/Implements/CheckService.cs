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
using Sip.be.Shared.Request.AppException;
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

        private readonly IPdfSignatureReader _pdfSignatureReader;
        private readonly ISignatureVerifier _signatureVerifier;

        public CheckService(SipDbContext sipDbContext, ILogger<BaseService> logger, IMapper mapper,
            IPdfSignatureReader pdfSignatureReader, ISignatureVerifier signatureVerifier) : base(sipDbContext, logger, mapper)
        {
            _pdfSignatureReader = pdfSignatureReader;
            _signatureVerifier = signatureVerifier;
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
                    // Đọc chữ ký số + verify DẤU THỜI GIAN tin cậy (RFC 3161) của file để gắn kèm event.
                    var signature = _pdfSignatureReader.Read(file);
                    var ts = _signatureVerifier.Verify(file);
                    signature.Timestamp = ts;
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
                            Message = $"File '{fileName}' có định dạng không được hỗ trợ (chỉ chấp nhận PDF, hình ảnh, âm thanh, phim).",
                            Signature = signature
                        };
                    }
                    else if (ts.Blocks)
                    {
                        // PDF không có/không hợp lệ dấu thời gian tin cậy -> lỗi chặn đóng gói (non-PDF không rơi vào đây).
                        errorCount++;
                        yield return new CheckProgressDto
                        {
                            Stage = (int)CheckStage.FolderStructure,
                            StageName = CheckConstants.StageNameFolderStructure,
                            TargetType = CheckConstants.TargetTypeFile,
                            Target = fileName,
                            Status = StatusCodeE.Error,
                            Code = ErrorCodes.TimestampInvalid,
                            Message = ts.Reason ?? "Dấu thời gian/chữ ký số của file không hợp lệ.",
                            Signature = signature
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
                            Message = "Định dạng file đính kèm hợp lệ.",
                            Signature = signature
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
                if (!vanBanColumns.TryGetValue(EadFieldMapping.NormalizeHeader(CheckConstants.ColMaDinhDanhTaiLieu), out var maTaiLieuCol))
                {
                    errorCount++;
                    yield return new CheckProgressDto
                    {
                        Stage = (int)CheckStage.CodeMatching,
                        StageName = CheckConstants.StageNameCodeMatching,
                        TargetType = CheckConstants.TargetTypeCode,
                        Target = CheckConstants.ColMaDinhDanhTaiLieu,
                        Status = StatusCodeE.Error,
                        Code = ErrorCodes.ColumnMissing,
                        Message = $"Sheet '{CheckConstants.SheetVanBan}' thiếu cột '{CheckConstants.ColMaDinhDanhTaiLieu}'."
                    };
                }
                else
                {
                    var maTaiLieuSet = new HashSet<string>(
                        vanBanDataRows
                            .Select(r => r.Cell(maTaiLieuCol).GetString().Trim())
                            .Where(v => !string.IsNullOrWhiteSpace(v)),
                        StringComparer.OrdinalIgnoreCase);

                    // Mỗi file đính kèm: (a) tên phải THUỘC hồ sơ (bắt đầu bằng "{mã hồ sơ}."),
                    // (b) phải khớp 1 mã định danh tài liệu trong sheet (phát hiện file thừa/sai tên).
                    // Chiều ngược lại (mã định danh có trong sheet nhưng THIẾU file) KHÔNG còn tính là lỗi.
                    foreach (var file in attachmentFiles)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var fileName = Path.GetFileName(file);
                        var nameNoExt = Path.GetFileNameWithoutExtension(file);

                        // (a) Tên file phải bắt đầu bằng "{mã hồ sơ}." (mã định danh tài liệu = mã hồ sơ + số thứ tự).
                        if (!nameNoExt.StartsWith(hoSoFolderName + ".", StringComparison.OrdinalIgnoreCase))
                        {
                            errorCount++;
                            yield return new CheckProgressDto
                            {
                                Stage = (int)CheckStage.CodeMatching,
                                StageName = CheckConstants.StageNameCodeMatching,
                                TargetType = CheckConstants.TargetTypeFile,
                                Target = fileName,
                                Status = StatusCodeE.Error,
                                Code = ErrorCodes.FileNotBelongToHoSo,
                                Message = $"File '{fileName}' không thuộc hồ sơ '{hoSoFolderName}' (tên file phải bắt đầu bằng mã hồ sơ '{hoSoFolderName}.')."
                            };
                            continue;
                        }

                        // (b) Tên file phải khớp 1 mã định danh tài liệu trong sheet.
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

                        // Trống: bỏ qua (trường bắt buộc chỉ cần CÓ CỘT, KHÔNG bắt buộc có dữ liệu).
                        if (string.IsNullOrWhiteSpace(value))
                        {
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
                        var rowLabel = vanBanColumns.TryGetValue(EadFieldMapping.NormalizeHeader(CheckConstants.ColMaDinhDanhTaiLieu), out var idCol)
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

                            // Trống: bỏ qua (trường bắt buộc chỉ cần CÓ CỘT, KHÔNG bắt buộc có dữ liệu).
                            if (string.IsNullOrWhiteSpace(value))
                            {
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

            // === NHẬN DIỆN CẤU TRÚC: có file Excel dùng CHUNG ở thư mục cha -> loại 2/3 (metadata chung) ===
            // Loại 3: thư mục cha có folder 'metadata' chứa Excel; Loại 2: có file Excel đặt trực tiếp ở thư mục cha.
            // Khi đó mỗi hồ sơ con chứa file data TRỰC TIẾP (không có metadata/attachment) -> xử lý bằng nhánh riêng.
            // Không có Excel chung -> giữ nguyên loại 1 (mỗi hồ sơ con có metadata/ + attachment/) ở phần dưới.
            var sharedExcelPath = ResolveSharedExcelPath(parentPath);
            if (sharedExcelPath != null)
            {
                await foreach (var ev in CheckMultiSharedExcelAsync(parentPath, sharedExcelPath, input, cancellationToken))
                {
                    yield return ev;
                }
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
            // Seed 1 dòng/file (mặc định Success) kèm thông tin ký số; các bước sau đánh dấu lỗi vào đúng file.
            foreach (var f in attachmentFiles)
            {
                var sig = _pdfSignatureReader.Read(f);
                var tsv = _signatureVerifier.Verify(f);
                sig.Timestamp = tsv;
                var fileRow = new FileCheckResultDto
                {
                    Name = Path.GetFileName(f),
                    Status = tsv.Blocks ? StatusCodeE.Error : StatusCodeE.Success,
                    Reason = tsv.Blocks ? (tsv.Reason ?? "Dấu thời gian/chữ ký số của file không hợp lệ.") : null,
                    Signature = sig
                };
                // PDF không có/không hợp lệ dấu thời gian tin cậy -> lỗi hồ sơ (non-PDF không rơi vào đây).
                if (tsv.Blocks)
                {
                    result.Errors.Add($"File '{fileRow.Name}': {fileRow.Reason}");
                }
                result.Files.Add(fileRow);
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

                if (!vanBanColumns.TryGetValue(EadFieldMapping.NormalizeHeader(CheckConstants.ColMaDinhDanhTaiLieu), out var maTaiLieuCol))
                {
                    result.Errors.Add($"Sheet '{CheckConstants.SheetVanBan}' thiếu cột '{CheckConstants.ColMaDinhDanhTaiLieu}'.");
                }
                else
                {
                    var maTaiLieuSet = new HashSet<string>(
                        vanBanDataRows.Select(r => r.Cell(maTaiLieuCol).GetString().Trim()).Where(v => !string.IsNullOrWhiteSpace(v)),
                        StringComparer.OrdinalIgnoreCase);

                    // Mỗi file: (a) tên phải THUỘC hồ sơ (bắt đầu bằng "{mã hồ sơ}."), (b) phải khớp mã định danh trong sheet.
                    // Chiều ngược lại (mã định danh trong sheet nhưng THIẾU file) KHÔNG còn tính là lỗi.
                    foreach (var file in attachmentFiles)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var fileName = Path.GetFileName(file);
                        var nameNoExt = Path.GetFileNameWithoutExtension(file);

                        if (!nameNoExt.StartsWith(result.HoSoName + ".", StringComparison.OrdinalIgnoreCase))
                        {
                            result.Errors.Add($"File '{fileName}' không thuộc hồ sơ '{result.HoSoName}' (tên file phải bắt đầu bằng mã hồ sơ '{result.HoSoName}.').");
                            MarkFileError(fileName, $"Tên file không bắt đầu bằng mã hồ sơ '{result.HoSoName}.'.");
                            continue;
                        }

                        if (!maTaiLieuSet.Contains(nameNoExt))
                        {
                            result.Errors.Add($"File '{fileName}' không có mã định danh tài liệu tương ứng trong sheet '{CheckConstants.SheetVanBan}'.");
                            MarkFileError(fileName, $"Không có mã định danh tương ứng trong sheet '{CheckConstants.SheetVanBan}'.");
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
                        var rowLabel = vanBanColumns.TryGetValue(EadFieldMapping.NormalizeHeader(CheckConstants.ColMaDinhDanhTaiLieu), out var idCol)
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
                // Trống: bỏ qua (trường bắt buộc chỉ cần CÓ CỘT, KHÔNG bắt buộc có dữ liệu).
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

        // =====================================================================
        // ===== KIỂM TRA NHIỀU HỒ SƠ — CẤU TRÚC EXCEL DÙNG CHUNG (loại 2/3) =====
        // =====================================================================
        // Cấu trúc: thư mục cha chứa các thư mục hồ sơ con (bên trong là file data TRỰC TIẾP),
        // cùng 1 file Excel dùng CHUNG cho cả batch — đặt trực tiếp ở thư mục cha (loại 2) hoặc
        // trong folder 'metadata' của thư mục cha (loại 3). Khớp: mã hồ sơ (sheet Ho_so) = tên thư mục con,
        // mã lưu trữ tài liệu (sheet Van_ban) = tên file; mã lưu trữ chứa mã hồ sơ làm tiền tố (maHoSo + '.').

        /// <summary>
        /// Dò file Excel dùng CHUNG ở thư mục cha: ưu tiên Excel trong folder 'metadata' của cha (loại 3),
        /// sau đó Excel đặt trực tiếp ở thư mục cha (loại 2). Trả về null nếu không có (=> cấu trúc loại 1).
        /// </summary>
        private static string ResolveSharedExcelPath(string parentPath)
        {
            var metadataDir = Directory.GetDirectories(parentPath)
                .FirstOrDefault(d => string.Equals(new DirectoryInfo(d).Name, CheckConstants.MetadataFolderName, StringComparison.OrdinalIgnoreCase));
            if (metadataDir != null)
            {
                var excelInMetadata = Directory.GetFiles(metadataDir)
                    .FirstOrDefault(f => CheckConstants.ExcelExtensions.Contains(Path.GetExtension(f)));
                if (excelInMetadata != null) return excelInMetadata;
            }
            return Directory.GetFiles(parentPath)
                .FirstOrDefault(f => CheckConstants.ExcelExtensions.Contains(Path.GetExtension(f)));
        }

        private async IAsyncEnumerable<MultiCheckProgressDto> CheckMultiSharedExcelAsync(
            string parentPath,
            string sharedExcelPath,
            CheckMultiFolderRequestDto input,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _logger.LogInformation($"{nameof(CheckMultiSharedExcelAsync)}: Excel chung = {sharedExcelPath}");

            // Hồ sơ con = mọi thư mục con TRỪ folder 'metadata' chung (loại 3).
            var childDirs = Directory.GetDirectories(parentPath)
                .Where(d => !string.Equals(new DirectoryInfo(d).Name, CheckConstants.MetadataFolderName, StringComparison.OrdinalIgnoreCase))
                .ToArray();

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
            var folderNames = childDirs.Select(d => new DirectoryInfo(d).Name).ToList();

            // Manifest: liệt kê sẵn tên các hồ sơ con để FE seed các dòng (spinner) trước khi có kết quả.
            yield return new MultiCheckProgressDto
            {
                Phase = (int)MultiCheckPhase.StructureValidation,
                PhaseName = CheckConstants.StageNameStructureValidation,
                TargetType = CheckConstants.TargetTypeManifest,
                HoSoNames = folderNames.ToList(),
                Status = StatusCodeE.Success,
                Code = 200,
                TotalHoSo = total
            };

            // ===== Đọc Excel chung 1 LẦN (tuần tự) -> trích ra memory; lỗi ở đây là cấp cha -> dừng cả batch =====
            var parsed = TryParseSharedExcel(sharedExcelPath);
            if (!parsed.Ok)
            {
                yield return new MultiCheckProgressDto
                {
                    Phase = (int)MultiCheckPhase.StructureValidation,
                    PhaseName = CheckConstants.StageNameStructureValidation,
                    TargetType = CheckConstants.TargetTypeParent,
                    Status = StatusCodeE.Error,
                    Code = parsed.Code,
                    Message = parsed.Message,
                    Completed = true
                };
                yield break;
            }
            var data = parsed.Data!;

            // Map mã hồ sơ -> dòng Ho_so; đồng thời dò trùng mã hồ sơ trong sheet Ho_so.
            var hoSoByCode = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            var dupHoSoCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in data.HoSoRows)
            {
                var ma = GetCellValue(row, CheckConstants.ColMaHoSo);
                if (string.IsNullOrWhiteSpace(ma)) continue;
                if (!hoSoByCode.TryAdd(ma, row)) dupHoSoCodes.Add(ma);
            }

            // Gán tài liệu (dòng Van_ban) về hồ sơ: mã lưu trữ chứa mã hồ sơ làm tiền tố (maLuuTru bắt đầu bằng "maHoSo.").
            // Chọn hồ sơ có tên (mã) là tiền tố DÀI NHẤT để tránh nhầm khi các mã lồng nhau.
            var vanBanByHoSo = folderNames.ToDictionary(n => n, _ => new List<Dictionary<string, string>>(), StringComparer.OrdinalIgnoreCase);
            var orphanDocs = new List<string>();
            foreach (var row in data.VanBanRows)
            {
                var maLuuTru = GetCellValue(row, CheckConstants.ColMaDinhDanhTaiLieu);
                if (string.IsNullOrWhiteSpace(maLuuTru)) continue;
                var owner = folderNames
                    .Where(n => maLuuTru.StartsWith(n + ".", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(n => n.Length)
                    .FirstOrDefault();
                if (owner == null) orphanDocs.Add(maLuuTru);
                else vanBanByHoSo[owner].Add(row);
            }

            // ================= PHASE 1: TRÙNG LẶP TÊN THƯ MỤC / TÊN FILE (tuần tự) =================
            var dupFolderNames = childDirs
                .GroupBy(d => new DirectoryInfo(d).Name, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var fileOwners = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase); // tên file -> các hồ sơ chứa nó
            foreach (var dir in childDirs)
            {
                var name = new DirectoryInfo(dir).Name;
                foreach (var file in Directory.GetFiles(dir))
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

            var validFolders = new List<string>();
            foreach (var dir in childDirs)
            {
                var name = new DirectoryInfo(dir).Name;
                var errors = new List<string>();
                if (dupFolderNames.Contains(name))
                    errors.Add($"Tên thư mục hồ sơ '{name}' bị trùng lặp trong thư mục cha.");
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

            // Nạp cấu hình 1 LẦN trước khi chia luồng (DbContext KHÔNG thread-safe).
            var configHoSoList = await _sipDbContext.ConfigHoSo.Where(x => !x.Deleted).ToListAsync(cancellationToken);
            var configVanBanList = await _sipDbContext.ConfigVanBan.Where(x => !x.Deleted).ToListAsync(cancellationToken);

            // ================= PHASE 2: KIỂM TRA NỘI DUNG (song song, giới hạn luồng) =================
            var results = new ConcurrentBag<HoSoCheckResult>();
            if (validFolders.Count > 0)
            {
                var degree = input.MaxDegreeOfParallelism.GetValueOrDefault(Environment.ProcessorCount);
                if (degree < 1) degree = 1;

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
                            var name = new DirectoryInfo(folder).Name;
                            var hoSoRow = hoSoByCode.GetValueOrDefault(name);
                            var vanBanRows = vanBanByHoSo.TryGetValue(name, out var vr) ? vr : new List<Dictionary<string, string>>();

                            var r = InspectHoSoShared(folder, hoSoRow, vanBanRows, configHoSoList, configVanBanList, token);
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

                await producer;
            }

            // ===== Trùng mã hồ sơ trong sheet Ho_so (đánh dấu hồ sơ tương ứng là lỗi) =====
            var dupCodeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var code in dupHoSoCodes)
            {
                var owner = folderNames.FirstOrDefault(n => string.Equals(n, code, StringComparison.OrdinalIgnoreCase));
                if (owner != null) dupCodeNames.Add(owner);

                yield return new MultiCheckProgressDto
                {
                    Phase = (int)MultiCheckPhase.Processing,
                    PhaseName = CheckConstants.StageNameProcessing,
                    TargetType = CheckConstants.TargetTypeDuplicate,
                    HoSoName = owner ?? string.Empty,
                    Status = StatusCodeE.Error,
                    Code = ErrorCodes.DuplicateHoSoCode,
                    Message = $"Mã hồ sơ '{code}' bị khai trùng nhiều lần trong sheet '{CheckConstants.SheetHoSo}'.",
                    Errors = new List<string> { $"Mã hồ sơ '{code}' xuất hiện nhiều dòng trong sheet '{CheckConstants.SheetHoSo}'." },
                    TotalHoSo = total,
                    CompletedHoSo = completed
                };
            }

            // ===== Tài liệu 'mồ côi': mã lưu trữ không thuộc hồ sơ con nào (không có thư mục tương ứng) =====
            if (orphanDocs.Count > 0)
            {
                yield return new MultiCheckProgressDto
                {
                    Phase = (int)MultiCheckPhase.Processing,
                    PhaseName = CheckConstants.StageNameProcessing,
                    TargetType = CheckConstants.TargetTypeParent,
                    Status = StatusCodeE.Error,
                    Code = ErrorCodes.CheckHasErrors,
                    Message = $"Có {orphanDocs.Count} mã lưu trữ tài liệu không thuộc hồ sơ con nào.",
                    Errors = orphanDocs.Select(m => $"Mã lưu trữ tài liệu '{m}' không khớp thư mục hồ sơ con nào.").ToList(),
                    TotalHoSo = total,
                    CompletedHoSo = completed
                };
            }

            // ============================= TỔNG KẾT =============================
            var okCount = results.Count(r => r.Ok && !dupCodeNames.Contains(r.HoSoName));
            var errorCount = total - okCount;
            var batchOk = errorCount == 0 && orphanDocs.Count == 0;

            yield return new MultiCheckProgressDto
            {
                Phase = (int)MultiCheckPhase.Done,
                PhaseName = CheckConstants.StageNameMultiSummary,
                TargetType = CheckConstants.TargetTypeSummary,
                Status = batchOk ? StatusCodeE.Success : StatusCodeE.Error,
                Code = batchOk ? 200 : ErrorCodes.CheckHasErrors,
                Message = batchOk
                    ? $"Tất cả {total} hồ sơ đủ điều kiện đóng gói SIP theo chuẩn thông tư TT05."
                    : $"Có {errorCount}/{total} hồ sơ chưa đạt" + (orphanDocs.Count > 0 ? $" và {orphanDocs.Count} tài liệu không thuộc hồ sơ nào" : string.Empty) + ", xem chi tiết.",
                TotalHoSo = total,
                CompletedHoSo = total,
                OkCount = okCount,
                ErrorCount = errorCount,
                Completed = true
            };
        }

        /// <summary>Dữ liệu Excel chung đã trích ra memory (mỗi dòng = map header chuẩn hoá -> giá trị) để xử lý sau khi đóng workbook.</summary>
        private sealed class SharedExcelData
        {
            public List<Dictionary<string, string>> HoSoRows { get; set; } = new();
            public List<Dictionary<string, string>> VanBanRows { get; set; } = new();
        }

        /// <summary>
        /// Mở Excel chung, kiểm tra đủ 2 sheet + 2 cột khoá (Mã hồ sơ / Mã lưu trữ tài liệu), rồi trích toàn bộ dòng
        /// dữ liệu ra memory và đóng workbook ngay. Trả về (Ok, Code, Message, Data) — Ok=false là lỗi cấp cha.
        /// </summary>
        private (bool Ok, int Code, string Message, SharedExcelData? Data) TryParseSharedExcel(string excelPath)
        {
            XLWorkbook? workbook;
            try
            {
                workbook = new XLWorkbook(excelPath);
            }
            catch
            {
                return (false, ErrorCodes.ExcelFileUnreadable, $"Không đọc được file Excel chung '{Path.GetFileName(excelPath)}'.", null);
            }

            using (workbook)
            {
                if (!workbook.Worksheets.TryGetWorksheet(CheckConstants.SheetHoSo, out var hoSoSheet))
                    return (false, ErrorCodes.SheetMissing, $"File Excel chung thiếu sheet '{CheckConstants.SheetHoSo}'.", null);
                if (!workbook.Worksheets.TryGetWorksheet(CheckConstants.SheetVanBan, out var vanBanSheet))
                    return (false, ErrorCodes.SheetMissing, $"File Excel chung thiếu sheet '{CheckConstants.SheetVanBan}'.", null);

                var hoSoColumns = BuildColumnMap(hoSoSheet);
                var vanBanColumns = BuildColumnMap(vanBanSheet);

                if (!hoSoColumns.ContainsKey(EadFieldMapping.NormalizeHeader(CheckConstants.ColMaHoSo)))
                    return (false, ErrorCodes.ColumnMissing, $"Sheet '{CheckConstants.SheetHoSo}' thiếu cột '{CheckConstants.ColMaHoSo}'.", null);
                if (!vanBanColumns.ContainsKey(EadFieldMapping.NormalizeHeader(CheckConstants.ColMaDinhDanhTaiLieu)))
                    return (false, ErrorCodes.ColumnMissing, $"Sheet '{CheckConstants.SheetVanBan}' thiếu cột '{CheckConstants.ColMaDinhDanhTaiLieu}'.", null);

                var data = new SharedExcelData
                {
                    HoSoRows = hoSoSheet.RowsUsed().Skip(1).Select(r => BuildRowCells(r, hoSoColumns)).ToList(),
                    VanBanRows = vanBanSheet.RowsUsed().Skip(1).Select(r => BuildRowCells(r, vanBanColumns)).ToList()
                };
                return (true, 200, string.Empty, data);
            }
        }

        /// <summary>Trích 1 dòng thành map (header đã chuẩn hoá -> giá trị đã trim) cho MỌI cột trong header (kể cả ô trống).</summary>
        private static Dictionary<string, string> BuildRowCells(IXLRow row, Dictionary<string, int> columnMap)
        {
            var cells = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in columnMap)
            {
                cells[kv.Key] = row.Cell(kv.Value).GetString().Trim();
            }
            return cells;
        }

        /// <summary>Lấy giá trị 1 cột (theo tên gốc) từ map dòng đã trích; rỗng nếu cột không có trong sheet.</summary>
        private static string GetCellValue(IReadOnlyDictionary<string, string> cells, string columnName)
        {
            return cells.TryGetValue(EadFieldMapping.NormalizeHeader(columnName), out var value) ? value : string.Empty;
        }

        /// <summary>
        /// Kiểm tra nội dung 1 hồ sơ ở cấu trúc Excel dùng CHUNG (loại 2/3): file data nằm TRỰC TIẾP trong thư mục hồ sơ;
        /// dữ liệu mô tả lấy từ dòng Ho_so (khớp mã = tên thư mục) + các dòng Van_ban thuộc hồ sơ (đã trích sẵn từ Excel chung).
        /// THUẦN: không truy cập DB/workbook -> an toàn khi chạy song song.
        /// </summary>
        public HoSoCheckResult InspectHoSoShared(
            string folderPath,
            IReadOnlyDictionary<string, string>? hoSoRow,
            IReadOnlyList<Dictionary<string, string>> vanBanRows,
            IReadOnlyList<ConfigHoSo> configHoSo,
            IReadOnlyList<ConfigVanBan> configVanBan,
            CancellationToken cancellationToken)
        {
            var result = new HoSoCheckResult { HoSoName = new DirectoryInfo(folderPath).Name };
            result.MaHoSo = result.HoSoName;

            // Gán lỗi vào 1 file trong bảng con (khớp theo tên đầy đủ HOẶC tên không đuôi = mã lưu trữ); lỗi đầu tiên thắng.
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

            var dataFiles = Directory.GetFiles(folderPath);
            result.FileCount = dataFiles.Length;
            // Seed 1 dòng/file (mặc định Success) kèm thông tin ký số; các bước sau đánh dấu lỗi vào đúng file.
            foreach (var f in dataFiles)
            {
                var sig = _pdfSignatureReader.Read(f);
                var tsv = _signatureVerifier.Verify(f);
                sig.Timestamp = tsv;
                var fileRow = new FileCheckResultDto
                {
                    Name = Path.GetFileName(f),
                    Status = tsv.Blocks ? StatusCodeE.Error : StatusCodeE.Success,
                    Reason = tsv.Blocks ? (tsv.Reason ?? "Dấu thời gian/chữ ký số của file không hợp lệ.") : null,
                    Signature = sig
                };
                // PDF không có/không hợp lệ dấu thời gian tin cậy -> lỗi hồ sơ (non-PDF không rơi vào đây).
                if (tsv.Blocks)
                {
                    result.Errors.Add($"File '{fileRow.Name}': {fileRow.Reason}");
                }
                result.Files.Add(fileRow);
            }

            if (dataFiles.Length == 0)
            {
                result.Errors.Add("Thư mục hồ sơ không có file dữ liệu.");
                return result;
            }

            // Định dạng file (không nghiêm trọng -> gom lỗi, kiểm tra tiếp)
            foreach (var file in dataFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!CheckConstants.AllowedAttachmentExtensions.Contains(Path.GetExtension(file)))
                {
                    result.Errors.Add($"File '{Path.GetFileName(file)}' có định dạng không được hỗ trợ.");
                    MarkFileError(Path.GetFileName(file), "Định dạng file không được hỗ trợ.");
                }
            }

            // Giai đoạn khớp mã: hồ sơ phải có dòng Ho_so khớp mã (= tên thư mục)
            if (hoSoRow == null)
            {
                result.Errors.Add($"Không tìm thấy mã hồ sơ '{result.HoSoName}' trong sheet '{CheckConstants.SheetHoSo}' của Excel chung.");
            }

            // Khớp mã lưu trữ tài liệu <-> tên file (trong phạm vi hồ sơ này)
            var maTaiLieuSet = new HashSet<string>(
                vanBanRows.Select(r => GetCellValue(r, CheckConstants.ColMaDinhDanhTaiLieu)).Where(v => !string.IsNullOrWhiteSpace(v)),
                StringComparer.OrdinalIgnoreCase);

            // Mỗi file: (a) tên phải THUỘC hồ sơ (bắt đầu bằng "{mã hồ sơ}."), (b) phải khớp mã lưu trữ trong sheet.
            // Chiều ngược lại (mã lưu trữ trong sheet nhưng THIẾU file) KHÔNG còn tính là lỗi.
            foreach (var file in dataFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fileName = Path.GetFileName(file);
                var nameNoExt = Path.GetFileNameWithoutExtension(file);

                if (!nameNoExt.StartsWith(result.HoSoName + ".", StringComparison.OrdinalIgnoreCase))
                {
                    result.Errors.Add($"File '{fileName}' không thuộc hồ sơ '{result.HoSoName}' (tên file phải bắt đầu bằng mã hồ sơ '{result.HoSoName}.').");
                    MarkFileError(fileName, $"Tên file không bắt đầu bằng mã hồ sơ '{result.HoSoName}.'.");
                    continue;
                }

                if (!maTaiLieuSet.Contains(nameNoExt))
                {
                    result.Errors.Add($"File '{fileName}' không có mã lưu trữ tài liệu tương ứng trong sheet '{CheckConstants.SheetVanBan}'.");
                    MarkFileError(fileName, $"Không có mã lưu trữ tương ứng trong sheet '{CheckConstants.SheetVanBan}'.");
                }
            }

            // Giai đoạn nội dung: sheet Ho_so (dòng của hồ sơ này)
            if (configHoSo.Count > 0 && hoSoRow != null)
            {
                foreach (var cfg in configHoSo)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ValidateFieldCells(cfg.TruongThongTin, cfg.MoTa, cfg.BatBuoc == ConfigConstants.BatBuoc,
                        hoSoRow, CheckConstants.SheetHoSo, null, result.Errors);
                }
            }

            // Giai đoạn nội dung: sheet Van_ban (mỗi tài liệu thuộc hồ sơ này)
            if (configVanBan.Count > 0)
            {
                foreach (var row in vanBanRows)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var rowLabel = GetCellValue(row, CheckConstants.ColMaDinhDanhTaiLieu);
                    if (string.IsNullOrWhiteSpace(rowLabel)) rowLabel = "Dòng không có mã lưu trữ";

                    var rowErrors = new List<string>();
                    foreach (var cfg in configVanBan)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        ValidateFieldCells(cfg.TruongThongTin, cfg.MoTa, cfg.BatBuoc == ConfigConstants.BatBuoc,
                            row, CheckConstants.SheetVanBan, rowLabel, rowErrors);
                    }
                    if (rowErrors.Count > 0)
                    {
                        result.Errors.AddRange(rowErrors);
                        MarkFileError(rowLabel, rowErrors[0]); // rowLabel = mã lưu trữ = tên file (không đuôi)
                    }
                }
            }

            result.Ok = result.Errors.Count == 0;
            return result;
        }

        /// <summary>
        /// Kiểm tra 1 trường của 1 dòng đã trích ra map (bắt buộc/trống/enum), gom lỗi vào <paramref name="errors"/>.
        /// Bản dùng cho cấu trúc Excel chung (thao tác trên map thay vì IXLRow).
        /// </summary>
        public void ValidateFieldCells(
            string truongThongTin, string? moTa, bool isRequired,
            IReadOnlyDictionary<string, string> cells,
            string sheetName, string? rowLabel, List<string> errors)
        {
            var prefix = rowLabel == null ? $"Sheet '{sheetName}'" : $"File '{rowLabel}' (sheet '{sheetName}')";

            if (!cells.TryGetValue(EadFieldMapping.NormalizeHeader(truongThongTin), out var value))
            {
                if (isRequired)
                    errors.Add($"{prefix} thiếu cột bắt buộc '{truongThongTin}'.");
                return;
            }

            value = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                // Trống: bỏ qua (trường bắt buộc chỉ cần CÓ CỘT, KHÔNG bắt buộc có dữ liệu).
                return;
            }

            if (!IsFieldValueValid(value, moTa))
            {
                errors.Add($"{prefix}: trường '{truongThongTin}' có giá trị '{value}' không hợp lệ theo mô tả.");
            }
        }

        // =====================================================================
        // ============== GOM FILE CHƯA KÝ SỐ (để người dùng xem) ==============
        // =====================================================================

        public Task<CollectUnsignedResultDto> CollectUnsignedFilesAsync(CollectUnsignedRequestDto input, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation($"{nameof(CollectUnsignedFilesAsync)}, folderPath = {input?.FolderPath}");

            var folderPath = input?.FolderPath?.Trim();
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                throw new UserFriendlyException(ErrorCodes.FolderNotFound, $"Không tìm thấy thư mục hồ sơ: '{folderPath}'.");
            }

            var fileNames = new HashSet<string>(input!.FileNames ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            if (fileNames.Count == 0)
            {
                throw new UserFriendlyException(ErrorCodes.BadRequest, "Không có file chưa ký số nào để gom.");
            }

            var parentDir = CreateUnsignedParent(folderPath);
            var hoSoName = new DirectoryInfo(folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)).Name;
            var count = CopyUnsignedForHoSo(folderPath, hoSoName, fileNames, parentDir);

            return Task.FromResult(new CollectUnsignedResultDto
            {
                ParentPath = parentDir,
                HoSoCount = count > 0 ? 1 : 0,
                FileCount = count,
                Message = $"Đã gom {count} file chưa ký số."
            });
        }

        public Task<CollectUnsignedResultDto> CollectUnsignedMultiAsync(CollectUnsignedMultiRequestDto input, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation($"{nameof(CollectUnsignedMultiAsync)}, parentFolderPath = {input?.ParentFolderPath}");

            var parentPath = input?.ParentFolderPath?.Trim();
            if (string.IsNullOrWhiteSpace(parentPath) || !Directory.Exists(parentPath))
            {
                throw new UserFriendlyException(ErrorCodes.ParentFolderNotFound, $"Không tìm thấy thư mục cha: '{parentPath}'.");
            }

            var items = (input!.Items ?? new List<CollectUnsignedItemDto>())
                .Where(i => !string.IsNullOrWhiteSpace(i.HoSoName) && i.FileNames is { Count: > 0 })
                .ToList();
            if (items.Count == 0)
            {
                throw new UserFriendlyException(ErrorCodes.BadRequest, "Không có file chưa ký số nào để gom.");
            }

            var parentDir = CreateUnsignedParent(parentPath);
            var hoSoCount = 0;
            var fileCount = 0;
            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var hoSoFolder = Path.Combine(parentPath, item.HoSoName);
                if (!Directory.Exists(hoSoFolder)) continue;
                var names = new HashSet<string>(item.FileNames, StringComparer.OrdinalIgnoreCase);
                var copied = CopyUnsignedForHoSo(hoSoFolder, item.HoSoName, names, parentDir);
                if (copied > 0)
                {
                    hoSoCount++;
                    fileCount += copied;
                }
            }

            return Task.FromResult(new CollectUnsignedResultDto
            {
                ParentPath = parentDir,
                HoSoCount = hoSoCount,
                FileCount = fileCount,
                Message = $"Đã gom {fileCount} file chưa ký số của {hoSoCount} hồ sơ."
            });
        }

        /// <summary>Tạo thư mục cha "chưa ký số_{yyyyMMdd_HHmmss}" cùng cấp với thư mục đầu vào.</summary>
        private static string CreateUnsignedParent(string inputFolder)
        {
            var trimmed = inputFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var baseDir = Path.GetDirectoryName(trimmed);
            if (string.IsNullOrEmpty(baseDir)) baseDir = trimmed; // fallback (vd gốc ổ đĩa)
            var parent = Path.Combine(baseDir, $"chưa ký số_{DateTime.Now:yyyyMMdd_HHmmss}");
            Directory.CreateDirectory(parent);
            return parent;
        }

        /// <summary>Copy các file (theo tên) của 1 hồ sơ vào "{parentDir}/{hoSoName}_chưa ký số/". Trả số file đã copy.</summary>
        private static int CopyUnsignedForHoSo(string hoSoFolder, string hoSoName, HashSet<string> fileNames, string parentDir)
        {
            var matched = GetDataFiles(hoSoFolder)
                .Where(f => fileNames.Contains(Path.GetFileName(f)))
                .ToList();
            if (matched.Count == 0) return 0;

            var sub = Path.Combine(parentDir, $"{hoSoName}_chưa ký số");
            Directory.CreateDirectory(sub);
            foreach (var f in matched)
            {
                File.Copy(f, Path.Combine(sub, Path.GetFileName(f)), true);
            }
            return matched.Count;
        }

        /// <summary>File dữ liệu của 1 hồ sơ: có thư mục 'attachment' thì lấy trong đó (loại 1), ngược lại lấy trực tiếp (loại 2/3).</summary>
        private static string[] GetDataFiles(string hoSoFolder)
        {
            var attachmentDir = Directory.GetDirectories(hoSoFolder)
                .FirstOrDefault(d => string.Equals(new DirectoryInfo(d).Name, CheckConstants.AttachmentFolderName, StringComparison.OrdinalIgnoreCase));
            return attachmentDir != null ? Directory.GetFiles(attachmentDir) : Directory.GetFiles(hoSoFolder);
        }
    }
}
