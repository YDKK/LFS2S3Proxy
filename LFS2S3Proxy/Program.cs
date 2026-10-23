using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace LFS2S3Proxy
{
    class Program
    {
        const string version = "LFS2S3Proxy v1.0";
        static async Task<int> Main(string[] args)
        {
            var shutdownRequested = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var cleanupCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                shutdownRequested.TrySetResult(true);
            };
            EventHandler processExitHandler = (_, _) =>
            {
                shutdownRequested.TrySetResult(true);
                cleanupCompleted.Task.GetAwaiter().GetResult();
            };

            Console.CancelKeyPress += cancelHandler;
            AppDomain.CurrentDomain.ProcessExit += processExitHandler;
            try
            {
                using var instance = new Instance();
                if (!instance.Start())
                {
                    return 1;
                }

                Console.WriteLine("Press Ctrl+C to stop.");
                await Task.WhenAny(shutdownRequested.Task, instance.Completion).ConfigureAwait(false);

                if (instance.Completion.IsCompleted)
                {
                    try
                    {
                        await instance.Completion.ConfigureAwait(false);
                        Console.Error.WriteLine("HTTP listener stopped unexpectedly.");
                    }
                    catch (Exception)
                    {
                        // Instance.Dispose observes and logs the accept loop failure.
                    }

                    return 1;
                }

                instance.Dispose();
                return instance.Completion.IsFaulted ? 1 : 0;
            }
            finally
            {
                Console.CancelKeyPress -= cancelHandler;
                cleanupCompleted.TrySetResult(true);
                AppDomain.CurrentDomain.ProcessExit -= processExitHandler;
            }
        }

        private class Instance : IDisposable
        {
            private static S3Proxy _proxy;
            private static string _token;
            private HttpServer _server;
            private Task _completion = Task.CompletedTask;
            private bool _disposed;

            public Task Completion => _completion;

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }
                _disposed = true;

                try
                {
                    _server?.Dispose();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Failed to stop HTTP listener: {ex.Message}");
                }

                _server = null;

                try
                {
                    _proxy?.Dispose();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Failed to dispose S3 client: {ex.Message}");
                }

                _proxy = null;
                _token = null;
            }

            public bool Start()
            {
                if (!File.Exists("config.json"))
                {
                    Console.Error.WriteLine("config.json not found.");
                    Console.Error.WriteLine("require: s3bucketName, token, listen");
                    return false;
                }

                dynamic config = JObject.Parse(File.ReadAllText("config.json"));
                _proxy = new S3Proxy((string)config.s3bucketName);
                _token = config.token;

                _server = new HttpServer((string)config.listen, ctx =>
                {
                    Console.WriteLine("Request to: " + ctx.Request.RawUrl);
                    Event(ctx);
                });
                _completion = _server.Completion;

                Console.WriteLine($"{version} started at {config.listen}");
                return true;
            }

            private static void Event(RequestContext ctx)
            {
                if (ctx.Request.RawUrl == "/version")
                {
                    ctx.Respond(new StringResponse(version));
                    return;
                }

                if (TokenValidator.MatchesUrlToken(ctx.Request.RawUrl, _token))
                {
                    if (ctx.Request.RawUrl.EndsWith("/objects/batch") && ctx.Request.RawUrl.Count(x => x == '/') == 4)
                    {
                        var repositoryName = ctx.Request.RawUrl.Split('/')[2];
                        _proxy.Request(ctx, repositoryName);
                        return;
                    }

                    if (ctx.Request.RawUrl.EndsWith("/verify") && ctx.Request.RawUrl.Count(x => x == '/') == 3)
                    {
                        var repositoryName = ctx.Request.RawUrl.Split('/')[2];
                        _proxy.Verify(ctx, repositoryName);
                        return;
                    }
                }
                ctx.Respond(new StringResponse("Not Found.", 404));
            }
        }
    }
}
