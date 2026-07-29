using System.Globalization;
using System.Text;

namespace Sip.be.External.Documents.Implements
{
    /// <summary>
    /// Bộ ghi object PDF TỐI THIỂU — chỉ đủ các kiểu giá trị thực sự xuất hiện trong những object mà bản ký
    /// nối bản TỰ SINH RA (dict chữ ký, field chữ ký, widget, xref stream): tên, số, chuỗi, luận lý, null,
    /// tham chiếu gián tiếp, mảng, dict lồng.
    ///
    /// PHẠM VI CÓ CHỦ ĐÍCH — đây KHÔNG phải bộ ghi PDF tổng quát:
    /// - Không mã hóa stream. Trong Catalog/AcroForm/Page, stream LUÔN là tham chiếu gián tiếp chứ không nằm
    ///   thẳng trong dict, nên không cần.
    /// - Object CŨ bị ghi đè (Catalog, trang 1) KHÔNG đi qua đây: chúng được CHÈN BYTE vào đúng nguyên văn
    ///   dict gốc (xem PdfIncrementalSigner). Phân tích rồi ghi lại một dict lạ là tự chuốc lấy rủi ro làm
    ///   hỏng những giá trị mình không lường trước; nguyên văn thì luôn đúng.
    ///
    /// Xem .claude/be/plans/pdf-incremental-signing.plan.md step 2.
    /// </summary>
    public static class PdfObjectWriter
    {
        /// <summary>Tên PDF: /Type, /Sig... Ký tự ngoài bảng in được phải viết dạng #xx.</summary>
        public sealed class Name
        {
            public string Value { get; }
            public Name(string value) => Value = value;
        }

        /// <summary>Tham chiếu gián tiếp "N 0 R".</summary>
        public sealed class Ref
        {
            public int Number { get; }
            public Ref(int number) => Number = number;
        }

        /// <summary>
        /// Nguyên văn — đưa thẳng vào file không xử lý gì. Dùng cho những thứ đã có sẵn dạng byte đúng chuẩn:
        /// mảng /ID của trailer, chỗ trống /Contents, dict gốc đã chèn byte.
        /// </summary>
        public sealed class Raw
        {
            public string Text { get; }
            public Raw(string text) => Text = text;
        }

        /// <summary>Chuỗi PDF literal/hex. Xem <see cref="WriteValue"/> để biết khi nào chuyển sang hex.</summary>
        public sealed class Str
        {
            public string Value { get; }
            public Str(string value) => Value = value;
        }

        /// <summary>Dict PDF — GIỮ THỨ TỰ khóa như lúc thêm để file sinh ra ổn định, dễ đối chiếu byte.</summary>
        public sealed class Dict
        {
            public List<KeyValuePair<string, object?>> Items { get; } = new();

            public Dict Add(string key, object? value)
            {
                Items.Add(new KeyValuePair<string, object?>(key, value));
                return this;
            }
        }

        /// <summary>
        /// Ghi trọn một object gián tiếp: "N 0 obj\n&lt;giá trị&gt;\nendobj\n".
        /// </summary>
        public static void WriteObject(StringBuilder sb, int number, object? value)
        {
            sb.Append(number.ToString(CultureInfo.InvariantCulture)).Append(" 0 obj\n");
            WriteValue(sb, value);
            sb.Append("\nendobj\n");
        }

        /// <summary>
        /// Ghi một giá trị. Kiểu không nằm trong danh sách hỗ trợ thì NÉM lỗi chứ không ghi bừa —
        /// một giá trị ghi sai lặng lẽ sẽ thành file PDF hỏng mà không ai biết tại đâu.
        /// </summary>
        public static void WriteValue(StringBuilder sb, object? value)
        {
            switch (value)
            {
                case null:
                    sb.Append("null");
                    break;

                case Raw raw:
                    sb.Append(raw.Text);
                    break;

                case Name name:
                    sb.Append('/');
                    foreach (var c in name.Value)
                    {
                        // Chuẩn: ngoài khoảng in được 0x21-0x7E, và các ký tự phân tách, phải viết #xx.
                        if (c is > (char)0x20 and < (char)0x7F && "()<>[]{}/%#".IndexOf(c) < 0)
                            sb.Append(c);
                        else
                            sb.Append('#').Append(((int)c).ToString("X2", CultureInfo.InvariantCulture));
                    }
                    break;

                case bool b:
                    sb.Append(b ? "true" : "false");
                    break;

                case int i:
                    sb.Append(i.ToString(CultureInfo.InvariantCulture));
                    break;

                case long l:
                    sb.Append(l.ToString(CultureInfo.InvariantCulture));
                    break;

                case double d:
                    // InvariantCulture bắt buộc: máy đặt locale VN sẽ ghi "594,72" và PDF thành rác.
                    sb.Append(d.ToString("0.####", CultureInfo.InvariantCulture));
                    break;

                case Ref r:
                    sb.Append(r.Number.ToString(CultureInfo.InvariantCulture)).Append(" 0 R");
                    break;

                case Str s:
                    WriteString(sb, s.Value);
                    break;

                case Dict dict:
                    sb.Append("<<");
                    foreach (var kv in dict.Items)
                    {
                        WriteValue(sb, new Name(kv.Key));
                        // Giá trị bắt đầu bằng ký tự tự phân tách (/ < [ () thì không cần khoảng trắng, nhưng
                        // thêm vào luôn cho chắc: thừa 1 space không bao giờ sai, thiếu thì dính token.
                        sb.Append(' ');
                        WriteValue(sb, kv.Value);
                    }
                    sb.Append(">>");
                    break;

                case System.Collections.IEnumerable list when value is not string:
                    sb.Append('[');
                    var first = true;
                    foreach (var item in list)
                    {
                        if (!first) sb.Append(' ');
                        WriteValue(sb, item);
                        first = false;
                    }
                    sb.Append(']');
                    break;

                default:
                    throw new InvalidOperationException(
                        $"PdfObjectWriter không hỗ trợ kiểu giá trị {value.GetType().Name}.");
            }
        }

        /// <summary>
        /// Ghi chuỗi. Toàn ASCII -> literal "(...)" cho dễ đọc. Có ký tự ngoài ASCII (tên người ký tiếng Việt
        /// "Phạm Ngọc Chính", lý do ký "Ký số tài liệu") -> BẮT BUỘC hex UTF-16BE kèm BOM FEFF: bảng mã mặc
        /// định của PDF (PDFDocEncoding) KHÔNG có các ký tự có dấu, ghi literal là mất dấu hoặc ra ký tự lạ.
        /// </summary>
        private static void WriteString(StringBuilder sb, string value)
        {
            var ascii = value.All(c => c is >= (char)0x20 and < (char)0x7F);
            if (ascii)
            {
                sb.Append('(');
                foreach (var c in value)
                {
                    // 3 ký tự này có nghĩa cú pháp trong chuỗi literal -> phải escape.
                    if (c is '(' or ')' or '\\') sb.Append('\\');
                    sb.Append(c);
                }
                sb.Append(')');
                return;
            }

            sb.Append('<').Append("FEFF");
            foreach (var b in Encoding.BigEndianUnicode.GetBytes(value))
            {
                sb.Append(b.ToString("X2", CultureInfo.InvariantCulture));
            }
            sb.Append('>');
        }

        /// <summary>
        /// Chèn thêm một phần tử vào CUỐI mảng có tên <paramref name="key"/> trong nguyên văn dict
        /// <paramref name="dictRaw"/>, giữ nguyên từng byte còn lại. Không có mảng đó thì thêm mới trước "&gt;&gt;".
        ///
        /// Đây là cách ghi đè object CŨ: Catalog cần /Fields += ref, trang 1 cần /Annots += ref. Chèn byte
        /// giữ nguyên mọi thứ khác trong dict — kể cả những khóa mình không hiểu — nên an toàn hơn hẳn việc
        /// phân tích rồi dựng lại.
        /// Trả về null nếu mảng là tham chiếu gián tiếp (/Fields 12 0 R) — tầng trên phải ghi đè object đó.
        /// </summary>
        public static string? AppendToArray(string dictRaw, string key, int newObjectNumber)
        {
            var marker = "/" + key;
            var at = -1;
            var from = 0;
            while (true)
            {
                var idx = dictRaw.IndexOf(marker, from, StringComparison.Ordinal);
                if (idx < 0) break;
                // Phải là RANH GIỚI token: "/Annots" không được khớp nhầm vào "/AnnotsExtra".
                var after = idx + marker.Length;
                if (after >= dictRaw.Length ||
                    dictRaw[after] is ' ' or '\r' or '\n' or '\t' or '[' or '/' or '<')
                {
                    at = idx;
                    break;
                }
                from = idx + 1;
            }

            if (at < 0)
            {
                // Chưa có khóa -> thêm mới ngay trước ">>" đóng dict ngoài cùng.
                var close = dictRaw.LastIndexOf(">>", StringComparison.Ordinal);
                if (close < 0) return null;
                return dictRaw[..close] + marker + "[" + newObjectNumber + " 0 R]" + dictRaw[close..];
            }

            var p = at + marker.Length;
            while (p < dictRaw.Length && char.IsWhiteSpace(dictRaw[p])) p++;
            if (p >= dictRaw.Length || dictRaw[p] != '[') return null; // tham chiếu gián tiếp

            // Tìm ']' đóng mảng. Mảng trong /Fields và /Annots chỉ chứa tham chiếu nên không lồng nhau,
            // nhưng vẫn đếm lồng để không cắt nhầm nếu gặp file khác thường.
            var depth = 0;
            var q = p;
            while (q < dictRaw.Length)
            {
                if (dictRaw[q] == '[') depth++;
                else if (dictRaw[q] == ']')
                {
                    depth--;
                    if (depth == 0) break;
                }
                q++;
            }
            if (q >= dictRaw.Length) return null;

            return dictRaw[..q] + " " + newObjectNumber + " 0 R" + dictRaw[q..];
        }
    }
}
