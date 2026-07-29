namespace Sip.be.External.Documents.Dtos
{
    /// <summary>Kết quả dựng khối chữ ký hiển thị: cây object cần ghi thêm + số hiệu Form XObject gốc cây.</summary>
    public sealed class AppearanceResult
    {
        public List<TransplantedObject> Objects { get; init; } = new();

        /// <summary>Số hiệu Form XObject dùng làm /AP /N của widget chữ ký.</summary>
        public int FormObjectNumber { get; init; }
    }
}
