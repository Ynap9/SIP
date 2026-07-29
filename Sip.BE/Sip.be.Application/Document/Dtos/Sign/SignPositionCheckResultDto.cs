namespace Sip.be.Application.Document.Dtos.Sign
{
    /// <summary>
    /// KẾT QUẢ kiểm vị trí chữ ký cho cả lô — dữ liệu của mục "Kiểm tra cấu hình chữ ký" ở bước ký.
    /// Chỉ để hiển thị: mọi con số ở đây là DỰ BÁO bộ ký sẽ làm gì, không chặn việc ký.
    /// </summary>
    public class SignPositionCheckResultDto
    {
        /// <summary>Tổng số file PDF trong nguồn.</summary>
        public int TotalFiles { get; set; }

        /// <summary>Số file mà mọi khối đều vừa vặn, không phải chỉnh.</summary>
        public int FittedCount { get; set; }

        /// <summary>Số file sẽ bị chỉnh vị trí khi ký (vẫn ký được).</summary>
        public int AdjustedCount { get; set; }

        /// <summary>Số file không đọc được để đo.</summary>
        public int UnreadableCount { get; set; }

        /// <summary>Chi tiết từng file, đúng thứ tự của lô ký.</summary>
        public List<SignPositionFileDto> Files { get; set; } = new();
    }
}
