using System.Net;
using System.Runtime.Serialization;

namespace VideoPlayer.Extensions
{
    public class HttpClientRequestException : ApplicationException
    {
        public HttpClientRequestException(HttpStatusCode statusCode)
        {
            StatusCode = statusCode;
        }

        public HttpClientRequestException(string message, HttpStatusCode statusCode)
            :base(message)
        {
            StatusCode = statusCode;
        }

        public HttpClientRequestException(string message, Exception innerException, HttpStatusCode statusCode) : base(message, innerException)
        {
            StatusCode = statusCode;
        }

        public HttpStatusCode StatusCode { get; }
    }
}
