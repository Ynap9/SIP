namespace Sip.be.External.Documents.Dtos
{
    /// <summary>
    /// Một hình chữ nhật trong hệ tọa độ CỦA PDF: đơn vị point, gốc ở góc DƯỚI-TRÁI trang, trục Y hướng LÊN.
    /// Khác hệ của <see cref="PdfSignPlacementDto"/> (tỉ lệ, gốc trên-trái) — chỗ đổi hệ là
    /// <c>SignaturePlacementResolver</c>, ngoài nó ra không nơi nào được tự lật trục.
    /// </summary>
    public sealed class PdfRectDto
    {
        /// <summary>Cạnh trái.</summary>
        public double X { get; set; }

        /// <summary>Cạnh DƯỚI (không phải cạnh trên).</summary>
        public double Y { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }

        /// <summary>Cạnh phải.</summary>
        public double Right => X + Width;

        /// <summary>Cạnh trên.</summary>
        public double Top => Y + Height;
    }
}
