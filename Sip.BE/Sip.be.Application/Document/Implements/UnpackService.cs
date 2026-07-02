using AutoMapper;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sip.be.Application.Base;
using Sip.be.Application.Document.Dtos.Unpack;
using Sip.be.Application.Document.Interfaces;
using Sip.be.Domain.Document;
using Sip.be.Domain.Documents;
using Sip.be.infratructure.Persistence;
using Sip.be.Shared.Constants.Check;
using Sip.be.Shared.Constants.Documents;
using Sip.be.Shared.Request.AppException;
using Sip.be.Shared.Request.ErrorRequest;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Sip.be.Application.Document.Implements
{
    public class UnpackService : BaseService, IUnpackService
    {
        // Pattern nhận diện 1 dòng "mã: nhãn" trong mô tả config (vd "01: Vĩnh viễn") -> field là enum.
        public const string OptionPattern = @"^([A-Za-z0-9]{1,4})\s*[:：]\s*(.+)$";

        // Tên 2 thư mục con của hồ sơ dựng lại: viết hoa chữ đầu (khớp template gói mẫu H49.61.8.2017.01).
        private const string OutputMetadataDir = "Metadata";
        private const string OutputAttachmentDir = "Attachment";

        // Ô dữ liệu ngắn (<= ngưỡng) thì căn giữa; text dài hơn thì canh trái cho dễ đọc.
        private const int MaxCenteredTextLength = 20;

        public UnpackService(SipDbContext sipDbContext, ILogger<BaseService> logger, IMapper mapper)
            : base(sipDbContext, logger, mapper)
        {
        }

        /// <summary>1 cột của sheet Excel dựng lại: header (tiếng Việt) + tên phần tử EAD + bảng bung enum (nếu có).</summary>
        public sealed class ColumnSpec
        {
            public string Header { get; set; } = string.Empty;
            public string Element { get; set; } = string.Empty;
            public Dictionary<string, string>? EnumMap { get; set; } // mã -> "mã: nhãn"
        }

        public async Task<UnpackResultDto> UnpackOneAsync(UnpackRequestDto input, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation($"{nameof(UnpackOneAsync)}, sourcePath = {input?.SourcePath}");

            var sourcePath = input?.SourcePath?.Trim();
            if (string.IsNullOrWhiteSpace(sourcePath) || (!File.Exists(sourcePath) && !Directory.Exists(sourcePath)))
            {
                throw new UserFriendlyException(ErrorCodes.UnpackSourceNotFound, $"Không tìm thấy gói SIP nguồn: '{sourcePath}'.");
            }

            var outputPath = input.OutputPath?.Trim();
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new UserFriendlyException(ErrorCodes.BadRequest, "Vui lòng chọn thư mục xuất (OutputPath).");
            }
            Directory.CreateDirectory(outputPath);

            // Header của Excel dựng lại lấy từ config (map qua EadFieldMapping) -> bắt buộc phải có config.
            var configHoSo = await _sipDbContext.ConfigHoSo.Where(x => !x.Deleted).OrderBy(x => x.Id).ToListAsync(cancellationToken);
            var configVanBan = await _sipDbContext.ConfigVanBan.Where(x => !x.Deleted).OrderBy(x => x.Id).ToListAsync(cancellationToken);
            if (configHoSo.Count == 0 || configVanBan.Count == 0)
            {
                throw new UserFriendlyException(ErrorCodes.ConfigEmpty, "Chưa có cấu hình mô tả (ConfigHoSo/ConfigVanBan) trong hệ thống để dựng header Excel.");
            }

            return UnpackOneCore(sourcePath, outputPath, configHoSo, configVanBan, cancellationToken);
        }

        /// <summary>
        /// Dịch ngược 1 gói SIP thành thư mục hồ sơ trong <paramref name="outputRoot"/>.
        /// THUẦN: không truy cập DB (config truyền vào) -> sẵn sàng tái dùng cho luồng multi sau này.
        /// </summary>
        public UnpackResultDto UnpackOneCore(
            string sourcePath,
            string outputRoot,
            IReadOnlyList<ConfigHoSo> configHoSo,
            IReadOnlyList<ConfigVanBan> configVanBan,
            CancellationToken cancellationToken)
        {
            var isZip = File.Exists(sourcePath)
                && string.Equals(Path.GetExtension(sourcePath), ".zip", StringComparison.OrdinalIgnoreCase);

            string? tempDir = null;
            try
            {
                // ===== Xác định thư mục gói (giải nén zip vào thư mục tạm nếu cần) =====
                string searchRoot;
                if (isZip)
                {
                    tempDir = Path.Combine(Path.GetTempPath(), "SipUnpack", Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(tempDir);
                    ZipFile.ExtractToDirectory(sourcePath, tempDir);
                    searchRoot = tempDir;
                }
                else if (Directory.Exists(sourcePath))
                {
                    searchRoot = sourcePath;
                }
                else
                {
                    throw new UserFriendlyException(ErrorCodes.UnpackSourceNotFound, $"Không tìm thấy gói SIP nguồn: '{sourcePath}'.");
                }

                var packageRoot = FindPackageRoot(searchRoot)
                    ?? throw new UserFriendlyException(ErrorCodes.UnpackInvalidPackage,
                        $"Không nhận diện được gói SIP hợp lệ trong '{Path.GetFileName(sourcePath)}'.");

                cancellationToken.ThrowIfCancellationRequested();

                // ===== Đọc EAD.xml (hồ sơ) =====
                var eadPath = Path.Combine(packageRoot, SipPackConstants.MetadataDir, SipPackConstants.DescriptiveDir, SipPackConstants.EadFileName);
                if (!File.Exists(eadPath))
                {
                    throw new UserFriendlyException(ErrorCodes.UnpackInvalidPackage, $"Gói thiếu {SipPackConstants.MetadataDir}/{SipPackConstants.DescriptiveDir}/{SipPackConstants.EadFileName}.");
                }
                var eadRoot = XDocument.Load(eadPath).Root
                    ?? throw new UserFriendlyException(ErrorCodes.UnpackInvalidPackage, "EAD.xml không hợp lệ.");
                var hoSoElements = ReadEadElements(eadRoot);

                var fileCode = hoSoElements.TryGetValue(EadFieldMapping.HoSo[EadFieldMapping.NormalizeHeader(CheckConstants.ColMaHoSo)], out var fc) ? fc.Trim() : string.Empty;
                if (string.IsNullOrWhiteSpace(fileCode))
                {
                    throw new UserFriendlyException(ErrorCodes.RequiredFieldEmpty, "EAD.xml thiếu 'fileCode' để đặt tên hồ sơ.");
                }

                // ===== Đọc EAD_DOC_*.xml (từng tài liệu) =====
                var repMetadataDir = Path.Combine(packageRoot, SipPackConstants.RepresentationsDir, SipPackConstants.Rep1Dir,
                    SipPackConstants.MetadataDir, SipPackConstants.DescriptiveDir);
                var docElementsList = new List<Dictionary<string, string>>();
                if (Directory.Exists(repMetadataDir))
                {
                    foreach (var docFile in Directory.GetFiles(repMetadataDir, "*.xml").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var docRoot = XDocument.Load(docFile).Root;
                        if (docRoot != null)
                        {
                            docElementsList.Add(ReadEadElements(docRoot));
                        }
                    }
                }

                // ===== Tạo cây thư mục hồ sơ dựng lại =====
                var hoSoFolder = Path.Combine(outputRoot, fileCode);
                var metadataDir = Path.Combine(hoSoFolder, OutputMetadataDir);
                var attachmentDir = Path.Combine(hoSoFolder, OutputAttachmentDir);
                Directory.CreateDirectory(metadataDir);
                Directory.CreateDirectory(attachmentDir);

                var warnings = new List<string>();

                // ===== Ghi Excel theo template (Ho_so + Van_ban) =====
                var excelPath = Path.Combine(metadataDir, $"{fileCode}.xlsx");
                WriteExcel(excelPath, configHoSo, configVanBan, hoSoElements, docElementsList);

                // ===== Copy tài liệu: representations/rep1/data/ -> attachment/ =====
                var dataDir = Path.Combine(packageRoot, SipPackConstants.RepresentationsDir, SipPackConstants.Rep1Dir, SipPackConstants.DataDir);
                if (Directory.Exists(dataDir))
                {
                    foreach (var file in Directory.GetFiles(dataDir))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        File.Copy(file, Path.Combine(attachmentDir, Path.GetFileName(file)), true);
                    }
                }
                else
                {
                    warnings.Add($"Gói thiếu thư mục {SipPackConstants.RepresentationsDir}/{SipPackConstants.Rep1Dir}/{SipPackConstants.DataDir} -> không có tài liệu đính kèm.");
                }

                return new UnpackResultDto
                {
                    HoSoName = fileCode,
                    FolderPath = hoSoFolder,
                    TotalDoc = docElementsList.Count,
                    Tree = BuildTree(hoSoFolder),
                    Warnings = warnings,
                    Message = warnings.Count == 0 ? "Dịch ngược gói SIP thành công." : "Dịch ngược hoàn tất kèm cảnh báo, xem Warnings."
                };
            }
            finally
            {
                if (tempDir != null && Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { /* dọn dẹp best-effort */ }
                }
            }
        }

        // ===================== HÀM HỖ TRỢ =====================

        /// <summary>Đọc các phần tử con của &lt;simpledc&gt; -> map tên phần tử EAD (LocalName) sang giá trị (đã trim).</summary>
        public Dictionary<string, string> ReadEadElements(XElement simpledc)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var el in simpledc.Elements())
            {
                result[el.Name.LocalName] = (el.Value ?? string.Empty).Trim();
            }
            return result;
        }

        /// <summary>
        /// Ghi 1 file Excel với 2 sheet Ho_so (1 dòng) + Van_ban (n dòng) theo template.
        /// Cột lấy từ config (theo thứ tự): trường bắt buộc luôn có cột; trường không bắt buộc chỉ có cột khi có dữ liệu.
        /// </summary>
        public void WriteExcel(
            string excelPath,
            IReadOnlyList<ConfigHoSo> configHoSo,
            IReadOnlyList<ConfigVanBan> configVanBan,
            Dictionary<string, string> hoSoElements,
            List<Dictionary<string, string>> docElementsList)
        {
            using var workbook = new XLWorkbook();

            var hoSoColumns = BuildColumns(configHoSo.Select(c => (c.TruongThongTin, c.MoTa)), EadFieldMapping.HoSo);
            WriteSheet(workbook.Worksheets.Add(CheckConstants.SheetHoSo), hoSoColumns,
                new List<Dictionary<string, string>> { hoSoElements });

            var vanBanColumns = BuildColumns(configVanBan.Select(c => (c.TruongThongTin, c.MoTa)), EadFieldMapping.VanBan);
            WriteSheet(workbook.Worksheets.Add(CheckConstants.SheetVanBan), vanBanColumns, docElementsList);

            workbook.SaveAs(excelPath);
        }

        /// <summary>
        /// Dựng danh sách cột cho 1 sheet theo config (giữ đúng thứ tự): MỌI trường map được với EAD đều có cột,
        /// kể cả trường không bắt buộc và không có dữ liệu (để khớp template đủ header).
        /// </summary>
        public List<ColumnSpec> BuildColumns(
            IEnumerable<(string Header, string MoTa)> configs,
            IReadOnlyDictionary<string, string> headerToElement)
        {
            var columns = new List<ColumnSpec>();
            foreach (var cfg in configs)
            {
                var header = cfg.Header?.Trim();
                if (string.IsNullOrWhiteSpace(header)) continue;
                if (!headerToElement.TryGetValue(EadFieldMapping.NormalizeHeader(header), out var element)) continue; // không map được -> bỏ

                columns.Add(new ColumnSpec
                {
                    Header = header,
                    Element = element,
                    EnumMap = BuildEnumMap(cfg.MoTa)
                });
            }
            return columns;
        }

        /// <summary>
        /// Ghi header (dòng 1) + dữ liệu (từ dòng 2) vào sheet theo template: header IN ĐẬM + tự xuống dòng,
        /// dữ liệu tự xuống dòng, và bật AutoFilter (lọc giá trị) trên toàn dải. Bung mã enum "01" -> "01: nhãn".
        /// </summary>
        public void WriteSheet(IXLWorksheet sheet, List<ColumnSpec> columns, IReadOnlyList<Dictionary<string, string>> dataRows)
        {
            for (var c = 0; c < columns.Count; c++)
            {
                var cell = sheet.Cell(1, c + 1);
                cell.Value = columns[c].Header;
                cell.Style.Font.Bold = true;                                   // header in đậm
                cell.Style.Alignment.WrapText = true;                          // header dài -> xuống dòng
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;  // header căn giữa
                cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            }

            for (var r = 0; r < dataRows.Count; r++)
            {
                for (var c = 0; c < columns.Count; c++)
                {
                    var col = columns[c];
                    if (!dataRows[r].TryGetValue(col.Element, out var raw)) continue;

                    var value = (raw ?? string.Empty).Trim();
                    if (value.Length == 0) continue;
                    if (col.EnumMap != null && col.EnumMap.TryGetValue(value, out var full))
                    {
                        value = full;
                    }
                    var cell = sheet.Cell(r + 2, c + 1);
                    cell.Value = value;
                    cell.Style.Alignment.WrapText = true;                      // dữ liệu dài -> xuống dòng
                    if (value.Length <= MaxCenteredTextLength)                 // data ngắn -> căn giữa; text dài -> canh trái
                    {
                        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    }
                }
            }

            if (columns.Count > 0)
            {
                var lastRow = dataRows.Count + 1;                              // header + số dòng dữ liệu
                sheet.Range(1, 1, lastRow, columns.Count).SetAutoFilter();     // lọc giá trị trên header
                sheet.Columns(1, columns.Count).Width = 22;                    // bề rộng cố định để wrap hiển thị
            }
        }

        /// <summary>Dựng bảng bung enum (mã -> "mã: nhãn") từ mô tả config; null nếu không phải danh sách enum (&lt;2 dòng).</summary>
        public Dictionary<string, string>? BuildEnumMap(string? moTa)
        {
            var options = (moTa ?? string.Empty).Split('\n')
                .Select(line => Regex.Match(line.Trim(), OptionPattern))
                .Where(m => m.Success)
                .ToList();
            if (options.Count < 2) return null;

            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in options)
            {
                var code = m.Groups[1].Value.Trim();
                var label = m.Groups[2].Value.Trim();
                map[code] = $"{code}: {label}";
            }
            return map;
        }

        /// <summary>Tìm thư mục gốc gói SIP (chứa cả metadata/ và representations/) trong tối đa 2 cấp. Null nếu không thấy.</summary>
        public string? FindPackageRoot(string root)
        {
            if (IsPackageRoot(root)) return root;
            foreach (var dir in Directory.GetDirectories(root))
            {
                if (IsPackageRoot(dir)) return dir;
                foreach (var sub in Directory.GetDirectories(dir))
                {
                    if (IsPackageRoot(sub)) return sub;
                }
            }
            return null;
        }

        private static bool IsPackageRoot(string dir)
        {
            return Directory.Exists(Path.Combine(dir, SipPackConstants.MetadataDir))
                && Directory.Exists(Path.Combine(dir, SipPackConstants.RepresentationsDir));
        }

        /// <summary>Quét đệ quy 1 thư mục -> cây FileNodeDto (thư mục trước, file sau, sắp theo tên) cho FE hiển thị library.</summary>
        public FileNodeDto BuildTree(string path)
        {
            if (Directory.Exists(path))
            {
                var node = new FileNodeDto
                {
                    Name = new DirectoryInfo(path).Name,
                    Path = path,
                    Type = FileNodeTypes.Folder,
                    Children = new List<FileNodeDto>()
                };
                foreach (var dir in Directory.GetDirectories(path).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                {
                    node.Children.Add(BuildTree(dir));
                }
                foreach (var file in Directory.GetFiles(path).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                {
                    node.Children.Add(BuildTree(file));
                }
                return node;
            }

            return new FileNodeDto
            {
                Name = Path.GetFileName(path),
                Path = path,
                Type = FileNodeTypes.File
            };
        }
    }
}
