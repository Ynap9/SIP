using AutoMapper;
using Sip.be.Application.Document.Dtos.Config;
using Sip.be.Domain.Document;
using Sip.be.Domain.Documents;

namespace Sip.be.Application.Base
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<ConfigHoSo, ViewConfigHoSoDto>();
            CreateMap<ConfigVanBan, ViewConfigVanBanDto>();
        }
    }
}
