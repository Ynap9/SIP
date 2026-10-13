namespace Sip.be.Application.Document.Dtos.SignTemplate
{
    /// <summary>
    /// Một template trả về cho FE. Đường dẫn ảnh ở đây là đường dẫn ĐÃ COPY trong %AppData% (file dùng thật),
    /// còn <see cref="AnhDauDoNguonPath"/>/<see cref="AnhChuKyTuoiNguonPath"/> là chỗ người dùng đã lấy ảnh,
    /// chỉ để hiển thị cho họ nhớ.
    /// </summary>
    public class ViewSignTemplateDto
    {
        public int Id { get; set; }
        public string TenTemplate { get; set; } = string.Empty;
        public string Thumbprint { get; set; } = string.Empty;
        public string? TenChungThu { get; set; }
        public string? LyDoKy { get; set; }
        public string? NoiKy { get; set; }

        public string? AnhDauDoPath { get; set; }
        public string? AnhDauDoNguonPath { get; set; }
        public string? AnhChuKyTuoiPath { get; set; }
        public string? AnhChuKyTuoiNguonPath { get; set; }

        public string? FontChuKySo { get; set; }
        public double? CoChuKySo { get; set; }
        public string? MauChuKySo { get; set; }

        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }

        /// <summary>Toạ độ mọi khối của template, phân biệt bằng <see cref="SignTemplatePositionDto.Kind"/>.</summary>
        public List<SignTemplatePositionDto> Positions { get; set; } = new();
    }
}
