using AutoMapper;
using Microsoft.Extensions.Logging;
using Sip.be.infratructure.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sip.be.Application.Base
{
    public class BaseService
    {
        public readonly SipDbContext _sipDbContext;
        public readonly ILogger<BaseService> _logger;
        protected readonly IMapper _mapper;

        public BaseService(SipDbContext sipDbContext, ILogger<BaseService> logger, IMapper mapper)
        {
            _sipDbContext = sipDbContext;
            _logger = logger;
            _mapper = mapper;
        }
    }
}
