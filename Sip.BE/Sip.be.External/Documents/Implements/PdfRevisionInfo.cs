using Sip.be.External.Documents.Dtos;
using Sip.be.Shared.Request.AppException;
using Sip.be.Shared.Request.ErrorRequest;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace Sip.be.External.Documents.Implements
{
    /// <summary>
    /// Đọc "bản sửa đổi cuối" (revision) của một file PDF: đi ngược chuỗi xref từ startxref cuối cùng,
    /// dựng bảng vị trí object, lấy trailer (/Root, /Size, /ID) và chặn 2 trường hợp KHÔNG ký nối bản được.
    ///
    /// Vì sao phải tự đọc: chữ ký PDF phủ OFFSET BYTE TUYỆT ĐỐI, nên ký thêm bắt buộc phải nối bản
    /// (incremental update) — giữ nguyên từng byte cũ rồi ghi thêm vào cuối. Muốn nối bản thì phải biết
    /// chính xác object nào nằm ở đâu trong bản cũ. Xem .claude/be/plans/pdf-incremental-signing.plan.md step 1.
    ///
    /// Đo thực trên 14 file mẫu (file/ x13, filetestky/ x1): 100% dùng LUỒNG xref (/Type/XRef) chứ không phải
    /// bảng xref cổ điển, đều có object stream, đều không mã hóa, đều không /DocMDP. Vẫn đọc được cả 2 dạng
    /// vì kho lưu trữ có thể có file PDF đời cũ dùng bảng cổ điển.
    /// </summary>
    public sealed class PdfRevisionInfo
    {
        /// <summary>Toàn bộ byte của file gốc — bản ký nối sẽ copy nguyên si khối này rồi ghi thêm phía sau.</summary>
        public byte[] Bytes { get; private init; } = Array.Empty<byte>();

        /// <summary>Offset của xref cuối cùng (giá trị sau startxref) — sẽ thành /Prev của xref mới.</summary>
        public long LastXrefOffset { get; private init; }

        /// <summary>Xref cuối là LUỒNG (true) hay BẢNG cổ điển (false). Xref mới phải cùng dạng.</summary>
        public bool LastXrefIsStream { get; private set; }

        /// <summary>
        /// Toàn bộ file dưới dạng chuỗi Latin1 — mỗi byte thành đúng 1 ký tự nên CHỈ SỐ KÝ TỰ == CHỈ SỐ BYTE.
        /// Dựng MỘT lần: file thật tới 4,5 MB, gọi Encoding.Latin1.GetString mỗi lần đọc object là chép lại
        /// từng đó bộ nhớ cho mỗi object.
        /// </summary>
        private string Text => _text ??= Encoding.Latin1.GetString(Bytes);
        private string? _text;

        /// <summary>/Size trong trailer = số hiệu object trống kế tiếp. Object mới bắt đầu đánh số từ đây.</summary>
        public int Size { get; private init; }

        /// <summary>Số hiệu object của Catalog (/Root).</summary>
        public int RootObjectNumber { get; private init; }

        /// <summary>
        /// Nguyên văn mảng /ID của trailer, ví dụ "[&lt;7f1e...&gt;&lt;a056...&gt;]". Phải GIỮ NGUYÊN sang trailer mới:
        /// phần tử đầu của /ID là định danh vĩnh viễn của tài liệu, đổi nó là thành tài liệu khác.
        /// </summary>
        public string? IdRaw { get; private init; }

        /// <summary>
        /// Nguyên văn giá trị /Info của trailer ("24 0 R") nếu có. Chép sang trailer mới để tài liệu không
        /// mất phần thông tin mô tả (tác giả, tiêu đề...) mà trình đọc hiển thị.
        /// </summary>
        public string? InfoRaw { get; private init; }

        /// <summary>Bảng vị trí object đã gộp qua toàn chuỗi xref (bản mới đè bản cũ).</summary>
        public IReadOnlyDictionary<int, PdfObjectLocation> Objects => _objects;

        private readonly Dictionary<int, PdfObjectLocation> _objects = new();

        private PdfRevisionInfo() { }

        /// <summary>
        /// Đọc file và kiểm 2 chốt chặn fail-closed. Ném <see cref="UserFriendlyException"/> nếu:
        /// file mã hóa (/Encrypt), hoặc có chữ ký chứng thực /DocMDP /P 1 (tác giả cấm sửa tiếp).
        /// Tầng trên bắt lỗi này để đánh trượt RIÊNG file đó, cả lô vẫn chạy tiếp.
        /// </summary>
        public static PdfRevisionInfo Load(byte[] bytes)
        {
            if (bytes.Length < 32 || bytes[0] != '%' || bytes[1] != 'P')
            {
                throw new UserFriendlyException(ErrorCodes.SignPdfStructureUnsupported,
                    "File không phải PDF hợp lệ (thiếu header %PDF).");
            }

            // startxref CUỐI CÙNG trỏ tới xref của bản sửa đổi mới nhất.
            var tailStart = Math.Max(0, bytes.Length - 2048);
            var tail = Encoding.Latin1.GetString(bytes, tailStart, bytes.Length - tailStart);
            var sx = Regex.Matches(tail, @"startxref\s+(\d+)");
            if (sx.Count == 0)
            {
                throw new UserFriendlyException(ErrorCodes.SignPdfStructureUnsupported,
                    "Không tìm thấy startxref ở cuối file PDF.");
            }

            var lastXref = long.Parse(sx[^1].Groups[1].Value);
            if (lastXref <= 0 || lastXref >= bytes.Length)
            {
                throw new UserFriendlyException(ErrorCodes.SignPdfStructureUnsupported,
                    $"startxref trỏ ra ngoài file ({lastXref}/{bytes.Length}).");
            }

            var info = new PdfRevisionInfo { Bytes = bytes, LastXrefOffset = lastXref };

            // Đi ngược chuỗi /Prev. seen chặn file lỗi trỏ vòng tròn -> lặp vô hạn.
            var seen = new HashSet<long>();
            long? offset = lastXref;
            string? firstTrailer = null;
            var isFirst = true;
            while (offset is long cur && seen.Add(cur))
            {
                if (cur < 0 || cur >= bytes.Length) break;

                // Phân biệt 2 dạng: bảng cổ điển bắt đầu bằng từ khóa "xref", luồng là "N 0 obj <<...>> stream".
                var probe = Encoding.Latin1.GetString(bytes, (int)cur, Math.Min(20, bytes.Length - (int)cur)).TrimStart();
                string trailerDict;
                if (probe.StartsWith("xref"))
                {
                    trailerDict = info.ReadClassicTable(cur);
                    if (isFirst) info.LastXrefIsStream = false;
                }
                else
                {
                    trailerDict = info.ReadXrefStream(cur);
                    if (isFirst) info.LastXrefIsStream = true;
                }

                firstTrailer ??= trailerDict;
                isFirst = false;

                var prev = Regex.Match(trailerDict, @"/Prev\s+(\d+)");
                offset = prev.Success ? long.Parse(prev.Groups[1].Value) : null;
            }

            if (firstTrailer == null)
            {
                throw new UserFriendlyException(ErrorCodes.SignPdfStructureUnsupported,
                    "Không đọc được trailer của file PDF.");
            }

            // GUARD 1: file mã hóa. Ký nối bản vào file mã hóa đòi phải mã hóa cả object mới bằng đúng
            // khóa của file — ngoài phạm vi. Fail-closed: thà trượt còn hơn phát hành file hỏng.
            if (Regex.IsMatch(firstTrailer, @"/Encrypt\s"))
            {
                throw new UserFriendlyException(ErrorCodes.SignPdfEncrypted,
                    "File PDF được mã hóa (/Encrypt) nên không ký số nối bản được.");
            }

            var root = Regex.Match(firstTrailer, @"/Root\s+(\d+)\s+\d+\s+R");
            var size = Regex.Match(firstTrailer, @"/Size\s+(\d+)");
            if (!root.Success || !size.Success)
            {
                throw new UserFriendlyException(ErrorCodes.SignPdfStructureUnsupported,
                    "Trailer PDF thiếu /Root hoặc /Size.");
            }

            var id = Regex.Match(firstTrailer, @"/ID\s*(\[[^\]]*\])");
            var infoRef = Regex.Match(firstTrailer, @"/Info\s+(\d+\s+\d+\s+R)");

            var result = new PdfRevisionInfo
            {
                InfoRaw = infoRef.Success ? infoRef.Groups[1].Value : null,
                Bytes = bytes,
                LastXrefOffset = lastXref,
                LastXrefIsStream = info.LastXrefIsStream,
                RootObjectNumber = int.Parse(root.Groups[1].Value),
                Size = int.Parse(size.Groups[1].Value),
                IdRaw = id.Success ? id.Groups[1].Value : null,
            };
            foreach (var kv in info._objects) result._objects[kv.Key] = kv.Value;

            // GUARD 2: chữ ký chứng thực /DocMDP với /P 1 = "không được thay đổi gì nữa". Ký thêm vào file
            // đó là cố tình phá chữ ký chứng thực. Dict chữ ký theo chuẩn KHÔNG bao giờ nằm trong object
            // stream (phải đọc thẳng được), nên quét byte thô là đủ, không cần giải nén.
            var latin1 = result.Text;
            foreach (Match m in Regex.Matches(latin1, @"/DocMDP"))
            {
                // /P nằm trong TransformParams cùng dict chữ ký, ngay sau /DocMDP trong vài trăm byte.
                var window = latin1.Substring(m.Index, Math.Min(400, latin1.Length - m.Index));
                var p = Regex.Match(window, @"/P\s+(\d+)");
                if (p.Success && p.Groups[1].Value == "1")
                {
                    throw new UserFriendlyException(ErrorCodes.SignPdfLocked,
                        "File PDF có chữ ký chứng thực cấm mọi thay đổi (/DocMDP /P 1) nên không ký thêm được.");
                }
            }

            return result;
        }

        /// <summary>Đọc BẢNG xref cổ điển tại offset; trả về nguyên văn dict trailer đi kèm.</summary>
        private string ReadClassicTable(long offset)
        {
            var text = Text;
            var pos = (int)offset;
            var m = Regex.Match(text[pos..Math.Min(text.Length, pos + 20)], @"\s*xref\s*");
            pos += m.Length;

            // Nhiều "subsection": "<số hiệu đầu> <số lượng>" rồi các dòng 20 byte "offset gen n/f".
            while (true)
            {
                var head = Regex.Match(text[pos..Math.Min(text.Length, pos + 40)], @"^\s*(\d+)\s+(\d+)\s*");
                if (!head.Success) break;
                var start = int.Parse(head.Groups[1].Value);
                var count = int.Parse(head.Groups[2].Value);
                pos += head.Length;

                for (var i = 0; i < count; i++)
                {
                    var line = text.Substring(pos, Math.Min(20, text.Length - pos));
                    var e = Regex.Match(line, @"(\d{10})\s+(\d{5})\s+([nf])");
                    if (e.Success && e.Groups[3].Value == "n")
                    {
                        var num = start + i;
                        // Bản MỚI đè bản CŨ: đi ngược chuỗi nên gặp trước là mới hơn -> chỉ ghi khi chưa có.
                        if (!_objects.ContainsKey(num))
                        {
                            _objects[num] = new PdfObjectLocation
                            { Type = 1, Value = long.Parse(e.Groups[1].Value) };
                        }
                    }
                    pos += 20;
                }
            }

            var t = text.IndexOf("trailer", pos, StringComparison.Ordinal);
            if (t < 0) return string.Empty;
            var ds = text.IndexOf("<<", t, StringComparison.Ordinal);
            return ds < 0 ? string.Empty : ExtractDictionary(text, ds);
        }

        /// <summary>Đọc LUỒNG xref (/Type/XRef) tại offset; trả về nguyên văn dict của nó (chính là trailer).</summary>
        private string ReadXrefStream(long offset)
        {
            var text = Text;
            var ds = text.IndexOf("<<", (int)offset, StringComparison.Ordinal);
            if (ds < 0 || ds > offset + 60) return string.Empty;
            var dict = ExtractDictionary(text, ds);

            var data = ReadStreamData(text, ds + dict.Length, dict);
            if (data == null) return dict;

            var wm = Regex.Match(dict, @"/W\s*\[([^\]]*)\]");
            if (!wm.Success) return dict;
            var w = wm.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(int.Parse).ToArray();

            var sizeM = Regex.Match(dict, @"/Size\s+(\d+)");
            var size = sizeM.Success ? int.Parse(sizeM.Groups[1].Value) : 0;

            // /Index liệt kê các đoạn [số hiệu đầu, số lượng]; không có thì mặc định [0 Size].
            var idxM = Regex.Match(dict, @"/Index\s*\[([^\]]*)\]");
            var index = idxM.Success
                ? idxM.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray()
                : new[] { 0, size };

            var rowWidth = w.Sum();
            if (rowWidth == 0) return dict;
            var pos = 0;
            for (var k = 0; k + 1 < index.Length; k += 2)
            {
                for (var n = index[k]; n < index[k] + index[k + 1]; n++)
                {
                    if (pos + rowWidth > data.Length) return dict;
                    var f = new long[3];
                    var o = pos;
                    for (var c = 0; c < w.Length && c < 3; c++)
                    {
                        long v = 0;
                        for (var b = 0; b < w[c]; b++) v = (v << 8) | data[o++];
                        f[c] = v;
                    }
                    pos += rowWidth;

                    // W[0]==0 nghĩa là cột type bị lược -> mặc định type 1 (theo chuẩn).
                    var type = w[0] == 0 ? 1 : (int)f[0];
                    if (type != 1 && type != 2) continue;
                    if (!_objects.ContainsKey(n))
                    {
                        _objects[n] = new PdfObjectLocation
                        { Type = type, Value = f[1], IndexInStream = (int)f[2] };
                    }
                }
            }

            return dict;
        }

        /// <summary>
        /// Lấy nguyên văn thân object (thường là dict "&lt;&lt;...&gt;&gt;") theo số hiệu, giải nén nếu nó nằm
        /// trong object stream. Trả về null nếu không có. Dùng để ĐỌC Catalog và trang 1 trước khi ghi đè.
        /// </summary>
        public string? GetObjectBody(int number)
        {
            if (!_objects.TryGetValue(number, out var loc)) return null;
            var text = Text;

            if (loc.Type == 1)
            {
                if (loc.Value < 0 || loc.Value >= Bytes.Length) return null;
                var objKw = text.IndexOf("obj", (int)loc.Value, StringComparison.Ordinal);
                if (objKw < 0 || objKw > loc.Value + 40) return null;
                var p = objKw + 3;
                while (p < text.Length && char.IsWhiteSpace(text[p])) p++;
                if (p >= text.Length) return null;

                // Dict thì cắt theo cặp << >> (không thể cắt tới endobj vì thân stream nằm chen vào giữa).
                if (text[p] == '<' && p + 1 < text.Length && text[p + 1] == '<') return ExtractDictionary(text, p);

                // Object KHÔNG phải dict cũng phải đọc được: /Widths của font là một MẢNG object riêng,
                // bỏ sót nó thì tham chiếu tới nó không được đánh số lại và trỏ bậy sau khi cấy ghép.
                var end = text.IndexOf("endobj", p, StringComparison.Ordinal);
                return end < 0 ? null : text[p..end].Trim();
            }

            // Type 2: nằm trong object stream số hiệu loc.Value.
            if (!_objects.TryGetValue((int)loc.Value, out var stmLoc) || stmLoc.Type != 1) return null;
            var sds = text.IndexOf("<<", (int)stmLoc.Value, StringComparison.Ordinal);
            if (sds < 0) return null;
            var stmDict = ExtractDictionary(text, sds);
            var raw = ReadStreamData(text, sds + stmDict.Length, stmDict);
            if (raw == null) return null;

            var nM = Regex.Match(stmDict, @"/N\s+(\d+)");
            var firstM = Regex.Match(stmDict, @"/First\s+(\d+)");
            if (!nM.Success || !firstM.Success) return null;
            var count = int.Parse(nM.Groups[1].Value);
            var first = int.Parse(firstM.Groups[1].Value);
            if (first > raw.Length) return null;

            // Phần đầu là các cặp "số hiệu offset" (offset tính từ /First).
            var header = Encoding.Latin1.GetString(raw, 0, first)
                .Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var body = Encoding.Latin1.GetString(raw);
            for (var i = 0; i + 1 < header.Length && i / 2 < count; i += 2)
            {
                if (int.Parse(header[i]) != number) continue;
                var start = first + int.Parse(header[i + 1]);
                var end = i + 3 < header.Length ? first + int.Parse(header[i + 3]) : raw.Length;
                if (start < 0 || end > raw.Length || start >= end) return null;
                var slice = body[start..end];
                var d = slice.IndexOf("<<", StringComparison.Ordinal);
                return d >= 0 ? ExtractDictionary(slice, d) : slice.Trim();
            }
            return null;
        }

        /// <summary>
        /// Lấy byte thân stream Y NGUYÊN như trong file (CHƯA giải nén) của một object nằm thẳng trong file.
        /// Dùng khi CHÉP object sang bản mới: chép nguyên byte đã nén thì khỏi phải nén lại, và bản chép
        /// chắc chắn giống hệt bản gốc. Trả về null nếu object không có stream.
        /// </summary>
        public byte[]? GetRawStreamBytes(int number)
        {
            if (!_objects.TryGetValue(number, out var loc) || loc.Type != 1) return null;
            var text = Text;
            // Neo theo từ khóa "obj" của CHÍNH object này: bắt "<<" tự do có thể vớ phải dict của object kế tiếp
            // khi object này không phải dict.
            var objKw = text.IndexOf("obj", (int)loc.Value, StringComparison.Ordinal);
            if (objKw < 0 || objKw > loc.Value + 40) return null;
            var ds = objKw + 3;
            while (ds < text.Length && char.IsWhiteSpace(text[ds])) ds++;
            if (ds + 1 >= text.Length || text[ds] != '<' || text[ds + 1] != '<') return null;
            var dict = ExtractDictionary(text, ds);

            var st = text.IndexOf("stream", ds + dict.Length, StringComparison.Ordinal);
            // "stream" phải ngay sau dict (chỉ cách bởi khoảng trắng), xa hơn là đã sang object khác.
            if (st < 0 || st > ds + dict.Length + 20) return null;
            var p = st + 6;
            if (p + 1 < text.Length && text[p] == '\r' && text[p + 1] == '\n') p += 2;
            else if (p < text.Length && (text[p] == '\n' || text[p] == '\r')) p += 1;

            var lenM = Regex.Match(dict, @"/Length\s+(\d+)");
            int len;
            if (lenM.Success)
            {
                len = int.Parse(lenM.Groups[1].Value);
            }
            else
            {
                // /Length gián tiếp: dò tới "endstream". Chấp nhận được vì chỉ dùng cho tài liệu appearance
                // do CHÍNH TA sinh ra, không phải file của người dùng.
                var es = text.IndexOf("endstream", p, StringComparison.Ordinal);
                if (es < 0) return null;
                len = es - p;
                while (len > 0 && (text[p + len - 1] == '\n' || text[p + len - 1] == '\r')) len--;
            }

            if (p + len > Bytes.Length) return null;
            var raw = new byte[len];
            Array.Copy(Bytes, p, raw, 0, len);
            return raw;
        }

        /// <summary>
        /// Đọc dữ liệu thân stream bắt đầu tìm từ <paramref name="afterDict"/>, giải FlateDecode và gỡ
        /// PNG predictor nếu có. Không hỗ trợ filter khác -> trả về null (tầng trên coi là không đọc được).
        /// </summary>
        private byte[]? ReadStreamData(string text, int afterDict, string dict)
        {
            var st = text.IndexOf("stream", afterDict, StringComparison.Ordinal);
            if (st < 0) return null;
            var p = st + 6;
            // Sau từ khóa stream là CRLF hoặc LF (chuẩn cấm chỉ CR, nhưng file thật vẫn có).
            if (p + 1 < text.Length && text[p] == '\r' && text[p + 1] == '\n') p += 2;
            else if (p < text.Length && (text[p] == '\n' || text[p] == '\r')) p += 1;

            var lenM = Regex.Match(dict, @"/Length\s+(\d+)");
            if (!lenM.Success) return null; // /Length gián tiếp -> chưa gặp trên corpus thật, không đoán
            var len = int.Parse(lenM.Groups[1].Value);
            if (p + len > Bytes.Length) return null;

            var raw = new byte[len];
            Array.Copy(Bytes, p, raw, 0, len);

            if (dict.Contains("/FlateDecode", StringComparison.Ordinal))
            {
                try
                {
                    using var input = new MemoryStream(raw);
                    using var z = new ZLibStream(input, CompressionMode.Decompress);
                    using var outMs = new MemoryStream();
                    z.CopyTo(outMs);
                    raw = outMs.ToArray();
                }
                catch
                {
                    return null;
                }
            }
            else if (Regex.IsMatch(dict, @"/Filter\s*/?\w"))
            {
                return null; // filter khác Flate (LZW, ASCII85...) -> không hỗ trợ
            }

            var predM = Regex.Match(dict, @"/Predictor\s+(\d+)");
            if (predM.Success && int.Parse(predM.Groups[1].Value) >= 10)
            {
                var colM = Regex.Match(dict, @"/Columns\s+(\d+)");
                var columns = colM.Success ? int.Parse(colM.Groups[1].Value) : 1;
                // PNG predictor: mỗi dòng có 1 byte kiểu lọc ở đầu, giá trị lưu là HIỆU so với dòng/ô trước.
                var outBytes = new List<byte>(raw.Length);
                var prev = new byte[columns];
                var q = 0;
                while (q + 1 + columns <= raw.Length)
                {
                    var filter = raw[q++];
                    var row = new byte[columns];
                    Array.Copy(raw, q, row, 0, columns);
                    q += columns;
                    for (var i = 0; i < columns; i++)
                    {
                        row[i] = filter switch
                        {
                            0 => row[i],                                              // None
                            1 => (byte)(row[i] + (i > 0 ? row[i - 1] : 0)),           // Sub
                            2 => (byte)(row[i] + prev[i]),                            // Up
                            _ => row[i],
                        };
                    }
                    outBytes.AddRange(row);
                    prev = row;
                }
                raw = outBytes.ToArray();
            }

            return raw;
        }

        /// <summary>
        /// Cắt nguyên văn một dict "&lt;&lt; ... &gt;&gt;" bắt đầu tại <paramref name="start"/>, đếm lồng nhau và
        /// BỎ QUA dấu ngoặc nằm trong chuỗi literal "(...)" — nếu không, một chuỗi chứa "&gt;&gt;" sẽ cắt cụt dict.
        /// </summary>
        public static string ExtractDictionary(string text, int start)
        {
            var depth = 0;
            var i = start;
            while (i < text.Length - 1)
            {
                var c = text[i];
                if (c == '(')
                {
                    // Chuỗi literal: nhảy tới ')' cân bằng, tôn trọng escape '\'.
                    var level = 1;
                    i++;
                    while (i < text.Length && level > 0)
                    {
                        if (text[i] == '\\') { i += 2; continue; }
                        if (text[i] == '(') level++;
                        else if (text[i] == ')') level--;
                        i++;
                    }
                    continue;
                }
                if (c == '<' && text[i + 1] == '<') { depth++; i += 2; continue; }
                if (c == '>' && text[i + 1] == '>')
                {
                    depth--;
                    i += 2;
                    if (depth == 0) return text[start..i];
                    continue;
                }
                i++;
            }
            return text[start..];
        }
    }
}
