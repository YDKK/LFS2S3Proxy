using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text;
using System.Threading.Tasks;

namespace LFS2S3Proxy
{
    public class HttpServer : IObservable<RequestContext>, IDisposable
    {
        private readonly HttpListener _listener;
        private readonly IObservable<RequestContext> _stream;

        public HttpServer(string url)
        {
            _listener = new HttpListener { IgnoreWriteExceptions = true };
            _listener.Prefixes.Add(url);
            _listener.Start();
            _stream = ObservableHttpContext();
        }

        private IObservable<RequestContext> ObservableHttpContext()
        {
            return Observable.Create<RequestContext>(obs =>
                _listener.GetContextAsync().ToObservable().Select(x => new RequestContext(x.Request, x.Response)).Subscribe(obs)
            )
                .Repeat()
                .Retry()
                .Publish()
                .RefCount();
        }
        public void Dispose()
        {
            _listener.Stop();
        }

        public IDisposable Subscribe(IObserver<RequestContext> observer)
        {
            return _stream.Subscribe(observer);
        }
    }
}
