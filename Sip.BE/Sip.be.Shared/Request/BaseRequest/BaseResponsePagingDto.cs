using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace traobang.be.shared.HttpRequest.BaseRequest
{
    public class BaseResponsePagingDto<T>
    {
        public IEnumerable<T> Items { get; set; } = Enumerable.Empty<T>();
        public int TotalItems { get; set; }
        public object? CustomData { get; set; }
    }
}
