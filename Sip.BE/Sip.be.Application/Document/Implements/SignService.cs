using AutoMapper;
using Microsoft.Extensions.Logging;
using Sip.be.Application.Base;
using Sip.be.Application.Document.Dtos.Check;
using Sip.be.External.Documents.Dtos;
using Sip.be.Application.Document.Dtos.Sign;
using Sip.be.Application.Document.Interfaces;
using Sip.be.infratructure.Persistence;
using Sip.be.Shared.Constants.Documents;
using Sip.be.Shared.Request;
using Sip.be.Shared.Request.AppException;
using Sip.be.Shared.Request.ErrorRequest;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Sip.be.External.Documents.Interfaces;

namespace Sip.be.Application.Document.Implements
{
    public class SignService : BaseService, ISignService
    {
        // Đọc cert là logic phụ thuộc nền tảng (Windows cert store) -> nằm ở Sip.be.External.
        private readonly ICertificateProvider _certificateProvider;
        private readonly ISigningKey _signingKey;
        private readonly IPdfSigner _pdfSigner;
        private readonly ISignatureVerifier _signatureVerifier;
        // Soi định dạng (PDF/A hai lớp) cho bước gate trước ký — logic parse PDF nằm ở Sip.be.External.
        private readonly IPdfFormatInspector _pdfFormatInspector;
        // Đo khổ trang + quy tọa độ ra ô thật: dùng cho bước KIỂM vị trí, để báo trước đúng cái mà bộ ký sẽ
        // làm với từng file (chính hai cổng này cũng là thứ bộ ký dùng, nên dự báo không thể lệch).
        private readonly IPdfPageInspector _pdfPageInspector;
        private readonly ISignaturePlacementResolver _placementResolver;

        private const string PdfExtension = ".pdf";
        private const string TargetTypeManifest = "manifest";
        private const string TargetTypeFile = "file";
        private const string TargetTypeSummary = "summary";

        public SignService(SipDbContext sipDbContext, ILogger<BaseService> logger, IMapper mapper,
            ICertificateProvider certificateProvider, ISigningKey signingKey, IPdfSigner pdfSigner,
            ISignatureVerifier signatureVerifier, IPdfFormatInspector pdfFormatInspector,
            IPdfPageInspector pdfPageInspector, ISignaturePlacementResolver placementResolver)
            : base(sipDbContext, logger, mapper)
        {
            _certificateProvider = certificateProvider;
            _signingKey = signingKey;
            _pdfSigner = pdfSigner;
            _signatureVerifier = signatureVerifier;
            _pdfFormatInspector = pdfFormatInspector;
            _pdfPageInspector = pdfPageInspector;
            _placementResolver = placementResolver;
        }

        /// <inheritdoc/>
        public List<SignCertDto> GetCertificates(SignCertQueryDto query)
        {
            _logger.LogInformation($"{nameof(GetCertificates)}, onlySignable={query.OnlySignable}");

            var certs = _certificateProvider.GetCertificates();

            // Store rỗng = chưa cắm token/chưa cài cert. Báo rõ thay vì trả mảng rỗng không rõ nguyên nhân.
            if (certs.Count == 0)
            {
                throw new UserFriendlyException(ErrorCodes.CertificateNotFound,
                    "Không đọc được chứng thư số nào. Kiểm tra USB token đã cắm và middleware (VGCA PKI Manager/bit4id xPKI) đã chạy chưa.");
            }

            if (query.OnlySignable)
            {
                var signable = certs.Where(c => c.CanSign).ToList();
                if (signable.Count == 0)
                {
                    throw new UserFriendlyException(ErrorCodes.CertificateNotSignable,
                        "Có chứng thư số nhưng không cái nào dùng để ký được (thiếu khóa bí mật, hết hạn, hoặc không có quyền ký).");
                }
                return signable;
            }

            return certs;
        }

        /// <inheritdoc/>
        public SignFileListDto ListPdfFiles(string sourcePath)
        {
            _logger.LogInformation($"{nameof(ListPdfFiles)}, sourcePath={sourcePath}");

            var isFolder = Directory.Exists(sourcePath);
            if (!isFolder && !File.Exists(sourcePath))
            {
                throw new UserFriendlyException(ErrorCodes.SignSourceNotFound,
                    "Không tìm thấy file hoặc thư mục cần ký.");
            }

            var result = new SignFileListDto { SourcePath = sourcePath, IsFolder = isFolder };

            var paths = isFolder
                ? Directory.EnumerateFiles(sourcePath, "*", SearchOption.TopDirectoryOnly)
                    .Where(p => string.Equals(Path.GetExtension(p), PdfExtension, StringComparison.OrdinalIgnoreCase))
                : new[] { sourcePath }.Where(p =>
                    string.Equals(Path.GetExtension(p), PdfExtension, StringComparison.OrdinalIgnoreCase));

            // Sắp theo THỨ TỰ TỰ NHIÊN: mọi cụm chữ số trong tên được đệm 0 cho bằng bề rộng rồi mới so chuỗi,
            // nên "243.2" đứng trước "243.10" ở CẢ hai cụm số (trước và sau dấu chấm). So chuỗi thẳng thì '1'
            // < '2' nên thứ tự thành 1, 10, 11... rồi mới tới 2 — người dùng lật xem trước từng file thấy nhảy
            // cóc, và số thứ tự trong bảng ký cũng không khớp số trong tên file.
            foreach (var path in paths.OrderBy(
                p => Regex.Replace(Path.GetFileName(p), @"\d+", m => m.Value.PadLeft(12, '0')),
                StringComparer.OrdinalIgnoreCase))
            {
                var info = new FileInfo(path);

                // Đã ký sẵn hay chưa: chỉ cần biết PDF có khối chữ ký nào không. Dùng chính verifier —
                // file chưa ký trả NoTimestamp kèm HasTimestamp=false, không cần parser riêng.
                var verify = _signatureVerifier.Verify(path);

                // Đếm theo SỐ KHỐI chữ ký phát hiện được, KHÔNG theo Signatures.Count: chữ ký hỏng không decode
                // được thì không vào Signatures -> file đã ký nhưng chữ ký bị phá sẽ bị coi là "chưa ký"
                // (fail-open). Verifier không đọc nổi file cũng phải coi là ĐÃ KÝ -> fail-closed.
                var alreadySigned = verify.ReadFailed
                    || verify.SignatureBlockCount > 0
                    || verify.Signatures.Count > 0;

                result.Files.Add(new SignFileItemDto
                {
                    Name = info.Name,
                    FullPath = info.FullName,
                    SizeBytes = info.Length,
                    AlreadySigned = alreadySigned
                });
            }

            return result;
        }

        /// <inheritdoc/>
        public SignPreviewDto GetPreviewAt(string sourcePath, int index)
        {
            _logger.LogInformation($"{nameof(GetPreviewAt)}, sourcePath={sourcePath}, index={index}");

            // Dùng lại ListPdfFiles để thứ tự file ở bước xem trước và bước ký là MỘT: hai cách sắp xếp khác
            // nhau thì người dùng cấu hình trên file này mà lúc ký lại nhìn thứ tự khác.
            var list = ListPdfFiles(sourcePath);
            var total = list.Files.Count;

            var result = new SignPreviewDto
            {
                Index = index,
                Total = total,
                HasPrev = index > 0 && total > 0,
                HasNext = index >= 0 && index < total - 1
            };

            // Ngoài danh sách -> Files rỗng (không ném lỗi). FE đã khoá nút theo HasPrev/HasNext nên đây chỉ
            // là lưới an toàn cho trường hợp gọi thẳng API.
            if (index >= 0 && index < total)
            {
                result.Files.Add(list.Files[index]);
            }

            return result;
        }

        /// <inheritdoc/>
        public Stream OpenPdfForPreview(string filePath)
        {
            _logger.LogInformation($"{nameof(OpenPdfForPreview)}, filePath={filePath}");

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                throw new UserFriendlyException(ErrorCodes.SignSourceNotFound,
                    "Không tìm thấy file PDF cần xem trước.");
            }

            // Chỉ mở PDF: endpoint này trả byte thô ra trình duyệt nên phải khoá đúng loại file cần xem,
            // không để nó thành đường đọc mọi file trên máy.
            if (!string.Equals(Path.GetExtension(filePath), PdfExtension, StringComparison.OrdinalIgnoreCase))
            {
                throw new UserFriendlyException(ErrorCodes.SignSourceNotFound,
                    "Chỉ xem trước được file PDF.");
            }

            // FileShare.Read: người dùng có thể đang mở chính file này bằng trình đọc PDF khác.
            return new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        /// <inheritdoc/>
        public TimestampVerifyDto VerifySignature(string filePath)
        {
            _logger.LogInformation($"{nameof(VerifySignature)}, filePath={filePath}");

            // Chỉ chặn ở mức "file có tồn tại không" — mọi kết luận còn lại là việc của verifier.
            // Thư mục KHÔNG hợp lệ ở đây: endpoint này soi đúng 1 file, không quét cả thư mục.
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                throw new UserFriendlyException(ErrorCodes.SignSourceNotFound,
                    "Không tìm thấy file cần kiểm tra chữ ký.");
            }

            // Không lọc phần mở rộng: file không phải PDF do verifier trả NotApplicable (cảnh báo, không chặn),
            // chặn sẵn ở đây sẽ đổi ngữ nghĩa so với luồng check hồ sơ.
            return _signatureVerifier.Verify(filePath);
        }

        /// <inheritdoc/>
        public TokenVerifyDto VerifyToken(string thumbprint)
        {
            _logger.LogInformation($"{nameof(VerifyToken)}, thumbprint={thumbprint}");
            return _signingKey.Verify(thumbprint);
        }

        /// <inheritdoc/>
        public async IAsyncEnumerable<PdfFormatCheckProgressDto> CheckPdfFormatAsync(
            PdfFormatCheckRequestDto request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _logger.LogInformation($"{nameof(CheckPdfFormatAsync)}, sourcePath={request.SourcePath}");

            var isFolder = Directory.Exists(request.SourcePath);
            if (!isFolder && !File.Exists(request.SourcePath))
            {
                throw new UserFriendlyException(ErrorCodes.SignSourceNotFound,
                    "Không tìm thấy file hoặc thư mục cần kiểm tra.");
            }

            // Liệt kê TẤT CẢ file (khác ListPdfFiles): file không phải PDF phải được báo NotPdf, không bỏ qua —
            // đây là gate cho người dùng biết file nào chưa ký được.
            var files = (isFolder
                    ? Directory.EnumerateFiles(request.SourcePath, "*", SearchOption.TopDirectoryOnly)
                    : new[] { request.SourcePath })
                .OrderBy(p => p, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            if (files.Count == 0)
            {
                throw new UserFriendlyException(ErrorCodes.SignNoPdfFound,
                    "Thư mục không có file nào để kiểm tra.");
            }

            var total = files.Count;

            // ---- Event manifest: seed đủ dòng cho FE trước khi kiểm từng file ----
            yield return new PdfFormatCheckProgressDto
            {
                TargetType = TargetTypeManifest,
                Target = request.SourcePath,
                Status = StatusCodeE.Success,
                Code = 200,
                Message = $"Chuẩn bị kiểm tra định dạng {total} file.",
                Current = 0,
                Total = total,
                Files = files.Select(p => Path.GetFileName(p) ?? p).ToList()
            };

            var valid = 0;
            var invalid = 0;
            var current = 0;
            foreach (var path in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Parse PDF là CPU-bound: đẩy sang thread pool để không chặn thread request, SSE flush mượt.
                // Inspector KHÔNG bao giờ ném (nuốt lỗi -> ReadFailed) nên không cần try/catch quanh yield.
                var inspect = await Task.Run(() => _pdfFormatInspector.Inspect(path), cancellationToken);
                current++;

                var canSign = inspect.Verdict == PdfFormatVerdict.Valid;
                if (canSign) valid++; else invalid++;

                // Thông điệp tiếng Việt cho người dùng, khớp đúng verdict (chốt theo yêu cầu + TT05).
                var message = inspect.Verdict switch
                {
                    PdfFormatVerdict.Valid => "PDF/A hai lớp hợp lệ, ký được.",
                    PdfFormatVerdict.NotPdf => "Chưa thể ký số với file khác định dạng PDF.",
                    PdfFormatVerdict.NotPdfA => "File không phải PDF/A, chưa thể ký số.",
                    PdfFormatVerdict.NotTwoLayer => "PDF/A không đủ 2 lớp (ảnh scan + text OCR), chưa thể ký số.",
                    _ => "Không đọc được file, không thể ký số."
                };

                var name = Path.GetFileName(path) ?? path;
                yield return new PdfFormatCheckProgressDto
                {
                    TargetType = TargetTypeFile,
                    Target = name,
                    Status = canSign ? StatusCodeE.Success : StatusCodeE.Error,
                    Code = canSign ? 200 : ErrorCodes.PdfFormatInvalid,
                    Message = message,
                    Current = current,
                    Total = total,
                    File = new PdfFormatFileResultDto
                    {
                        FileName = name,
                        FullPath = path,
                        Verdict = inspect.Verdict,
                        CanSign = canSign,
                        Message = message,
                        PdfAPart = inspect.PdfAPart,
                        PdfAConformance = inspect.PdfAConformance
                    }
                };
            }

            // ---- Event summary: cả lô chỉ được sang bước ký khi KHÔNG còn file nào không hợp lệ ----
            yield return new PdfFormatCheckProgressDto
            {
                TargetType = TargetTypeSummary,
                Target = request.SourcePath,
                Status = invalid == 0 ? StatusCodeE.Success : StatusCodeE.Error,
                Code = invalid == 0 ? 200 : ErrorCodes.PdfFormatInvalid,
                Message = invalid == 0
                    ? $"Tất cả {total} file đều là PDF/A hai lớp, ký được."
                    : $"Kiểm xong: {valid} file ký được, {invalid} file không ký được.",
                Completed = true,
                Current = total,
                Total = total,
                Summary = new PdfFormatCheckSummaryDto
                {
                    TotalFiles = total,
                    ValidCount = valid,
                    InvalidCount = invalid
                }
            };
        }

        /// <inheritdoc/>
        public async IAsyncEnumerable<SignProgressDto> SignAsync(SignRequestDto request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _logger.LogInformation($"{nameof(SignAsync)}, thumbprint={request.Thumbprint}, sourcePath={request.SourcePath}");

            // ---- Kiểm chứng thư TRƯỚC: hỏng thì dừng ở file 0, không ký gì cả ----
            // Chứng thư hỏng làm hỏng MỌI file, nên fail-fast; còn 1 file hỏng thì chỉ mình nó (xử lý bên dưới).
            var tokenCheck = _signingKey.Verify(request.Thumbprint);
            if (!tokenCheck.Valid)
            {
                throw new UserFriendlyException(ErrorCodes.SignTokenInvalid,
                    tokenCheck.Reason ?? "Chứng thư số không dùng để ký được.");
            }

            // ---- Vị trí chữ ký: chuẩn hóa NGAY, trước khi ký file nào ----
            // Chỉ thiếu hẳn tọa độ mới là lỗi; còn khối lệch thì sửa chính khối đó (xem NormalizePlacements).
            List<PdfSignPlacementDto>? placements = null;
            if (request.UseCustomPosition)
            {
                if ((request.Positions == null || request.Positions.Count == 0)
                    && (request.FileOverrides == null || request.FileOverrides.Count == 0))
                {
                    throw new UserFriendlyException(ErrorCodes.SignPositionInvalid,
                        "Đã chọn tự đặt vị trí chữ ký nhưng không có tọa độ gửi kèm.");
                }

                placements = NormalizePlacements(request.Positions ?? new List<SignPositionDto>());
            }

            // Cấu hình RIÊNG theo từng file, tra bằng đường dẫn tuyệt đối (Windows không phân biệt hoa thường).
            // Dựng sẵn ở đây để vòng ký chỉ việc tra: cả lô vẫn đi trong MỘT request dù mỗi file một kiểu.
            var overrides = new Dictionary<string, List<PdfSignPlacementDto>>(StringComparer.OrdinalIgnoreCase);
            if (request.UseCustomPosition && request.FileOverrides != null)
            {
                foreach (var file in request.FileOverrides)
                {
                    if (string.IsNullOrWhiteSpace(file.Path)) continue;
                    overrides[Path.GetFullPath(file.Path)] = NormalizePlacements(file.Positions);
                }
            }

            // Hợp đồng của External: dựng MỘT lần rồi dùng cho cả lô — chỉ file có cấu hình riêng mới thay
            // Placements của chính nó (bên dưới, trong vòng ký).
            var signOptions = new PdfSignOptionsDto
            {
                Thumbprint = request.Thumbprint,
                Reason = request.Reason,
                Location = request.Location,
                Placements = placements,
                AllowOverlapContent = request.AllowOverlapContent
            };

            var fileList = ListPdfFiles(request.SourcePath);
            if (fileList.Files.Count == 0)
            {
                throw new UserFriendlyException(ErrorCodes.SignNoPdfFound, "Không có file PDF nào để ký.");
            }

            // ---- Ghi file đã ký CẠNH file gốc trong CHÍNH thư mục nguồn, KHÔNG tạo thư mục mới ----
            // Nguồn là thư mục -> ghi thẳng vào thư mục đó; nguồn là 1 file -> ghi vào thư mục CHA của file.
            // Giữ nguyên file gốc: file đã ký là bản MỚI "{tên gốc}_daky_{yyyyMMdd_HHmmss}{đuôi}" nên ký lỗi
            // không đụng tới bản gốc và chạy lại được. outputFolder = thư mục nguồn (nơi các file đã ký nằm kèm).
            var outputFolder = fileList.IsFolder
                ? request.SourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                : Path.GetDirectoryName(request.SourcePath)!;
            // Dấu thời gian trong tên file lấy MỘT LẦN đầu lô (VietnamNow), dùng chung cho MỌI file trong lô
            // để cùng một mốc thời gian.
            var signedStamp = VietnamNow.ToString(SigningConstants.SignedFileTimeFormat);

            var total = fileList.Files.Count;
            var channel = Channel.CreateUnbounded<SignProgressDto>();

            // Toàn bộ phần xử lý (kể cả try/catch) nằm trong task riêng vì C# không cho yield return
            // bên trong khối try có catch — iterator chỉ việc đọc channel.
            _ = Task.Run(async () =>
            {
                using var runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var done = new ConcurrentDictionary<string, byte>();
                var tokenRemoved = false;

                // ---- Job giám sát token ----
                // Rút token giữa chừng mà không có job này thì mọi file còn lại đều ném lỗi mật mã khó hiểu.
                // Probe quét store tươi, KHÔNG chạm khóa nên không bật PIN dù gọi liên tục.
                var monitor = Task.Run(async () =>
                {
                    while (!runCts.IsCancellationRequested)
                    {
                        try
                        {
                            await Task.Delay(TimeSpan.FromSeconds(SigningConstants.TokenMonitorIntervalSeconds), runCts.Token);
                        }
                        catch (OperationCanceledException) { return; }

                        if (!_signingKey.IsTokenPresent(request.Thumbprint))
                        {
                            tokenRemoved = true;
                            _signingKey.ClearCache();
                            runCts.Cancel();
                            return;
                        }
                    }
                }, CancellationToken.None);

                var success = 0;
                var skipped = 0;
                var failed = 0;

                try
                {
                    await channel.Writer.WriteAsync(new SignProgressDto
                    {
                        TargetType = TargetTypeManifest,
                        Target = outputFolder,
                        Status = StatusCodeE.Success,
                        Code = 200,
                        Message = $"Chuẩn bị ký {total} file.",
                        Current = 0,
                        Total = total,
                        Files = fileList.Files.Select(f => f.Name).ToList()
                    }, CancellationToken.None);

                    // Hàng đợi ĐỘNG: mỗi worker rảnh thì tự lấy file kế tiếp. Chia lô cố định sẽ dồn hết file
                    // nặng vào một luồng trong khi các luồng khác ngồi chơi — file PDF chênh lệch rất lớn.
                    var parallelOptions = new ParallelOptions
                    {
                        MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount,
                            SigningConstants.MinDegreeOfParallelism, SigningConstants.MaxDegreeOfParallelism),
                        CancellationToken = runCts.Token
                    };

                    await Parallel.ForEachAsync(fileList.Files, parallelOptions, async (item, ct) =>
                    {
                        // File đã ký ghi cạnh file gốc, tên "{tên gốc không đuôi}_daky_{stamp}{đuôi gốc}".
                        // outputFolder chính là thư mục chứa file gốc (nguồn) nên ghép thẳng vào đó.
                        var signedName = $"{Path.GetFileNameWithoutExtension(item.Name)}" +
                            $"{SigningConstants.SignedFileSuffix}{signedStamp}{Path.GetExtension(item.Name)}";
                        var outputPath = Path.Combine(outputFolder, signedName);
                        var fileResult = new SignFileResultDto { FileName = item.Name };

                        // ---- GATE định dạng TRƯỚC khi ký (defense-in-depth ở BE) ----
                        // Không bao giờ ký file không đạt PDF/A hai lớp, kể cả FE gọi sót. Inspector nuốt mọi
                        // lỗi -> ReadFailed nên gọi thẳng, không try/catch. Verdict != Valid -> BỎ QUA (không ký):
                        // đây KHÔNG phải lỗi ký, FE phân biệt qua cờ Skipped.
                        var inspect = _pdfFormatInspector.Inspect(item.FullPath);
                        if (inspect.Verdict != PdfFormatVerdict.Valid)
                        {
                            // Thông điệp khớp đúng verdict, giống hệt luồng kiểm định dạng (kiem-tra-dinh-dang).
                            var skipMessage = inspect.Verdict switch
                            {
                                PdfFormatVerdict.NotPdf => "Chưa thể ký số với file khác định dạng PDF.",
                                PdfFormatVerdict.NotPdfA => "File không phải PDF/A, chưa thể ký số.",
                                PdfFormatVerdict.NotTwoLayer => "PDF/A không đủ 2 lớp (ảnh scan + text OCR), chưa thể ký số.",
                                _ => "Không đọc được file, không thể ký số."
                            };
                            fileResult.Success = false;
                            fileResult.Skipped = true;
                            fileResult.Reason = skipMessage;
                            Interlocked.Increment(ref skipped);

                            done[item.Name] = 1;
                            await channel.Writer.WriteAsync(new SignProgressDto
                            {
                                TargetType = TargetTypeFile,
                                Target = item.Name,
                                Status = StatusCodeE.Error,
                                Code = ErrorCodes.PdfFormatInvalid,
                                Message = skipMessage,
                                Current = done.Count,
                                Total = total,
                                File = fileResult
                            }, CancellationToken.None);
                            return; // continue sang file kế trong Parallel.ForEachAsync
                        }

                        try
                        {
                            // File có cấu hình riêng thì dùng đúng vị trí của nó, còn lại dùng vị trí chung.
                            // Chỉ thay Placements: mọi tuỳ chọn khác (chứng thư, lý do, nơi ký) là của cả lô.
                            var fileOptions = overrides.TryGetValue(item.FullPath, out var own)
                                ? new PdfSignOptionsDto
                                {
                                    Thumbprint = signOptions.Thumbprint,
                                    Reason = signOptions.Reason,
                                    Location = signOptions.Location,
                                    Placements = own,
                                    AllowOverlapContent = signOptions.AllowOverlapContent
                                }
                                : signOptions;

                            await _pdfSigner.SignAsync(item.FullPath, outputPath, fileOptions, ct);

                            // Tự nghiệm thu: ghi được byte ra đĩa KHÔNG đồng nghĩa tạo ra chữ ký mà cổng
                            // đóng gói sẽ chấp nhận. Đem chính file vừa ký cho verifier kiểm lại.
                            var verify = _signatureVerifier.Verify(outputPath);
                            fileResult.Success = true;
                            fileResult.OutputPath = outputPath;
                            fileResult.Verdict = verify.Verdict;
                            fileResult.VerifyReason = verify.Reason;
                            fileResult.TrustedTime = verify.TrustedTime;
                            // Giờ ký tự khai của bên ký: đọc lại từ signingTime trong CMS của chính file vừa ký
                            // (verifier đã trích sẵn) -> KHÁC TrustedTime là giờ TSA cấp dấu.
                            fileResult.SignedTime = verify.SigningTime;
                            // File có thể ĐÃ có chữ ký từ trước và ta vừa ký chồng lên: hai cặp giờ cấp file ở
                            // trên chỉ nói về chữ ký mới nhất, nên trả kèm cả danh sách để người dùng thấy đủ.
                            fileResult.SignatureCount = verify.Signatures.Count;
                            fileResult.ValidSignatureCount = verify.ValidSignatureCount;
                            fileResult.Signatures = verify.Signatures;
                            Interlocked.Increment(ref success);
                        }
                        catch (Exception ex)
                        {
                            fileResult.Success = false;
                            // UserFriendlyException KHÔNG đặt Exception.Message (quy ước của repo: thông điệp
                            // nằm ở MessageLocalize) -> đọc thẳng ex.Message sẽ ra chuỗi RỖNG và người dùng
                            // thấy file trượt mà không có lý do. Các chốt chặn khi ký (file mã hóa, file bị
                            // khóa /DocMDP) ném đúng loại này.
                            fileResult.Reason = ex is BaseException be
                                ? be.MessageLocalize ?? ErrorMessages.GetMessage(be.ErrorCode)
                                : ex.Message;
                            Interlocked.Increment(ref failed);

                            // Ghi dở rồi hỏng -> xóa file rác để thư mục kết quả chỉ chứa file ký được.
                            try { if (File.Exists(outputPath)) File.Delete(outputPath); } catch { }
                        }

                        done[item.Name] = 1;
                        await channel.Writer.WriteAsync(new SignProgressDto
                        {
                            TargetType = TargetTypeFile,
                            Target = item.Name,
                            Status = fileResult.Success ? StatusCodeE.Success : StatusCodeE.Error,
                            Code = fileResult.Success ? 200 : ErrorCodes.SignFailed,
                            Message = fileResult.Success ? "Đã ký" : (fileResult.Reason ?? "Ký thất bại"),
                            Current = done.Count,
                            Total = total,
                            File = fileResult
                        }, CancellationToken.None);
                    });
                }
                catch (OperationCanceledException)
                {
                    // Rút token hoặc client hủy -> rơi vào đây; các file chưa xử lý báo lỗi ở dưới.
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"{nameof(SignAsync)} lỗi ngoài dự kiến");
                }

                // ---- File chưa kịp xử lý: báo lỗi ĐỦ, không cắt ngắn ----
                // FE đã vẽ đủ số dòng theo manifest, nên phải trả kết quả cho từng dòng thì
                // SuccessCount + FailedCount mới khớp tổng.
                var pendingReason = tokenRemoved
                    ? "Đã rút USB token khi đang ký nên dừng tiến trình."
                    : "Tiến trình ký bị dừng.";
                foreach (var item in fileList.Files.Where(f => !done.ContainsKey(f.Name)))
                {
                    failed++;
                    await channel.Writer.WriteAsync(new SignProgressDto
                    {
                        TargetType = TargetTypeFile,
                        Target = item.Name,
                        Status = StatusCodeE.Error,
                        Code = ErrorCodes.SignFailed,
                        Message = pendingReason,
                        Current = total,
                        Total = total,
                        File = new SignFileResultDto
                        {
                            FileName = item.Name,
                            Success = false,
                            Reason = pendingReason
                        }
                    }, CancellationToken.None);
                }

                await channel.Writer.WriteAsync(new SignProgressDto
                {
                    TargetType = TargetTypeSummary,
                    Target = outputFolder,
                    Status = failed == 0 ? StatusCodeE.Success : StatusCodeE.Error,
                    Code = failed == 0 ? 200 : ErrorCodes.SignFailed,
                    Message = tokenRemoved
                        ? $"Đã rút token khi đang ký. Ký thành công {success}/{total} file."
                        : $"Ký xong: {success} đã ký, {skipped} bỏ qua, {failed} lỗi.",
                    Completed = true,
                    Current = total,
                    Total = total,
                    Summary = new SignResultDto
                    {
                        OutputFolder = outputFolder,
                        TotalFiles = total,
                        SuccessCount = success,
                        SkippedCount = skipped,
                        FailedCount = failed
                    }
                }, CancellationToken.None);

                runCts.Cancel();
                await monitor;

                // Hết lô là xóa cache + nhả handle khóa: giữ lại thì lô sau vẫn báo token hợp lệ dù người
                // dùng đã rút, và lần chạy sau phải hỏi PIN lại mới đúng.
                _signingKey.ClearCache();
                channel.Writer.Complete();
            }, CancellationToken.None);

            await foreach (var progress in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return progress;
            }
        }

        /// <inheritdoc/>
        public SignPositionCheckResultDto CheckPositions(SignPositionCheckRequestDto request)
        {
            _logger.LogInformation($"{nameof(CheckPositions)}, sourcePath={request.SourcePath}");

            var fileList = ListPdfFiles(request.SourcePath);
            var shared = request.UseCustomPosition
                ? NormalizePlacements(request.Positions ?? new List<SignPositionDto>())
                : new List<PdfSignPlacementDto>();

            var overrides = new Dictionary<string, List<PdfSignPlacementDto>>(StringComparer.OrdinalIgnoreCase);
            if (request.UseCustomPosition && request.FileOverrides != null)
            {
                foreach (var file in request.FileOverrides)
                {
                    if (string.IsNullOrWhiteSpace(file.Path)) continue;
                    overrides[Path.GetFullPath(file.Path)] = NormalizePlacements(file.Positions);
                }
            }

            var result = new SignPositionCheckResultDto { TotalFiles = fileList.Files.Count };
            foreach (var item in fileList.Files)
            {
                var placements = overrides.TryGetValue(item.FullPath, out var own) ? own : shared;
                var metrics = _pdfPageInspector.Read(item.FullPath);
                var fileResult = new SignPositionFileDto
                {
                    FileName = item.Name,
                    FullPath = item.FullPath,
                    PageCount = metrics.Pages.Count,
                    // Không tự đặt vị trí -> đúng 1 khối ở góc trên-phải, y như bộ ký sẽ làm.
                    BlockCount = placements.Count > 0 ? placements.Count : 1,
                    Readable = metrics.Readable
                };

                if (!metrics.Readable)
                {
                    fileResult.Issues.Add("Không đọc được file để đo khổ trang. File hỏng hoặc không phải PDF.");
                    result.UnreadableCount++;
                    result.Files.Add(fileResult);
                    continue;
                }

                for (var i = 0; i < placements.Count; i++)
                {
                    var placement = placements[i];
                    var pageIndex = Math.Clamp(placement.PageNumber - 1, 0, metrics.Pages.Count - 1);
                    if (placement.PageNumber > metrics.Pages.Count)
                    {
                        fileResult.Issues.Add($"Khối {i + 1} đặt ở trang {placement.PageNumber} nhưng file chỉ " +
                            $"có {metrics.Pages.Count} trang, sẽ đóng vào trang {metrics.Pages.Count}.");
                    }

                    // Ô người dùng YÊU CẦU (quy thẳng tỉ lệ ra point) so với ô bộ ký SẼ đóng. Lệch nhau nghĩa là
                    // resolver đã phải chỉnh. Dùng chính resolver của bộ ký nên dự báo không thể sai khác.
                    var pageBox = metrics.Pages[pageIndex];
                    var requested = new PdfRectDto
                    {
                        X = placement.XRatio * pageBox.Width,
                        Y = pageBox.Height - placement.YRatio * pageBox.Height - placement.HeightRatio * pageBox.Height,
                        Width = placement.WidthRatio * pageBox.Width,
                        Height = placement.HeightRatio * pageBox.Height
                    };
                    var actual = _placementResolver.Resolve(pageBox, placement);

                    // Ngưỡng 0.5pt: dưới mức này là sai số làm tròn của số thực, không phải chỉnh vị trí.
                    if (Math.Abs(actual.Width - requested.Width) > 0.5 || Math.Abs(actual.Height - requested.Height) > 0.5)
                    {
                        fileResult.Issues.Add($"Khối {i + 1} nhỏ hơn mức đọc được trên khổ trang này, sẽ được " +
                            "phóng to cho vừa.");
                    }
                    else if (Math.Abs(actual.X - requested.X) > 0.5 || Math.Abs(actual.Y - requested.Y) > 0.5)
                    {
                        fileResult.Issues.Add($"Khối {i + 1} tràn ra ngoài trang {pageIndex + 1}, sẽ được dịch " +
                            "vào trong cho khỏi mất chữ ký.");
                    }
                }

                fileResult.Fitted = fileResult.Issues.Count == 0;
                if (fileResult.Fitted) result.FittedCount++;
                else result.AdjustedCount++;
                result.Files.Add(fileResult);
            }

            return result;
        }

        /// <summary>
        /// Quy tọa độ FE gửi lên thành vị trí của bộ ký, SỬA từng khối lệch thay vì đánh trượt cả lô: người
        /// dùng đặt cả chục khối mà hỏng một cái thì báo lỗi là bắt làm lại từ đầu.
        /// Dùng chung cho luồng KÝ và luồng KIỂM vị trí — hai nơi phải cho ra cùng một con số, nếu không thì
        /// mục "kiểm tra cấu hình" báo một đằng còn file ký ra một nẻo.
        /// </summary>
        private static List<PdfSignPlacementDto> NormalizePlacements(List<SignPositionDto> positions)
        {
            var result = new List<PdfSignPlacementDto>();
            foreach (var position in positions)
            {
                // Kích thước 0/âm = dữ liệu hỏng, không suy ra được ý người dùng -> quay về khổ chuẩn
                // 170x30pt quy theo trang tham chiếu; bộ ký vẫn co giãn tiếp theo khổ trang thật.
                var width = position.WidthRatio > 0
                    ? Math.Min(position.WidthRatio, 1)
                    : SigningConstants.AppearanceWidth / SigningConstants.AppearanceReferencePageWidth;
                var height = position.HeightRatio > 0
                    ? Math.Min(position.HeightRatio, 1)
                    : SigningConstants.AppearanceHeight / SigningConstants.AppearanceReferencePageHeight;

                result.Add(new PdfSignPlacementDto
                {
                    // Kẹp sàn 1: trang 0/âm là dữ liệu hỏng, nhưng ký vào trang 1 vẫn đúng ý hơn là đánh trượt
                    // cả lô. Trần để bộ ký kẹp theo số trang THẬT của từng file.
                    PageNumber = Math.Max(position.PageNumber, 1),
                    // Dịch vào trong cho khỏi tràn mép, GIỮ nguyên kích thước: chữ ký lòi ra ngoài trang là
                    // trình đọc không hiện gì cả, còn dịch vào vài phần trăm thì vẫn đúng ý người dùng.
                    XRatio = Math.Clamp(position.XRatio, 0, 1 - width),
                    YRatio = Math.Clamp(position.YRatio, 0, 1 - height),
                    WidthRatio = width,
                    HeightRatio = height
                });
            }
            return result;
        }
    }
}
