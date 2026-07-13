using AutoMapper;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sip.be.Application.Base;
using Sip.be.Application.Document.Dtos.Pack;
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
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace Sip.be.Application.Document.Implements
{
    public class PackService : BaseService, IPackService
    {
        // Pattern nhận diện 1 dòng "mã: nhãn" trong mô tả config (vd "22: Công văn") -> field là enum.
        public const string OptionPattern = @"^([A-Za-z0-9]{1,4})\s*[:：]\s*(.+)$";

        // Namespace METS dùng khi sinh XML.
        public static readonly XNamespace MetsNs = SipPackConstants.NsMets;
        public static readonly XNamespace XsiNs = SipPackConstants.NsXsi;
        public static readonly XNamespace XlinkNs = SipPackConstants.NsXlink;
        public static readonly XNamespace CsipNs = SipPackConstants.NsCsip;
        public static readonly XNamespace SipNs = SipPackConstants.NsSip;

        public PackService(SipDbContext sipDbContext, ILogger<BaseService> logger, IMapper mapper) : base(sipDbContext, logger, mapper)
        {
        }

        /// <summary>
        /// 1 tài liệu trong gói (đã tính sẵn id/size/checksum) để dựng METS bản đại diện.
        /// </summary>
        public class PackDoc
        {
            public string DataFileName { get; set; } = string.Empty;   // tên file trong data/
            public string DataMime { get; set; } = string.Empty;
            public long DataSize { get; set; }
            public string DataChecksum { get; set; } = string.Empty;
            public string DataFileId { get; set; } = string.Empty;     // ID-...
            public string MetaFileName { get; set; } = string.Empty;   // EAD_DOC_*.xml
            public long MetaSize { get; set; }
            public string MetaChecksum { get; set; } = string.Empty;
            public string DmdSecId { get; set; } = string.Empty;       // uuid-...
            public string MdRefId { get; set; } = string.Empty;        // uuid-...
        }

        /// <summary>1 file schema (.xsd) đã copy vào schemas/ kèm thông tin để dựng fileSec.</summary>
        public class PackSchema
        {
            public string FileName { get; set; } = string.Empty;
            public string FileId { get; set; } = string.Empty;
            public long Size { get; set; }
            public string Checksum { get; set; } = string.Empty;
        }

        public async Task<PackResultDto> PackHoSoFolderAsync(PackHoSoFolderRequestDto input, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation($"{nameof(PackHoSoFolderAsync)}, folderPath = {input?.FolderPath}");

            var folderPath = input?.FolderPath?.Trim();
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                throw new UserFriendlyException(ErrorCodes.FolderNotFound, $"Không tìm thấy thư mục hồ sơ: '{folderPath}'.");
            }

            // ===== Định vị metadata/, attachment/, file Excel (đầu vào đã qua CheckService) =====
            var metadataDir = Directory.GetDirectories(folderPath)
                .FirstOrDefault(d => string.Equals(new DirectoryInfo(d).Name, CheckConstants.MetadataFolderName, StringComparison.OrdinalIgnoreCase));
            if (metadataDir == null)
            {
                throw new UserFriendlyException(ErrorCodes.MetadataFolderMissing, $"Thiếu thư mục '{CheckConstants.MetadataFolderName}' trong hồ sơ.");
            }

            var attachmentDir = Directory.GetDirectories(folderPath)
                .FirstOrDefault(d => string.Equals(new DirectoryInfo(d).Name, CheckConstants.AttachmentFolderName, StringComparison.OrdinalIgnoreCase));
            if (attachmentDir == null)
            {
                throw new UserFriendlyException(ErrorCodes.AttachmentFolderMissing, $"Thiếu thư mục '{CheckConstants.AttachmentFolderName}' trong hồ sơ.");
            }

            var excelPath = Directory.GetFiles(metadataDir)
                .FirstOrDefault(f => CheckConstants.ExcelExtensions.Contains(Path.GetExtension(f)));
            if (excelPath == null)
            {
                throw new UserFriendlyException(ErrorCodes.ExcelFileMissing, $"Thư mục '{CheckConstants.MetadataFolderName}' không chứa file Excel.");
            }

            // ===== Đọc Excel =====
            using var workbook = new XLWorkbook(excelPath);
            if (!workbook.Worksheets.TryGetWorksheet(CheckConstants.SheetHoSo, out var hoSoSheet))
            {
                throw new UserFriendlyException(ErrorCodes.SheetMissing, $"File Excel thiếu sheet '{CheckConstants.SheetHoSo}'.");
            }
            if (!workbook.Worksheets.TryGetWorksheet(CheckConstants.SheetVanBan, out var vanBanSheet))
            {
                throw new UserFriendlyException(ErrorCodes.SheetMissing, $"File Excel thiếu sheet '{CheckConstants.SheetVanBan}'.");
            }

            var hoSoColumns = BuildColumnMap(hoSoSheet);
            var vanBanColumns = BuildColumnMap(vanBanSheet);
            var hoSoRow = hoSoSheet.RowsUsed().Skip(1).FirstOrDefault();
            var vanBanRows = vanBanSheet.RowsUsed().Skip(1).ToList();

            if (hoSoRow == null)
            {
                throw new UserFriendlyException(ErrorCodes.RequiredFieldEmpty, $"Sheet '{CheckConstants.SheetHoSo}' không có dòng dữ liệu.");
            }
            if (vanBanRows.Count == 0)
            {
                throw new UserFriendlyException(ErrorCodes.RequiredFieldEmpty, $"Sheet '{CheckConstants.SheetVanBan}' không có dòng dữ liệu.");
            }

            // ===== Lấy cấu hình mô tả từ 2 bảng config (nguồn map trường) =====
            var configHoSo = await _sipDbContext.ConfigHoSo.Where(x => !x.Deleted).OrderBy(x => x.Id).ToListAsync(cancellationToken);
            var configVanBan = await _sipDbContext.ConfigVanBan.Where(x => !x.Deleted).OrderBy(x => x.Id).ToListAsync(cancellationToken);
            if (configHoSo.Count == 0 || configVanBan.Count == 0)
            {
                throw new UserFriendlyException(ErrorCodes.ConfigEmpty, "Chưa có cấu hình mô tả (ConfigHoSo/ConfigVanBan) trong hệ thống để map trường.");
            }

            var fileCode = ReadCell(hoSoRow, hoSoColumns, CheckConstants.ColMaHoSo);
            if (string.IsNullOrWhiteSpace(fileCode))
            {
                throw new UserFriendlyException(ErrorCodes.RequiredFieldEmpty, $"Sheet '{CheckConstants.SheetHoSo}' thiếu '{CheckConstants.ColMaHoSo}'.");
            }
            var title = ReadCell(hoSoRow, hoSoColumns, "Tiêu đề hồ sơ");

            // ===== Tạo cây thư mục gói SIP (tên = OBJID) =====
            var objid = NewUuid();
            var outputRoot = string.IsNullOrWhiteSpace(input.OutputPath)
                ? Path.GetDirectoryName(folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))!
                : input.OutputPath!;
            var packageDir = Path.Combine(outputRoot, objid);
            var rootMetadataDir = Path.Combine(packageDir, SipPackConstants.MetadataDir, SipPackConstants.DescriptiveDir);
            var schemasDir = Path.Combine(packageDir, SipPackConstants.SchemasDir);
            var rep1Dir = Path.Combine(packageDir, SipPackConstants.RepresentationsDir, SipPackConstants.Rep1Dir);
            var repMetadataDir = Path.Combine(rep1Dir, SipPackConstants.MetadataDir, SipPackConstants.DescriptiveDir);
            var dataDir = Path.Combine(rep1Dir, SipPackConstants.DataDir);

            Directory.CreateDirectory(rootMetadataDir);
            Directory.CreateDirectory(schemasDir);
            Directory.CreateDirectory(repMetadataDir);
            Directory.CreateDirectory(dataDir);

            var created = FormatDateTime(DateTimeOffset.Now);
            var warnings = new List<string>();

            // ===== EAD.xml (hồ sơ) =====
            var eadPath = Path.Combine(rootMetadataDir, SipPackConstants.EadFileName);
            var eadXml = BuildSimpleDc(hoSoRow, hoSoColumns, configHoSo.Select(c => (c.TruongThongTin, c.MoTa)).ToList(), EadFieldMapping.HoSo);
            WriteXmlDocument(eadPath, new XDocument(new XDeclaration("1.0", "UTF-8", null), eadXml));
            var eadDmdSecId = NewUuid();
            var eadMdRefId = NewUuid();
            var eadSize = new FileInfo(eadPath).Length;
            var eadChecksum = ComputeSha256(eadPath);

            // ===== Mỗi tài liệu: EAD_DOC_*.xml + copy file data =====
            var attachmentFiles = Directory.GetFiles(attachmentDir);
            var docs = new List<PackDoc>();
            foreach (var row in vanBanRows)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Khớp file đính kèm theo MÃ ĐỊNH DANH tài liệu (= tên file), KHÔNG theo mã lưu trữ (docCode có thể
                // khác tên file). Data = {docId}.pdf, metadata = EAD_DOC_{docId}.xml (theo gói mẫu thật). docCode chỉ
                // là giá trị field <docCode> bên trong XML (do BuildSimpleDc sinh từ cột "Mã lưu trữ của tài liệu").
                var docId = ReadCell(row, vanBanColumns, CheckConstants.ColMaDinhDanhTaiLieu);
                if (string.IsNullOrWhiteSpace(docId))
                {
                    continue; // dòng không có mã định danh -> không gắn được file -> bỏ qua
                }

                var sourceFile = attachmentFiles.FirstOrDefault(f =>
                    string.Equals(Path.GetFileNameWithoutExtension(f), docId, StringComparison.OrdinalIgnoreCase));
                if (sourceFile == null)
                {
                    // Tài liệu khai trong sheet nhưng THIẾU file đính kèm -> bỏ qua lặng lẽ (không lỗi, không cảnh báo),
                    // vẫn đóng gói bình thường bằng dữ liệu của các file đang tồn tại.
                    continue;
                }

                var dataFileName = Path.GetFileName(sourceFile);
                var dataDestPath = Path.Combine(dataDir, dataFileName);
                File.Copy(sourceFile, dataDestPath, true);

                var metaFileName = $"{SipPackConstants.EadDocPrefix}{docId}.xml";
                var metaPath = Path.Combine(repMetadataDir, metaFileName);
                var docXml = BuildSimpleDc(row, vanBanColumns, configVanBan.Select(c => (c.TruongThongTin, c.MoTa)).ToList(), EadFieldMapping.VanBan);
                WriteXmlDocument(metaPath, new XDocument(new XDeclaration("1.0", "UTF-8", null), docXml));

                docs.Add(new PackDoc
                {
                    DataFileName = dataFileName,
                    DataMime = GetMimeType(Path.GetExtension(dataFileName)),
                    DataSize = new FileInfo(dataDestPath).Length,
                    DataChecksum = ComputeSha256(dataDestPath),
                    DataFileId = NewFileId(),
                    MetaFileName = metaFileName,
                    MetaSize = new FileInfo(metaPath).Length,
                    MetaChecksum = ComputeSha256(metaPath),
                    DmdSecId = NewUuid(),
                    MdRefId = NewUuid(),
                });
            }

            // ===== Copy schemas (.xsd) =====
            // Bỏ trống SchemaSourceDir -> dùng bộ template mặc định trong output (Sip.be.Shared/Templates/schemas).
            var schemas = new List<PackSchema>();
            var schemaSourceDir = string.IsNullOrWhiteSpace(input.SchemaSourceDir)
                ? Path.Combine(AppContext.BaseDirectory, SipPackConstants.SchemaTemplateDir, SipPackConstants.SchemaTemplateSubDir)
                : input.SchemaSourceDir.Trim();
            if (Directory.Exists(schemaSourceDir))
            {
                foreach (var xsd in Directory.GetFiles(schemaSourceDir, "*.xsd"))
                {
                    var destPath = Path.Combine(schemasDir, Path.GetFileName(xsd));
                    File.Copy(xsd, destPath, true);
                    schemas.Add(new PackSchema
                    {
                        FileName = Path.GetFileName(destPath),
                        FileId = NewFileId(),
                        Size = new FileInfo(destPath).Length,
                        Checksum = ComputeSha256(destPath),
                    });
                }
            }
            if (schemas.Count == 0)
            {
                warnings.Add("Không có thư mục schema nguồn (SchemaSourceDir) -> gói thiếu các file .xsd trong schemas/.");
            }

            // ===== METS bản đại diện (rep1) =====
            var repMetsPath = Path.Combine(rep1Dir, SipPackConstants.MetsFileName);
            var dataFileGrpId = NewUuid();
            var repMets = BuildRepMets(docs, dataFileGrpId, created, input.NguoiTao, input.MaPhong);
            WriteXmlDocument(repMetsPath, repMets);
            var repMetsFileId = NewFileId();
            var repFileGrpId = NewUuid();
            var repMetsSize = new FileInfo(repMetsPath).Length;
            var repMetsChecksum = ComputeSha256(repMetsPath);

            // ===== METS gói gốc =====
            var rootMetsPath = Path.Combine(packageDir, SipPackConstants.MetsFileName);
            var schemasFileGrpId = NewUuid();
            var rootMets = BuildRootMets(objid, title, fileCode, input.MaPhong, created,
                eadDmdSecId, eadMdRefId, eadSize, eadChecksum,
                schemas, schemasFileGrpId,
                repMetsFileId, repMetsSize, repMetsChecksum, repFileGrpId);
            WriteXmlDocument(rootMetsPath, rootMets);

            // ===== Nén ZIP =====
            // Tên ZIP theo gói thật: 1 GUID viết thường (KHÔNG prefix uuid-, KHÔNG STT), khác tên thư mục gói.
            // includeBaseDirectory = true: giải nén ra đúng thư mục gói (tên = OBJID) nằm bên trong zip.
            var zipPath = Path.Combine(outputRoot, $"{Guid.NewGuid()}.zip");
            if (File.Exists(zipPath)) File.Delete(zipPath);
            ZipFile.CreateFromDirectory(packageDir, zipPath, CompressionLevel.Optimal, true);

            // Chỉ giữ file ZIP -> xoá thư mục gói đã giải nén.
            Directory.Delete(packageDir, true);

            return new PackResultDto
            {
                Objid = objid,
                FileCode = fileCode,
                ZipPath = zipPath,
                TotalDoc = docs.Count,
                Warnings = warnings,
                Message = warnings.Count == 0
                    ? "Đóng gói SIP thành công."
                    : "Đóng gói SIP hoàn tất kèm cảnh báo, xem Warnings.",
            };
        }

        // ===================== HÀM HỖ TRỢ (public, không dùng private) =====================

        /// <summary>Map header (đã trim) -> số cột của 1 sheet.</summary>
        public Dictionary<string, int> BuildColumnMap(IXLWorksheet sheet)
        {
            var header = sheet.FirstRowUsed();
            if (header == null) return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            return header.CellsUsed()
                .Where(c => !string.IsNullOrWhiteSpace(c.GetString()))
                .GroupBy(c => EadFieldMapping.NormalizeHeader(c.GetString()), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Address.ColumnNumber, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Đọc giá trị 1 ô theo header (trống nếu không có cột).</summary>
        public string ReadCell(IXLRow row, Dictionary<string, int> columns, string header)
        {
            return columns.TryGetValue(EadFieldMapping.NormalizeHeader(header), out var col) ? row.Cell(col).GetString().Trim() : string.Empty;
        }

        /// <summary>Mô tả config có phải danh sách "mã: nhãn" (>=2 dòng) hay không.</summary>
        public bool IsEnumDescription(string? moTa)
        {
            var count = (moTa ?? string.Empty).Split('\n')
                .Select(line => Regex.Match(line.Trim(), OptionPattern))
                .Count(m => m.Success);
            return count >= 2;
        }

        /// <summary>
        /// Dựng phần tử &lt;simpledc&gt; của EAD từ 1 dòng dữ liệu + cấu hình trường.
        /// Trường enum (theo mô tả config) chỉ lấy phần mã ("01: Vĩnh viễn" -> "01").
        /// Luôn sinh đủ phần tử (kể cả rỗng) để khớp schema.
        /// </summary>
        public XElement BuildSimpleDc(IXLRow row, Dictionary<string, int> columns,
            IReadOnlyList<(string Truong, string MoTa)> fields, IReadOnlyDictionary<string, string> map)
        {
            var simpledc = new XElement("simpledc");
            foreach (var (truong, moTa) in fields)
            {
                if (!map.TryGetValue(EadFieldMapping.NormalizeHeader(truong), out var element)) continue;
                var raw = ReadCell(row, columns, truong);
                var value = IsEnumDescription(moTa) ? EadFieldMapping.TachMa(raw) : raw;
                simpledc.Add(new XElement(element, value));
            }
            return simpledc;
        }

        /// <summary>Đọc giá trị 1 ô theo header từ map dòng đã trích (trống nếu không có cột). Bản cells cho cấu trúc Excel chung.</summary>
        public string ReadCellFromCells(IReadOnlyDictionary<string, string> cells, string header)
        {
            return cells.TryGetValue(EadFieldMapping.NormalizeHeader(header), out var value) ? (value ?? string.Empty).Trim() : string.Empty;
        }

        /// <summary>
        /// Bản cells của <see cref="BuildSimpleDc"/>: dựng &lt;simpledc&gt; từ 1 dòng đã trích ra map (header chuẩn hoá
        /// -> giá trị) thay vì IXLRow. Dùng cho cấu trúc Excel dùng CHUNG (loại 2/3) — an toàn khi chạy song song
        /// (IXLRow của ClosedXML không thread-safe). Trường enum chỉ lấy phần mã; luôn sinh đủ phần tử.
        /// </summary>
        public XElement BuildSimpleDcFromCells(IReadOnlyDictionary<string, string> cells,
            IReadOnlyList<(string Truong, string MoTa)> fields, IReadOnlyDictionary<string, string> map)
        {
            var simpledc = new XElement("simpledc");
            foreach (var (truong, moTa) in fields)
            {
                if (!map.TryGetValue(EadFieldMapping.NormalizeHeader(truong), out var element)) continue;
                var raw = ReadCellFromCells(cells, truong);
                var value = IsEnumDescription(moTa) ? EadFieldMapping.TachMa(raw) : raw;
                simpledc.Add(new XElement(element, value));
            }
            return simpledc;
        }

        /// <summary>Sinh ID dạng uuid-{GUID viết hoa}.</summary>
        public string NewUuid() => SipPackConstants.UuidPrefix + Guid.NewGuid().ToString().ToUpperInvariant();

        /// <summary>Sinh ID file dạng ID-{GUID viết hoa}.</summary>
        public string NewFileId() => SipPackConstants.FileIdPrefix + Guid.NewGuid().ToString().ToUpperInvariant();

        /// <summary>Định dạng datetime theo METS: yyyy-MM-ddTHH:mm:ss.fffzzz.</summary>
        public string FormatDateTime(DateTimeOffset value) => value.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz");

        /// <summary>Tính SHA-256 của 1 file, trả về hex viết hoa (không dấu gạch).</summary>
        public string ComputeSha256(string filePath)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            return Convert.ToHexString(sha.ComputeHash(stream));
        }

        /// <summary>Suy ra mimetype từ phần mở rộng file (theo danh mục TT05).</summary>
        public string GetMimeType(string extension)
        {
            switch ((extension ?? string.Empty).ToLowerInvariant())
            {
                case ".pdf": return "application/pdf";
                case ".jpg":
                case ".jpeg": return "image/jpeg";
                case ".png": return "image/png";
                case ".gif": return "image/gif";
                case ".tif":
                case ".tiff": return "image/tiff";
                case ".mp3": return "audio/mpeg";
                case ".wma": return "audio/x-ms-wma";
                case ".aac": return "audio/aac";
                case ".avi": return "video/x-msvideo";
                case ".wmv": return "video/x-ms-wmv";
                case ".mov":
                case ".qt": return "video/quicktime";
                case ".mpeg":
                case ".mpg":
                case ".mp4": return "video/mpeg";
                default: return SipPackConstants.MimeTypeOctetStream;
            }
        }

        /// <summary>Ghi XDocument ra file UTF-8 (không BOM), có thụt lề.</summary>
        public void WriteXmlDocument(string path, XDocument doc)
        {
            var settings = new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false),
                Indent = true,
            };
            using var writer = XmlWriter.Create(path, settings);
            doc.Save(writer);
        }

        /// <summary>Dựng 1 phần tử &lt;file&gt; trong fileSec.</summary>
        public XElement BuildFileElement(string id, string mime, long size, string created, string checksum, string href)
        {
            return new XElement(MetsNs + "file",
                new XAttribute("ID", id),
                new XAttribute("MIMETYPE", mime),
                new XAttribute("SIZE", size),
                new XAttribute("CREATED", created),
                new XAttribute("CHECKSUM", checksum),
                new XAttribute("CHECKSUMTYPE", SipPackConstants.ChecksumType),
                new XElement(MetsNs + "FLocat",
                    new XAttribute(XlinkNs + "type", SipPackConstants.XlinkTypeSimple),
                    new XAttribute(XlinkNs + "href", href),
                    new XAttribute("LOCTYPE", SipPackConstants.LocType)));
        }

        /// <summary>Dựng 1 phần tử &lt;dmdSec&gt; trỏ tới 1 file metadata.</summary>
        public XElement BuildDmdSec(string dmdSecId, string mdRefId, string href, long size, string checksum, string created)
        {
            return new XElement(MetsNs + "dmdSec",
                new XAttribute("ID", dmdSecId),
                new XAttribute("CREATED", created),
                new XAttribute("STATUS", SipPackConstants.DmdStatusCurrent),
                new XElement(MetsNs + "mdRef",
                    new XAttribute("ID", mdRefId),
                    new XAttribute("LOCTYPE", SipPackConstants.LocType),
                    new XAttribute("MDTYPE", SipPackConstants.MdType),
                    new XAttribute("MDTYPEVERSION", SipPackConstants.MdTypeVersion),
                    new XAttribute(XlinkNs + "type", SipPackConstants.XlinkTypeSimple),
                    new XAttribute(XlinkNs + "href", href),
                    new XAttribute("MIMETYPE", SipPackConstants.MimeTypeXml),
                    new XAttribute("SIZE", size),
                    new XAttribute("CREATED", created),
                    new XAttribute("CHECKSUM", checksum),
                    new XAttribute("CHECKSUMTYPE", SipPackConstants.ChecksumType)));
        }

        /// <summary>Tạo phần tử gốc &lt;mets&gt; với đầy đủ namespace/PROFILE.</summary>
        public XElement CreateMetsRoot(string objid, string label, string schemaLocation)
        {
            return new XElement(MetsNs + "mets",
                new XAttribute(XNamespace.Xmlns + "xsi", XsiNs.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "sip", SipNs.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "csip", CsipNs.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "xlink", XlinkNs.NamespaceName),
                new XAttribute("OBJID", objid),
                new XAttribute("LABEL", label),
                new XAttribute("TYPE", SipPackConstants.MetsTypeRoot),
                new XAttribute(CsipNs + "CONTENTINFORMATIONTYPE", SipPackConstants.ContentInformationType),
                new XAttribute("PROFILE", SipPackConstants.Profile),
                new XAttribute(XsiNs + "schemaLocation", schemaLocation));
        }

        /// <summary>METS của gói gốc (root).</summary>
        public XDocument BuildRootMets(string objid, string title, string fileCode, string? maPhong, string created,
            string eadDmdSecId, string eadMdRefId, long eadSize, string eadChecksum,
            List<PackSchema> schemas, string schemasFileGrpId,
            string repMetsFileId, long repMetsSize, string repMetsChecksum, string repFileGrpId)
        {
            var mets = CreateMetsRoot(objid, title, SipPackConstants.SchemaLocationRoot);

            // metsHdr: agent phần mềm (SipPack) + altRecordID
            mets.Add(new XElement(MetsNs + "metsHdr",
                new XAttribute("CREATEDATE", created),
                new XAttribute("LASTMODDATE", created),
                new XAttribute("RECORDSTATUS", SipPackConstants.RecordStatusDefault),
                new XAttribute(CsipNs + "OAISPACKAGETYPE", SipPackConstants.OaisPackageType),
                new XElement(MetsNs + "agent",
                    new XAttribute("ROLE", SipPackConstants.AgentRole),
                    new XAttribute("TYPE", SipPackConstants.AgentTypeSoftware),
                    new XAttribute("OTHERTYPE", SipPackConstants.AgentOtherType),
                    new XElement(MetsNs + "name", SipPackConstants.AgentName),
                    new XElement(MetsNs + "note",
                        new XAttribute(CsipNs + "NOTETYPE", SipPackConstants.NoteTypeIdentificationCode),
                        maPhong ?? string.Empty)),
                new XElement(MetsNs + "altRecordID",
                    new XAttribute("TYPE", SipPackConstants.AltRecordIdType),
                    fileCode)));

            // dmdSec cho EAD.xml
            mets.Add(BuildDmdSec(eadDmdSecId, eadMdRefId, $"{SipPackConstants.MetadataDir}/{SipPackConstants.DescriptiveDir}/{SipPackConstants.EadFileName}", eadSize, eadChecksum, created));

            // amdSec rỗng (theo gói thật)
            mets.Add(new XElement(MetsNs + "amdSec", new XAttribute("ID", NewUuid())));

            // fileSec: Schemas + Representations/rep1
            var schemasGrp = new XElement(MetsNs + "fileGrp",
                new XAttribute("ID", schemasFileGrpId),
                new XAttribute("USE", SipPackConstants.UseSchemas));
            foreach (var s in schemas)
            {
                schemasGrp.Add(BuildFileElement(s.FileId, SipPackConstants.MimeTypeOctetStream, s.Size, created, s.Checksum, $"{SipPackConstants.SchemasDir}/{s.FileName}"));
            }

            var repHref = $"{SipPackConstants.RepresentationsDir}/{SipPackConstants.Rep1Dir}/{SipPackConstants.MetsFileName}";
            var repGrp = new XElement(MetsNs + "fileGrp",
                new XAttribute("ID", repFileGrpId),
                new XAttribute("USE", SipPackConstants.UseRepresentations),
                BuildFileElement(repMetsFileId, SipPackConstants.MimeTypeXml, repMetsSize, created, repMetsChecksum, repHref));

            mets.Add(new XElement(MetsNs + "fileSec",
                new XAttribute("ID", NewUuid()),
                schemasGrp,
                repGrp));

            // structMap
            mets.Add(new XElement(MetsNs + "structMap",
                new XAttribute("ID", NewUuid()),
                new XAttribute("TYPE", "PHYSICAL"),
                new XAttribute("LABEL", SipPackConstants.LabelCsip),
                new XElement(MetsNs + "div",
                    new XAttribute("ID", NewUuid()),
                    new XAttribute("LABEL", objid),
                    new XElement(MetsNs + "div",
                        new XAttribute("ID", NewUuid()),
                        new XAttribute("DMDID", eadDmdSecId),
                        new XAttribute("LABEL", SipPackConstants.LabelMetadata)),
                    new XElement(MetsNs + "div",
                        new XAttribute("ID", NewUuid()),
                        new XAttribute("LABEL", SipPackConstants.UseSchemas),
                        new XElement(MetsNs + "fptr", new XAttribute("FILEID", schemasFileGrpId))),
                    new XElement(MetsNs + "div",
                        new XAttribute("ID", NewUuid()),
                        new XAttribute("LABEL", SipPackConstants.UseRepresentations),
                        new XElement(MetsNs + "mptr",
                            new XAttribute(XlinkNs + "type", SipPackConstants.XlinkTypeSimple),
                            new XAttribute(XlinkNs + "href", repHref),
                            new XAttribute(XlinkNs + "title", repFileGrpId),
                            new XAttribute("LOCTYPE", SipPackConstants.LocType))))));

            return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), mets);
        }

        /// <summary>METS của bản đại diện rep1.</summary>
        public XDocument BuildRepMets(List<PackDoc> docs, string dataFileGrpId, string created, string? nguoiTao, string? maPhong)
        {
            var mets = CreateMetsRoot(SipPackConstants.RepObjid, string.Empty, SipPackConstants.SchemaLocationRep);

            // metsHdr: agent người tạo (INDIVIDUAL)
            mets.Add(new XElement(MetsNs + "metsHdr",
                new XAttribute("CREATEDATE", created),
                new XAttribute("LASTMODDATE", created),
                new XAttribute("RECORDSTATUS", SipPackConstants.RecordStatusDefault),
                new XAttribute(CsipNs + "OAISPACKAGETYPE", SipPackConstants.OaisPackageType),
                new XElement(MetsNs + "agent",
                    new XAttribute("ROLE", SipPackConstants.AgentRole),
                    new XAttribute("TYPE", SipPackConstants.AgentTypeIndividual),
                    new XAttribute("OTHERTYPE", string.Empty),
                    new XElement(MetsNs + "name", nguoiTao ?? string.Empty),
                    new XElement(MetsNs + "note",
                        new XAttribute(CsipNs + "NOTETYPE", SipPackConstants.NoteTypeIdentificationCode),
                        maPhong ?? string.Empty))));

            // dmdSec cho từng tài liệu
            foreach (var doc in docs)
            {
                mets.Add(BuildDmdSec(doc.DmdSecId, doc.MdRefId,
                    $"{SipPackConstants.MetadataDir}/{SipPackConstants.DescriptiveDir}/{doc.MetaFileName}",
                    doc.MetaSize, doc.MetaChecksum, created));
            }

            // fileSec: nhóm Data
            var dataGrp = new XElement(MetsNs + "fileGrp",
                new XAttribute("ID", dataFileGrpId),
                new XAttribute("USE", SipPackConstants.UseData));
            foreach (var doc in docs)
            {
                dataGrp.Add(BuildFileElement(doc.DataFileId, doc.DataMime, doc.DataSize, created, doc.DataChecksum, $"{SipPackConstants.DataDir}/{doc.DataFileName}"));
            }
            mets.Add(new XElement(MetsNs + "fileSec", new XAttribute("ID", NewUuid()), dataGrp));

            // structMap: rep1 > MetadataLink + Data
            var metadataLink = new XElement(MetsNs + "div",
                new XAttribute("ID", NewUuid()),
                new XAttribute("LABEL", SipPackConstants.LabelMetadataLink));
            foreach (var doc in docs)
            {
                metadataLink.Add(new XElement(MetsNs + "div",
                    new XAttribute("ID", NewUuid()),
                    new XAttribute("DMDID", doc.DmdSecId),
                    new XAttribute("LABEL", SipPackConstants.LabelMetadataLinkFile),
                    new XElement(MetsNs + "fptr", new XAttribute("FILEID", doc.DataFileId))));
            }

            mets.Add(new XElement(MetsNs + "structMap",
                new XAttribute("ID", NewUuid()),
                new XAttribute("TYPE", "PHYSICAL"),
                new XAttribute("LABEL", SipPackConstants.LabelCsip),
                new XElement(MetsNs + "div",
                    new XAttribute("ID", NewUuid()),
                    new XAttribute("TYPE", "ORIGINAL"),
                    new XAttribute("LABEL", SipPackConstants.Rep1Dir),
                    metadataLink,
                    new XElement(MetsNs + "div",
                        new XAttribute("ID", NewUuid()),
                        new XAttribute("LABEL", SipPackConstants.LabelData),
                        new XElement(MetsNs + "fptr", new XAttribute("FILEID", dataFileGrpId))))));

            return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), mets);
        }

        // =====================================================================
        // ============== ĐÓNG GÓI NHIỀU HỒ SƠ (multi) — luồng riêng ============
        // =====================================================================

        public async IAsyncEnumerable<MultiPackProgressDto> PackMultiFolderAsync(
            PackMultiFolderRequestDto input,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _logger.LogInformation($"{nameof(PackMultiFolderAsync)}, parentFolderPath = {input?.ParentFolderPath}");

            var parentPath = input?.ParentFolderPath?.Trim();
            if (string.IsNullOrWhiteSpace(parentPath) || !Directory.Exists(parentPath))
            {
                yield return BuildPackFatal(ErrorCodes.ParentFolderNotFound, $"Không tìm thấy thư mục cha: '{parentPath}'.");
                yield break;
            }

            var outputPath = input.OutputPath?.Trim();
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                yield return BuildPackFatal(ErrorCodes.BadRequest, "Vui lòng chọn thư mục xuất (OutputPath).");
                yield break;
            }

            // Cấu trúc Excel dùng CHUNG (loại 2/3): thư mục cha có 1 Excel chung -> đi nhánh riêng (khớp CheckService).
            // Không có Excel chung -> giữ nguyên loại 1 (mỗi hồ sơ con có metadata/ + attachment/) ở phần dưới.
            var sharedExcelPath = ResolveSharedExcelPath(parentPath);
            if (sharedExcelPath != null)
            {
                await foreach (var ev in PackMultiSharedExcelAsync(parentPath, sharedExcelPath, input, outputPath!, cancellationToken))
                {
                    yield return ev;
                }
                yield break;
            }

            var childDirs = Directory.GetDirectories(parentPath);
            if (childDirs.Length == 0)
            {
                yield return BuildPackFatal(ErrorCodes.NoHoSoInParent, "Thư mục cha không chứa hồ sơ con nào.");
                yield break;
            }

            // Allowlist: chỉ đóng gói các hồ sơ được chỉ định (thường là hồ sơ đã pass kiểm tra). Rỗng/null = gói tất cả.
            if (input.HoSoNames is { Count: > 0 })
            {
                var allow = new HashSet<string>(input.HoSoNames, StringComparer.OrdinalIgnoreCase);
                childDirs = childDirs.Where(d => allow.Contains(new DirectoryInfo(d).Name)).ToArray();
                if (childDirs.Length == 0)
                {
                    yield return BuildPackFatal(ErrorCodes.NoHoSoInParent, "Không có hồ sơ nào trong danh sách được phép đóng gói.");
                    yield break;
                }
            }

            // Thư mục cha output = OutputPath/{tên thư mục cha}_{timestamp} (subfolder riêng, không đè lần chạy trước)
            var parentName = new DirectoryInfo(parentPath).Name;
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var outputParent = Path.Combine(outputPath, $"{parentName}_{timestamp}");
            Directory.CreateDirectory(outputParent);

            // Nạp cấu hình 1 LẦN trước khi chia luồng (DbContext KHÔNG thread-safe)
            var configHoSoList = await _sipDbContext.ConfigHoSo.Where(x => !x.Deleted).OrderBy(x => x.Id).ToListAsync(cancellationToken);
            var configVanBanList = await _sipDbContext.ConfigVanBan.Where(x => !x.Deleted).OrderBy(x => x.Id).ToListAsync(cancellationToken);
            if (configHoSoList.Count == 0 || configVanBanList.Count == 0)
            {
                yield return BuildPackFatal(ErrorCodes.ConfigEmpty, "Chưa có cấu hình mô tả (ConfigHoSo/ConfigVanBan) trong hệ thống để map trường.");
                yield break;
            }

            var total = childDirs.Length;
            var completed = 0;
            var okCount = 0;
            var failed = new ConcurrentBag<string>(); // tên hồ sơ đóng gói lỗi -> trả ở event tổng kết

            var degree = input.MaxDegreeOfParallelism.GetValueOrDefault(Environment.ProcessorCount);
            if (degree < 1) degree = 1;

            // Channel gom event từ nhiều luồng về 1 stream SSE (lock-free -> tránh deadlock)
            var channel = Channel.CreateUnbounded<MultiPackProgressDto>(new UnboundedChannelOptions
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
                    await Parallel.ForEachAsync(childDirs, parallelOptions, async (folder, token) =>
                    {
                        var hoSoName = new DirectoryInfo(folder).Name;
                        MultiPackProgressDto ev;
                        try
                        {
                            // Mỗi hồ sơ: đóng gói độc lập vào thư mục cha output chung (tên objid/zip duy nhất -> không tranh chấp)
                            var result = PackOneHoSo(folder, outputParent, input.SchemaSourceDir,
                                input.MaPhong, input.NguoiTao, configHoSoList, configVanBanList, token);
                            Interlocked.Increment(ref okCount);
                            ev = new MultiPackProgressDto
                            {
                                TargetType = CheckConstants.TargetTypeHoSo,
                                HoSoName = hoSoName,
                                Status = StatusCodeE.Success,
                                Code = 200,
                                Message = "Đóng gói hồ sơ thành công.",
                                Objid = result.Objid,
                                FileCode = result.FileCode,
                                ZipPath = result.ZipPath,
                                TotalDoc = result.TotalDoc,
                                Warnings = result.Warnings.Count > 0 ? result.Warnings : null,
                                TotalHoSo = total
                            };
                        }
                        catch (OperationCanceledException)
                        {
                            throw; // huỷ toàn batch -> để Parallel lan truyền
                        }
                        catch (UserFriendlyException ex)
                        {
                            failed.Add(hoSoName);
                            ev = BuildPackError(hoSoName, ex.ErrorCode,
                                ex.MessageLocalize ?? ErrorMessages.GetMessage(ex.ErrorCode), total);
                        }
                        catch (Exception ex)
                        {
                            failed.Add(hoSoName);
                            ev = BuildPackError(hoSoName, ErrorCodes.InternalServerError, ex.Message, total);
                        }

                        var done = Interlocked.Increment(ref completed);
                        ev.CompletedHoSo = done;
                        await channel.Writer.WriteAsync(ev, token);
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

            // ============================= TỔNG KẾT =============================
            var errorCount = total - okCount;
            yield return new MultiPackProgressDto
            {
                TargetType = CheckConstants.TargetTypeSummary,
                Status = errorCount == 0 ? StatusCodeE.Success : StatusCodeE.Error,
                Code = errorCount == 0 ? 200 : ErrorCodes.CheckHasErrors,
                Message = errorCount == 0
                    ? $"Đã đóng gói thành công {total} hồ sơ."
                    : $"Đóng gói hoàn tất: {okCount}/{total} hồ sơ thành công, {errorCount} hồ sơ lỗi.",
                OutputParentPath = outputParent,
                FailedHoSo = failed.IsEmpty ? null : failed.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
                TotalHoSo = total,
                CompletedHoSo = total,
                OkCount = okCount,
                ErrorCount = errorCount,
                Completed = true
            };
        }

        /// <summary>
        /// Đóng gói 1 hồ sơ thành 1 file ZIP đặt vào <paramref name="outputRoot"/> (thư mục cha output chung).
        /// THUẦN: không truy cập DB (config truyền vào), tái dùng các helper Build*/ComputeSha256/... của luồng single
        /// -> an toàn khi gọi song song. Ném UserFriendlyException nếu hồ sơ không đóng gói được (caller bắt & báo lỗi).
        /// </summary>
        public PackResultDto PackOneHoSo(
            string folderPath,
            string outputRoot,
            string? schemaSourceDir,
            string? maPhong,
            string? nguoiTao,
            IReadOnlyList<ConfigHoSo> configHoSo,
            IReadOnlyList<ConfigVanBan> configVanBan,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                throw new UserFriendlyException(ErrorCodes.FolderNotFound, $"Không tìm thấy thư mục hồ sơ: '{folderPath}'.");
            }

            // ===== Định vị metadata/, attachment/, file Excel =====
            var metadataDir = Directory.GetDirectories(folderPath)
                .FirstOrDefault(d => string.Equals(new DirectoryInfo(d).Name, CheckConstants.MetadataFolderName, StringComparison.OrdinalIgnoreCase));
            if (metadataDir == null)
            {
                throw new UserFriendlyException(ErrorCodes.MetadataFolderMissing, $"Thiếu thư mục '{CheckConstants.MetadataFolderName}' trong hồ sơ.");
            }

            var attachmentDir = Directory.GetDirectories(folderPath)
                .FirstOrDefault(d => string.Equals(new DirectoryInfo(d).Name, CheckConstants.AttachmentFolderName, StringComparison.OrdinalIgnoreCase));
            if (attachmentDir == null)
            {
                throw new UserFriendlyException(ErrorCodes.AttachmentFolderMissing, $"Thiếu thư mục '{CheckConstants.AttachmentFolderName}' trong hồ sơ.");
            }

            var excelPath = Directory.GetFiles(metadataDir)
                .FirstOrDefault(f => CheckConstants.ExcelExtensions.Contains(Path.GetExtension(f)));
            if (excelPath == null)
            {
                throw new UserFriendlyException(ErrorCodes.ExcelFileMissing, $"Thư mục '{CheckConstants.MetadataFolderName}' không chứa file Excel.");
            }

            // ===== Đọc Excel =====
            using var workbook = new XLWorkbook(excelPath);
            if (!workbook.Worksheets.TryGetWorksheet(CheckConstants.SheetHoSo, out var hoSoSheet))
            {
                throw new UserFriendlyException(ErrorCodes.SheetMissing, $"File Excel thiếu sheet '{CheckConstants.SheetHoSo}'.");
            }
            if (!workbook.Worksheets.TryGetWorksheet(CheckConstants.SheetVanBan, out var vanBanSheet))
            {
                throw new UserFriendlyException(ErrorCodes.SheetMissing, $"File Excel thiếu sheet '{CheckConstants.SheetVanBan}'.");
            }

            var hoSoColumns = BuildColumnMap(hoSoSheet);
            var vanBanColumns = BuildColumnMap(vanBanSheet);
            var hoSoRow = hoSoSheet.RowsUsed().Skip(1).FirstOrDefault();
            var vanBanRows = vanBanSheet.RowsUsed().Skip(1).ToList();

            if (hoSoRow == null)
            {
                throw new UserFriendlyException(ErrorCodes.RequiredFieldEmpty, $"Sheet '{CheckConstants.SheetHoSo}' không có dòng dữ liệu.");
            }
            if (vanBanRows.Count == 0)
            {
                throw new UserFriendlyException(ErrorCodes.RequiredFieldEmpty, $"Sheet '{CheckConstants.SheetVanBan}' không có dòng dữ liệu.");
            }

            var fileCode = ReadCell(hoSoRow, hoSoColumns, CheckConstants.ColMaHoSo);
            if (string.IsNullOrWhiteSpace(fileCode))
            {
                throw new UserFriendlyException(ErrorCodes.RequiredFieldEmpty, $"Sheet '{CheckConstants.SheetHoSo}' thiếu '{CheckConstants.ColMaHoSo}'.");
            }
            var title = ReadCell(hoSoRow, hoSoColumns, "Tiêu đề hồ sơ");

            // ===== Tạo cây thư mục gói SIP (tên = OBJID) =====
            var objid = NewUuid();
            var packageDir = Path.Combine(outputRoot, objid);
            var rootMetadataDir = Path.Combine(packageDir, SipPackConstants.MetadataDir, SipPackConstants.DescriptiveDir);
            var schemasDir = Path.Combine(packageDir, SipPackConstants.SchemasDir);
            var rep1Dir = Path.Combine(packageDir, SipPackConstants.RepresentationsDir, SipPackConstants.Rep1Dir);
            var repMetadataDir = Path.Combine(rep1Dir, SipPackConstants.MetadataDir, SipPackConstants.DescriptiveDir);
            var dataDir = Path.Combine(rep1Dir, SipPackConstants.DataDir);

            Directory.CreateDirectory(rootMetadataDir);
            Directory.CreateDirectory(schemasDir);
            Directory.CreateDirectory(repMetadataDir);
            Directory.CreateDirectory(dataDir);

            var created = FormatDateTime(DateTimeOffset.Now);
            var warnings = new List<string>();

            // ===== EAD.xml (hồ sơ) =====
            var eadPath = Path.Combine(rootMetadataDir, SipPackConstants.EadFileName);
            var eadXml = BuildSimpleDc(hoSoRow, hoSoColumns, configHoSo.Select(c => (c.TruongThongTin, c.MoTa)).ToList(), EadFieldMapping.HoSo);
            WriteXmlDocument(eadPath, new XDocument(new XDeclaration("1.0", "UTF-8", null), eadXml));
            var eadDmdSecId = NewUuid();
            var eadMdRefId = NewUuid();
            var eadSize = new FileInfo(eadPath).Length;
            var eadChecksum = ComputeSha256(eadPath);

            // ===== Mỗi tài liệu: EAD_DOC_*.xml + copy file data =====
            var attachmentFiles = Directory.GetFiles(attachmentDir);
            var docs = new List<PackDoc>();
            foreach (var row in vanBanRows)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Khớp file đính kèm theo MÃ ĐỊNH DANH tài liệu (= tên file), KHÔNG theo mã lưu trữ (docCode có thể
                // khác tên file). Data = {docId}.pdf, metadata = EAD_DOC_{docId}.xml (theo gói mẫu thật). docCode chỉ
                // là giá trị field <docCode> bên trong XML (do BuildSimpleDc sinh từ cột "Mã lưu trữ của tài liệu").
                var docId = ReadCell(row, vanBanColumns, CheckConstants.ColMaDinhDanhTaiLieu);
                if (string.IsNullOrWhiteSpace(docId))
                {
                    continue; // dòng không có mã định danh -> không gắn được file -> bỏ qua
                }

                var sourceFile = attachmentFiles.FirstOrDefault(f =>
                    string.Equals(Path.GetFileNameWithoutExtension(f), docId, StringComparison.OrdinalIgnoreCase));
                if (sourceFile == null)
                {
                    // Tài liệu khai trong sheet nhưng THIẾU file đính kèm -> bỏ qua lặng lẽ (không lỗi, không cảnh báo),
                    // vẫn đóng gói bình thường bằng dữ liệu của các file đang tồn tại.
                    continue;
                }

                var dataFileName = Path.GetFileName(sourceFile);
                var dataDestPath = Path.Combine(dataDir, dataFileName);
                File.Copy(sourceFile, dataDestPath, true);

                var metaFileName = $"{SipPackConstants.EadDocPrefix}{docId}.xml";
                var metaPath = Path.Combine(repMetadataDir, metaFileName);
                var docXml = BuildSimpleDc(row, vanBanColumns, configVanBan.Select(c => (c.TruongThongTin, c.MoTa)).ToList(), EadFieldMapping.VanBan);
                WriteXmlDocument(metaPath, new XDocument(new XDeclaration("1.0", "UTF-8", null), docXml));

                docs.Add(new PackDoc
                {
                    DataFileName = dataFileName,
                    DataMime = GetMimeType(Path.GetExtension(dataFileName)),
                    DataSize = new FileInfo(dataDestPath).Length,
                    DataChecksum = ComputeSha256(dataDestPath),
                    DataFileId = NewFileId(),
                    MetaFileName = metaFileName,
                    MetaSize = new FileInfo(metaPath).Length,
                    MetaChecksum = ComputeSha256(metaPath),
                    DmdSecId = NewUuid(),
                    MdRefId = NewUuid(),
                });
            }

            // ===== Copy schemas (.xsd) =====
            var schemas = new List<PackSchema>();
            var resolvedSchemaDir = string.IsNullOrWhiteSpace(schemaSourceDir)
                ? Path.Combine(AppContext.BaseDirectory, SipPackConstants.SchemaTemplateDir, SipPackConstants.SchemaTemplateSubDir)
                : schemaSourceDir.Trim();
            if (Directory.Exists(resolvedSchemaDir))
            {
                foreach (var xsd in Directory.GetFiles(resolvedSchemaDir, "*.xsd"))
                {
                    var destPath = Path.Combine(schemasDir, Path.GetFileName(xsd));
                    File.Copy(xsd, destPath, true);
                    schemas.Add(new PackSchema
                    {
                        FileName = Path.GetFileName(destPath),
                        FileId = NewFileId(),
                        Size = new FileInfo(destPath).Length,
                        Checksum = ComputeSha256(destPath),
                    });
                }
            }
            if (schemas.Count == 0)
            {
                warnings.Add("Không có thư mục schema nguồn (SchemaSourceDir) -> gói thiếu các file .xsd trong schemas/.");
            }

            // ===== METS bản đại diện (rep1) =====
            var repMetsPath = Path.Combine(rep1Dir, SipPackConstants.MetsFileName);
            var dataFileGrpId = NewUuid();
            var repMets = BuildRepMets(docs, dataFileGrpId, created, nguoiTao, maPhong);
            WriteXmlDocument(repMetsPath, repMets);
            var repMetsFileId = NewFileId();
            var repFileGrpId = NewUuid();
            var repMetsSize = new FileInfo(repMetsPath).Length;
            var repMetsChecksum = ComputeSha256(repMetsPath);

            // ===== METS gói gốc =====
            var rootMetsPath = Path.Combine(packageDir, SipPackConstants.MetsFileName);
            var schemasFileGrpId = NewUuid();
            var rootMets = BuildRootMets(objid, title, fileCode, maPhong, created,
                eadDmdSecId, eadMdRefId, eadSize, eadChecksum,
                schemas, schemasFileGrpId,
                repMetsFileId, repMetsSize, repMetsChecksum, repFileGrpId);
            WriteXmlDocument(rootMetsPath, rootMets);

            // ===== Nén ZIP (tên = 1 GUID viết thường; thư mục gói tên=OBJID nằm trong zip) =====
            var zipPath = Path.Combine(outputRoot, $"{Guid.NewGuid()}.zip");
            if (File.Exists(zipPath)) File.Delete(zipPath);
            ZipFile.CreateFromDirectory(packageDir, zipPath, CompressionLevel.Optimal, true);

            // Chỉ giữ file ZIP -> xoá thư mục gói đã giải nén.
            Directory.Delete(packageDir, true);

            return new PackResultDto
            {
                Objid = objid,
                FileCode = fileCode,
                ZipPath = zipPath,
                TotalDoc = docs.Count,
                Warnings = warnings,
                Message = warnings.Count == 0
                    ? "Đóng gói SIP thành công."
                    : "Đóng gói SIP hoàn tất kèm cảnh báo, xem Warnings.",
            };
        }

        /// <summary>Dựng 1 event lỗi đóng gói cho 1 hồ sơ.</summary>
        private static MultiPackProgressDto BuildPackError(string hoSoName, int code, string message, int total)
        {
            return new MultiPackProgressDto
            {
                TargetType = CheckConstants.TargetTypeHoSo,
                HoSoName = hoSoName,
                Status = StatusCodeE.Error,
                Code = code,
                Message = "Đóng gói hồ sơ thất bại.",
                Error = message,
                TotalHoSo = total
            };
        }

        /// <summary>Dựng 1 event lỗi nghiêm trọng ở cấp batch (dừng toàn bộ, không sang bước đóng gói).</summary>
        private static MultiPackProgressDto BuildPackFatal(int code, string message)
        {
            return new MultiPackProgressDto
            {
                TargetType = CheckConstants.TargetTypeParent,
                Status = StatusCodeE.Error,
                Code = code,
                Message = message,
                Completed = true
            };
        }

        // =====================================================================
        // ===== ĐÓNG GÓI NHIỀU HỒ SƠ — CẤU TRÚC EXCEL DÙNG CHUNG (loại 2/3) =====
        // =====================================================================
        // Khớp CheckMultiSharedExcelAsync: file data nằm TRỰC TIẾP trong thư mục hồ sơ con, dùng chung 1 file Excel
        // đặt ở thư mục cha (loại 2) hoặc trong folder 'metadata' của cha (loại 3). Mỗi hồ sơ lấy dòng Ho_so
        // (khớp mã = tên thư mục) + các dòng Van_ban thuộc hồ sơ (mã lưu trữ có tiền tố "maHoSo.") từ Excel chung.

        private async IAsyncEnumerable<MultiPackProgressDto> PackMultiSharedExcelAsync(
            string parentPath,
            string sharedExcelPath,
            PackMultiFolderRequestDto input,
            string outputPath,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _logger.LogInformation($"{nameof(PackMultiSharedExcelAsync)}: Excel chung = {sharedExcelPath}");

            // Hồ sơ con = mọi thư mục con TRỪ folder 'metadata' chung (loại 3).
            var childDirs = Directory.GetDirectories(parentPath)
                .Where(d => !string.Equals(new DirectoryInfo(d).Name, CheckConstants.MetadataFolderName, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (childDirs.Length == 0)
            {
                yield return BuildPackFatal(ErrorCodes.NoHoSoInParent, "Thư mục cha không chứa hồ sơ con nào.");
                yield break;
            }

            // Allowlist: chỉ đóng gói các hồ sơ được chỉ định (thường là hồ sơ đã pass kiểm tra). Rỗng/null = gói tất cả.
            if (input.HoSoNames is { Count: > 0 })
            {
                var allow = new HashSet<string>(input.HoSoNames, StringComparer.OrdinalIgnoreCase);
                childDirs = childDirs.Where(d => allow.Contains(new DirectoryInfo(d).Name)).ToArray();
                if (childDirs.Length == 0)
                {
                    yield return BuildPackFatal(ErrorCodes.NoHoSoInParent, "Không có hồ sơ nào trong danh sách được phép đóng gói.");
                    yield break;
                }
            }

            // Thư mục cha output = OutputPath/{tên thư mục cha}_{timestamp} (subfolder riêng, không đè lần chạy trước)
            var parentName = new DirectoryInfo(parentPath).Name;
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var outputParent = Path.Combine(outputPath, $"{parentName}_{timestamp}");
            Directory.CreateDirectory(outputParent);

            // Nạp cấu hình 1 LẦN trước khi chia luồng (DbContext KHÔNG thread-safe)
            var configHoSoList = await _sipDbContext.ConfigHoSo.Where(x => !x.Deleted).OrderBy(x => x.Id).ToListAsync(cancellationToken);
            var configVanBanList = await _sipDbContext.ConfigVanBan.Where(x => !x.Deleted).OrderBy(x => x.Id).ToListAsync(cancellationToken);
            if (configHoSoList.Count == 0 || configVanBanList.Count == 0)
            {
                yield return BuildPackFatal(ErrorCodes.ConfigEmpty, "Chưa có cấu hình mô tả (ConfigHoSo/ConfigVanBan) trong hệ thống để map trường.");
                yield break;
            }

            // Đọc Excel chung 1 LẦN (tuần tự) -> trích ra memory; lỗi ở đây là cấp cha -> dừng cả batch.
            var parsed = TryParseSharedExcel(sharedExcelPath);
            if (!parsed.Ok)
            {
                yield return BuildPackFatal(parsed.Code, parsed.Message);
                yield break;
            }
            var data = parsed.Data!;

            // Map mã hồ sơ -> dòng Ho_so (đầu tiên thắng; batch đã pass check nên không còn trùng mã).
            var hoSoByCode = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in data.HoSoRows)
            {
                var ma = GetCellValue(row, CheckConstants.ColMaHoSo);
                if (string.IsNullOrWhiteSpace(ma)) continue;
                hoSoByCode.TryAdd(ma, row);
            }

            // Gán tài liệu (dòng Van_ban) về hồ sơ: mã lưu trữ có tiền tố "maHoSo." (chọn tiền tố DÀI NHẤT để tránh nhầm khi mã lồng nhau).
            var folderNames = childDirs.Select(d => new DirectoryInfo(d).Name).ToList();
            var vanBanByHoSo = folderNames.ToDictionary(n => n, _ => new List<Dictionary<string, string>>(), StringComparer.OrdinalIgnoreCase);
            foreach (var row in data.VanBanRows)
            {
                var maLuuTru = GetCellValue(row, CheckConstants.ColMaDinhDanhTaiLieu);
                if (string.IsNullOrWhiteSpace(maLuuTru)) continue;
                var owner = folderNames
                    .Where(n => maLuuTru.StartsWith(n + ".", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(n => n.Length)
                    .FirstOrDefault();
                if (owner != null) vanBanByHoSo[owner].Add(row);
            }

            var total = childDirs.Length;
            var completed = 0;
            var okCount = 0;
            var failed = new ConcurrentBag<string>(); // tên hồ sơ đóng gói lỗi -> trả ở event tổng kết

            var degree = input.MaxDegreeOfParallelism.GetValueOrDefault(Environment.ProcessorCount);
            if (degree < 1) degree = 1;

            // Channel gom event từ nhiều luồng về 1 stream SSE (lock-free -> tránh deadlock)
            var channel = Channel.CreateUnbounded<MultiPackProgressDto>(new UnboundedChannelOptions
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
                    await Parallel.ForEachAsync(childDirs, parallelOptions, async (folder, token) =>
                    {
                        var hoSoName = new DirectoryInfo(folder).Name;
                        MultiPackProgressDto ev;
                        try
                        {
                            var hoSoRow = hoSoByCode.GetValueOrDefault(hoSoName);
                            var vanBanRows = vanBanByHoSo.TryGetValue(hoSoName, out var vr) ? vr : new List<Dictionary<string, string>>();

                            var result = PackOneHoSoShared(folder, outputParent, input.SchemaSourceDir,
                                input.MaPhong, input.NguoiTao, hoSoRow, vanBanRows, configHoSoList, configVanBanList, token);
                            Interlocked.Increment(ref okCount);
                            ev = new MultiPackProgressDto
                            {
                                TargetType = CheckConstants.TargetTypeHoSo,
                                HoSoName = hoSoName,
                                Status = StatusCodeE.Success,
                                Code = 200,
                                Message = "Đóng gói hồ sơ thành công.",
                                Objid = result.Objid,
                                FileCode = result.FileCode,
                                ZipPath = result.ZipPath,
                                TotalDoc = result.TotalDoc,
                                Warnings = result.Warnings.Count > 0 ? result.Warnings : null,
                                TotalHoSo = total
                            };
                        }
                        catch (OperationCanceledException)
                        {
                            throw; // huỷ toàn batch -> để Parallel lan truyền
                        }
                        catch (UserFriendlyException ex)
                        {
                            failed.Add(hoSoName);
                            ev = BuildPackError(hoSoName, ex.ErrorCode,
                                ex.MessageLocalize ?? ErrorMessages.GetMessage(ex.ErrorCode), total);
                        }
                        catch (Exception ex)
                        {
                            failed.Add(hoSoName);
                            ev = BuildPackError(hoSoName, ErrorCodes.InternalServerError, ex.Message, total);
                        }

                        var done = Interlocked.Increment(ref completed);
                        ev.CompletedHoSo = done;
                        await channel.Writer.WriteAsync(ev, token);
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

            // ============================= TỔNG KẾT =============================
            var errorCount = total - okCount;
            yield return new MultiPackProgressDto
            {
                TargetType = CheckConstants.TargetTypeSummary,
                Status = errorCount == 0 ? StatusCodeE.Success : StatusCodeE.Error,
                Code = errorCount == 0 ? 200 : ErrorCodes.CheckHasErrors,
                Message = errorCount == 0
                    ? $"Đã đóng gói thành công {total} hồ sơ."
                    : $"Đóng gói hoàn tất: {okCount}/{total} hồ sơ thành công, {errorCount} hồ sơ lỗi.",
                OutputParentPath = outputParent,
                FailedHoSo = failed.IsEmpty ? null : failed.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
                TotalHoSo = total,
                CompletedHoSo = total,
                OkCount = okCount,
                ErrorCount = errorCount,
                Completed = true
            };
        }

        /// <summary>
        /// Đóng gói 1 hồ sơ ở cấu trúc Excel dùng CHUNG (loại 2/3): file data nằm TRỰC TIẾP trong thư mục hồ sơ;
        /// dữ liệu mô tả lấy từ dòng Ho_so (đã khớp mã = tên thư mục) + các dòng Van_ban thuộc hồ sơ (đã trích từ
        /// Excel chung). THUẦN: không truy cập DB/workbook -> an toàn khi gọi song song. Khớp file &lt;-&gt; dòng
        /// theo docId = tên file (không đuôi) = mã định danh tài liệu, giống luồng loại 1. Ném UserFriendlyException
        /// nếu hồ sơ không đóng gói được (caller bắt & báo lỗi).
        /// </summary>
        public PackResultDto PackOneHoSoShared(
            string folderPath,
            string outputRoot,
            string? schemaSourceDir,
            string? maPhong,
            string? nguoiTao,
            IReadOnlyDictionary<string, string>? hoSoRow,
            IReadOnlyList<Dictionary<string, string>> vanBanRows,
            IReadOnlyList<ConfigHoSo> configHoSo,
            IReadOnlyList<ConfigVanBan> configVanBan,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                throw new UserFriendlyException(ErrorCodes.FolderNotFound, $"Không tìm thấy thư mục hồ sơ: '{folderPath}'.");
            }
            var hoSoName = new DirectoryInfo(folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)).Name;
            if (hoSoRow == null)
            {
                throw new UserFriendlyException(ErrorCodes.RequiredFieldEmpty,
                    $"Không tìm thấy mã hồ sơ '{hoSoName}' trong sheet '{CheckConstants.SheetHoSo}' của Excel chung.");
            }

            var fileCode = ReadCellFromCells(hoSoRow, CheckConstants.ColMaHoSo);
            if (string.IsNullOrWhiteSpace(fileCode))
            {
                throw new UserFriendlyException(ErrorCodes.RequiredFieldEmpty, $"Sheet '{CheckConstants.SheetHoSo}' thiếu '{CheckConstants.ColMaHoSo}'.");
            }
            var title = ReadCellFromCells(hoSoRow, "Tiêu đề hồ sơ");

            // ===== Tạo cây thư mục gói SIP (tên = OBJID) =====
            var objid = NewUuid();
            var packageDir = Path.Combine(outputRoot, objid);
            var rootMetadataDir = Path.Combine(packageDir, SipPackConstants.MetadataDir, SipPackConstants.DescriptiveDir);
            var schemasDir = Path.Combine(packageDir, SipPackConstants.SchemasDir);
            var rep1Dir = Path.Combine(packageDir, SipPackConstants.RepresentationsDir, SipPackConstants.Rep1Dir);
            var repMetadataDir = Path.Combine(rep1Dir, SipPackConstants.MetadataDir, SipPackConstants.DescriptiveDir);
            var dataDir = Path.Combine(rep1Dir, SipPackConstants.DataDir);

            Directory.CreateDirectory(rootMetadataDir);
            Directory.CreateDirectory(schemasDir);
            Directory.CreateDirectory(repMetadataDir);
            Directory.CreateDirectory(dataDir);

            var created = FormatDateTime(DateTimeOffset.Now);
            var warnings = new List<string>();

            // ===== EAD.xml (hồ sơ) =====
            var eadPath = Path.Combine(rootMetadataDir, SipPackConstants.EadFileName);
            var eadXml = BuildSimpleDcFromCells(hoSoRow, configHoSo.Select(c => (c.TruongThongTin, c.MoTa)).ToList(), EadFieldMapping.HoSo);
            WriteXmlDocument(eadPath, new XDocument(new XDeclaration("1.0", "UTF-8", null), eadXml));
            var eadDmdSecId = NewUuid();
            var eadMdRefId = NewUuid();
            var eadSize = new FileInfo(eadPath).Length;
            var eadChecksum = ComputeSha256(eadPath);

            // ===== Mỗi tài liệu: EAD_DOC_*.xml + copy file data (file nằm TRỰC TIẾP trong thư mục hồ sơ) =====
            var dataFiles = Directory.GetFiles(folderPath);
            var docs = new List<PackDoc>();
            foreach (var row in vanBanRows)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var docId = ReadCellFromCells(row, CheckConstants.ColMaDinhDanhTaiLieu);
                if (string.IsNullOrWhiteSpace(docId))
                {
                    continue; // dòng không có mã định danh -> không gắn được file -> bỏ qua
                }

                var sourceFile = dataFiles.FirstOrDefault(f =>
                    string.Equals(Path.GetFileNameWithoutExtension(f), docId, StringComparison.OrdinalIgnoreCase));
                if (sourceFile == null)
                {
                    // Tài liệu khai trong sheet nhưng THIẾU file -> bỏ qua lặng lẽ (giống loại 1), vẫn đóng gói bình thường.
                    continue;
                }

                var dataFileName = Path.GetFileName(sourceFile);
                var dataDestPath = Path.Combine(dataDir, dataFileName);
                File.Copy(sourceFile, dataDestPath, true);

                var metaFileName = $"{SipPackConstants.EadDocPrefix}{docId}.xml";
                var metaPath = Path.Combine(repMetadataDir, metaFileName);
                var docXml = BuildSimpleDcFromCells(row, configVanBan.Select(c => (c.TruongThongTin, c.MoTa)).ToList(), EadFieldMapping.VanBan);
                WriteXmlDocument(metaPath, new XDocument(new XDeclaration("1.0", "UTF-8", null), docXml));

                docs.Add(new PackDoc
                {
                    DataFileName = dataFileName,
                    DataMime = GetMimeType(Path.GetExtension(dataFileName)),
                    DataSize = new FileInfo(dataDestPath).Length,
                    DataChecksum = ComputeSha256(dataDestPath),
                    DataFileId = NewFileId(),
                    MetaFileName = metaFileName,
                    MetaSize = new FileInfo(metaPath).Length,
                    MetaChecksum = ComputeSha256(metaPath),
                    DmdSecId = NewUuid(),
                    MdRefId = NewUuid(),
                });
            }

            // ===== Copy schemas (.xsd) =====
            var schemas = new List<PackSchema>();
            var resolvedSchemaDir = string.IsNullOrWhiteSpace(schemaSourceDir)
                ? Path.Combine(AppContext.BaseDirectory, SipPackConstants.SchemaTemplateDir, SipPackConstants.SchemaTemplateSubDir)
                : schemaSourceDir.Trim();
            if (Directory.Exists(resolvedSchemaDir))
            {
                foreach (var xsd in Directory.GetFiles(resolvedSchemaDir, "*.xsd"))
                {
                    var destPath = Path.Combine(schemasDir, Path.GetFileName(xsd));
                    File.Copy(xsd, destPath, true);
                    schemas.Add(new PackSchema
                    {
                        FileName = Path.GetFileName(destPath),
                        FileId = NewFileId(),
                        Size = new FileInfo(destPath).Length,
                        Checksum = ComputeSha256(destPath),
                    });
                }
            }
            if (schemas.Count == 0)
            {
                warnings.Add("Không có thư mục schema nguồn (SchemaSourceDir) -> gói thiếu các file .xsd trong schemas/.");
            }

            // ===== METS bản đại diện (rep1) =====
            var repMetsPath = Path.Combine(rep1Dir, SipPackConstants.MetsFileName);
            var dataFileGrpId = NewUuid();
            var repMets = BuildRepMets(docs, dataFileGrpId, created, nguoiTao, maPhong);
            WriteXmlDocument(repMetsPath, repMets);
            var repMetsFileId = NewFileId();
            var repFileGrpId = NewUuid();
            var repMetsSize = new FileInfo(repMetsPath).Length;
            var repMetsChecksum = ComputeSha256(repMetsPath);

            // ===== METS gói gốc =====
            var rootMetsPath = Path.Combine(packageDir, SipPackConstants.MetsFileName);
            var schemasFileGrpId = NewUuid();
            var rootMets = BuildRootMets(objid, title, fileCode, maPhong, created,
                eadDmdSecId, eadMdRefId, eadSize, eadChecksum,
                schemas, schemasFileGrpId,
                repMetsFileId, repMetsSize, repMetsChecksum, repFileGrpId);
            WriteXmlDocument(rootMetsPath, rootMets);

            // ===== Nén ZIP (tên = 1 GUID viết thường; thư mục gói tên=OBJID nằm trong zip) =====
            var zipPath = Path.Combine(outputRoot, $"{Guid.NewGuid()}.zip");
            if (File.Exists(zipPath)) File.Delete(zipPath);
            ZipFile.CreateFromDirectory(packageDir, zipPath, CompressionLevel.Optimal, true);

            // Chỉ giữ file ZIP -> xoá thư mục gói đã giải nén.
            Directory.Delete(packageDir, true);

            return new PackResultDto
            {
                Objid = objid,
                FileCode = fileCode,
                ZipPath = zipPath,
                TotalDoc = docs.Count,
                Warnings = warnings,
                Message = warnings.Count == 0
                    ? "Đóng gói SIP thành công."
                    : "Đóng gói SIP hoàn tất kèm cảnh báo, xem Warnings.",
            };
        }

        // ================= HÀM HỖ TRỢ CHO CẤU TRÚC EXCEL DÙNG CHUNG (khớp CheckService) =================

        /// <summary>
        /// Dò file Excel dùng CHUNG ở thư mục cha: ưu tiên Excel trong folder 'metadata' của cha (loại 3),
        /// sau đó Excel đặt trực tiếp ở thư mục cha (loại 2). Trả null nếu không có (=> cấu trúc loại 1).
        /// </summary>
        private static string? ResolveSharedExcelPath(string parentPath)
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

        /// <summary>Dữ liệu Excel chung đã trích ra memory (mỗi dòng = map header chuẩn hoá -> giá trị) để xử lý sau khi đóng workbook.</summary>
        private sealed class SharedExcelData
        {
            public List<Dictionary<string, string>> HoSoRows { get; set; } = new();
            public List<Dictionary<string, string>> VanBanRows { get; set; } = new();
        }

        /// <summary>
        /// Mở Excel chung, kiểm tra đủ 2 sheet + 2 cột khoá (Mã hồ sơ / Mã lưu trữ tài liệu), rồi trích toàn bộ dòng
        /// dữ liệu ra memory và đóng workbook ngay. Trả (Ok, Code, Message, Data) — Ok=false là lỗi cấp cha.
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
    }
}
