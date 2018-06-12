using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using LFS2S3Proxy.Extensions;

namespace LFS2S3Proxy
{
    public class Request
    {
        public string HttpMethod { get; set; }
        public IDictionary<string, IEnumerable<string>> Headers { get; set; }
        public Stream InputStream { get; set; }
        public string RawUrl { get; set; }
        public int ContentLength => int.Parse(Headers["Content-Length"].First());
    }

    public class Response
    {
        public Response()
        {
            WriteStream = s => { };
            StatusCode = 200;
            Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } };
        }

        public int StatusCode { get; set; }
        public IDictionary<string, string> Headers { get; set; }
        public Action<Stream> WriteStream { get; set; }
    }

    public class StringResponse : Response
    {
        public StringResponse(string message, int statusCode = 200)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            WriteStream = s => s.Write(bytes, 0, bytes.Length);
            StatusCode = statusCode;
        }
    }

    public class EmptyResponse : Response
    {
        public EmptyResponse(int statusCode = 204)
        {
            StatusCode = statusCode;
        }
    }

    public class RequestContext
    {
        private readonly HttpListenerResponse _listenerResponse;

        public RequestContext(HttpListenerRequest request, HttpListenerResponse response)
        {
            _listenerResponse = response;
            Request = MapRequest(request);
        }

        private static Request MapRequest(HttpListenerRequest request)
        {
            var mapRequest = new Request
            {
                Headers = request.Headers.ToDictionary(),
                HttpMethod = request.HttpMethod,
                InputStream = request.InputStream,
                RawUrl = request.RawUrl
            };
            return mapRequest;
        }

        public Request Request { get; }

        public void Respond(Response response)
        {
            foreach (var header in response.Headers.Where(r => r.Key != "Content-Type"))
            {
                _listenerResponse.AddHeader(header.Key, header.Value);
            }

            _listenerResponse.ContentType = response.Headers["Content-Type"];
            _listenerResponse.StatusCode = response.StatusCode;

            using (var output = _listenerResponse.OutputStream)
            {
                try
                {
                    response.WriteStream(output);
                }
                catch
                {
                    //Ignore
                }
            }
        }
    }
}
