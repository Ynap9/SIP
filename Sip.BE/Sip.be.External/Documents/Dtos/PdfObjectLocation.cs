namespace Sip.be.External.Documents.Dtos
{
    /// <summary>
    /// Vị trí của một object trong file PDF, đọc từ bảng/luồng xref.
    /// </summary>
    public sealed class PdfObjectLocation
    {
        /// <summary>1 = nằm thẳng trong file; 2 = nằm nén trong một object stream.</summary>
        public int Type { get; init; }

        /// <summary>Type 1: offset byte trong file. Type 2: SỐ HIỆU của object stream chứa nó.</summary>
        public long Value { get; init; }

        /// <summary>Type 2: thứ tự của object bên trong object stream đó.</summary>
        public int IndexInStream { get; init; }
    }
}
