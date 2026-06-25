using System;

namespace Sip.be.Application.Document.Dtos.Config
{
    public class ViewConfigVanBanDto
    {
        public int Id { get; set; }
        public string TruongThongTin { get; set; } = string.Empty;
        public string MoTa { get; set; } = string.Empty;
        public int BatBuoc { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public DateTime? DeletedDate { get; set; }
        public bool Deleted { get; set; }
    }
}
