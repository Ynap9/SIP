using AutoMapper;
using Microsoft.Extensions.Logging;
using Sip.be.infratructure.Persistence;
using Sip.be.Shared.Constants;
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

        /// <summary>
        /// Giờ Việt Nam hiện tại (UTC+7). MỌI service lấy giờ qua đây, KHÔNG dùng DateTime.Now:
        /// app chạy trên máy người dùng nên Now lệch theo múi giờ máy, sai âm thầm.
        /// Xem .claude/be/architecture/08-datetime.md.
        /// </summary>
        protected static DateTime VietnamNow => DateTimeConstants.VietnamNow;

        public BaseService(SipDbContext sipDbContext, ILogger<BaseService> logger, IMapper mapper)
        {
            _sipDbContext = sipDbContext;
            _logger = logger;
            _mapper = mapper;
        }
    }
}
