namespace Sip.be.External.Documents.Dtos
{
    /// <summary>Một object đã được chép sang bản mới, số hiệu và các tham chiếu bên trong đã đánh lại.</summary>
    public sealed class TransplantedObject
    {
        /// <summary>Số hiệu object trong tài liệu ĐÍCH.</summary>
        public int Number { get; init; }

        /// <summary>Nguyên văn dict, mọi "N 0 R" bên trong đã đổi sang số hiệu đích.</summary>
        public string DictText { get; init; } = string.Empty;

        /// <summary>Byte thân stream chép nguyên si (vẫn nén như cũ); null nếu object không có stream.</summary>
        public byte[]? StreamData { get; init; }
    }
}
