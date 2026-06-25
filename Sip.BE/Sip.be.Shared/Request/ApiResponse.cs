using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sip.be.Shared.Request
{
    public class ApiResponse
    {
        public StatusCodeE Status { get; set; }
        public object? Data { get; set; }
        public int Code { get; set; }
        public string Message { get; set; }

        public ApiResponse(StatusCodeE status, object? data, int code, string message)
        {
            Status = status;
            Data = data;
            Code = code;
            Message = message;
        }

        public ApiResponse(object? data)
        {
            Status = StatusCodeE.Success;
            Data = data;
            Code = 200;
            Message = "Ok";
        }

        public ApiResponse()
        {
            Status = StatusCodeE.Success;
            Data = null;
            Code = 200;
            Message = "Ok";
        }
    }

    public class ApiResponse<T> : ApiResponse
    {
        public new T Data { get; set; }

        public ApiResponse(StatusCodeE status, T data, int code, string message) : base(status, data, code, message)
        {
            Status = status;
            Data = data;
            Code = code;
            Message = message;
        }

        public ApiResponse(T data) : base(data)
        {
            Data = data;
        }
    }

    public enum StatusCodeE
    {
        Success = 1,
        Error = 0
    }
}