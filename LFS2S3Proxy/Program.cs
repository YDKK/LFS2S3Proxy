using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Amazon.Runtime.Internal;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Topshelf;

namespace LFS2S3Proxy
{
    class Program
    {
        const string version = "LFS2S3Proxy v1.0";
        static void Main(string[] args)
        {
            HostFactory.Run(x =>
            {
                x.Service<Instance>(s =>
                {
                    s.ConstructUsing(name => new Instance());
                    s.WhenStarted(tc => tc.Start());
                    s.WhenStopped(tc => tc.Stop());
                });

                //Windowsサービスの設定
                x.RunAsLocalSystem();
                x.SetDescription("This is LFS2S3Proxy");
                x.SetDisplayName("LFS2S3Proxy");
                x.SetServiceName("LFS2S3Proxy_Service");
            });
        }

        private class Instance
        {
            private static S3Proxy _proxy;
            private static string _token;
            private readonly List<IDisposable> _list = new List<IDisposable>();
            private HttpServer _server;

            public void Stop()
            {
                foreach (var observable in _list)
                {
                    observable.Dispose();
                }

                try
                {
                    _server.Dispose();
                }
                catch
                {
                    // ignored
                }
            }

            public void Start()
            {
                if (!File.Exists("config.json"))
                {
                    Console.Error.WriteLine("config.json not found.");
                    Console.Error.WriteLine("require: s3bucketName, token, listen");
                    return;
                }

                dynamic config = JObject.Parse(File.ReadAllText("config.json"));
                _proxy = new S3Proxy((string)config.s3bucketName);
                _token = config.token;

                _server = new HttpServer((string)config.listen);

                _list.Add(_server.Subscribe(ctx => Console.WriteLine("Request to: " + ctx.Request.RawUrl)));
                _list.Add(_server.Subscribe(Event));

                Console.WriteLine($"{version} started at {config.listen}");
            }

            private static void Event(RequestContext ctx)
            {
                if (ctx.Request.RawUrl == "/version")
                {
                    ctx.Respond(new StringResponse(version));
                    return;
                }

                if (ctx.Request.RawUrl.StartsWith($"/{_token}/"))
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
