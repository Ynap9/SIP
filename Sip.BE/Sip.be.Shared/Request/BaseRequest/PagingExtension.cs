using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace traobang.be.shared.HttpRequest.BaseRequest
{
    public static class PagingParameter
    {
        public const int DefaultPageSize = -1;
    }

    public static class PagingExtension
    {
        /// <summary>
        /// Hàm phân trang
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="query"></param>
        /// <param name="input"></param>
        /// <returns></returns>
        public static IQueryable<T> Paging<T>(this IQueryable<T> query, BaseRequestPagingDto input)
        {
            if (input.PageSize != PagingParameter.DefaultPageSize)
            {
                query = query.Skip(input.GetSkip()).Take(input.PageSize);
            }
            return query;
        }
    }
}
