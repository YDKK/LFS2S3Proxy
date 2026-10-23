using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace LFS2S3Proxy
{
    public sealed class HttpServer : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly Action<RequestContext> _requestHandler;
        private readonly CancellationTokenSource _shutdown = new CancellationTokenSource();
        private readonly Task _acceptTask;
        private int _disposed;

        public HttpServer(string url, Action<RequestContext> requestHandler)
        {
            _requestHandler = requestHandler ?? throw new ArgumentNullException(nameof(requestHandler));
            _listener = new HttpListener { IgnoreWriteExceptions = true };
            _listener.Prefixes.Add(url);
            _listener.Start();
            _acceptTask = AcceptRequestsAsync();
        }

        public Task Completion => _acceptTask;

        private async Task AcceptRequestsAsync()
        {
            while (!_shutdown.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (HttpListenerException) when (_shutdown.IsCancellationRequested)
                {
                    return;
                }
                catch (ObjectDisposedException) when (_shutdown.IsCancellationRequested)
                {
                    return;
                }

                if (_shutdown.IsCancellationRequested)
                {
                    try
                    {
                        context.Response.Close();
                    }
                    catch (HttpListenerException)
                    {
                        // The client may have disconnected while the listener was stopping.
                    }
                    catch (ObjectDisposedException)
                    {
                        // The response may have been closed while the listener was stopping.
                    }

                    return;
                }

                try
                {
                    _requestHandler(new RequestContext(context.Request, context.Response));
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine($"HTTP request handler failed: {exception}");
                    try
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                        context.Response.Close();
                    }
                    catch (HttpListenerException)
                    {
                        // The client may have disconnected before the error response was sent.
                    }
                    catch (ObjectDisposedException)
                    {
                        // The response may have been closed before the error response was sent.
                    }
                }
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _shutdown.Cancel();
            try
            {
                _listener.Close();
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Failed to close HTTP listener: {exception.Message}");
            }

            try
            {
                _acceptTask.GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"HTTP accept loop exited with an error: {exception}");
            }
            finally
            {
                _shutdown.Dispose();
            }
        }
    }
}
