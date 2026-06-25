using Sip.be.Shared.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sip.be.Domain.Document
{
    public class ConfigHoSo :ISoftDeleted
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
