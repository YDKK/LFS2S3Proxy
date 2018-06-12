using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Codeplex.Data;
using Newtonsoft.Json;

namespace LFS2S3Proxy
{
    public class S3Proxy
    {
        private readonly string _bucketName;
        private readonly IAmazonS3 _client;
        private readonly TimeSpan _urlExpiresAt = TimeSpan.FromMinutes(5);
        public S3Proxy(string bucketName)
        {
            _bucketName = bucketName;
            _client = new AmazonS3Client(Amazon.RegionEndpoint.APNortheast1);
        }

        private string GeneratePreSignedUrl(string repositoryName, string key, HttpVerb httpVerb)
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _bucketName,
                Key = $"LFS/{repositoryName}/{key}",
                Verb = httpVerb,
                Expires = DateTime.Now + _urlExpiresAt
            };
            return _client.GetPreSignedURL(request);
        }

        private (bool, MetadataCollection) CheckExists(string repositoryName, string key)
        {
            try
            {
                var response = _client.GetObjectMetadata(_bucketName, $"LFS/{repositoryName}/{key}");

                return (true, response.Metadata);
            }

            catch (AmazonS3Exception ex)
            {
                if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return (false, null);

                //status wasn't not found, so throw the exception
                throw;
            }
        }

        public void Request(RequestContext context, string repositoryName)
        {
            var stream = new MemoryStream();
            context.Request.InputStream.CopyTo(stream);

            var json = Encoding.UTF8.GetString(stream.ToArray());

            var data = DynamicJson.Parse(json);

            dynamic response = new ExpandoObject();

            response.objects = new List<ExpandoObject>();

            Parallel.ForEach((IEnumerable<dynamic>)(object[])data.objects, o =>
            {
                dynamic eo = new ExpandoObject();
                eo.oid = o.oid;
                eo.size = (int)o.size;
                lock (response.objects)
                {
                    ((List<ExpandoObject>)response.objects).Add(eo);
                }
            });

            switch (data.operation)
            {
                case "upload":
                    Parallel.ForEach((IEnumerable<dynamic>)response.objects, o =>
                    {
                        var (exist, metadata) = CheckExists(repositoryName, (string)o.oid);
                        if (!exist)
                        {
                            o.actions = new
                            {
                                upload = new
                                {
                                    href = GeneratePreSignedUrl(repositoryName, o.oid, HttpVerb.PUT),
                                    expires_at = DateTime.Now + _urlExpiresAt
                                }
                            };
                        }
                    });
                    break;
                case "download":
                    Parallel.ForEach((IEnumerable<dynamic>)response.objects, o =>
                    {
                        var (exist, metadata) = CheckExists(repositoryName, (string)o.oid);
                        if (exist)
                        {
                            o.actions = new
                            {
                                download = new
                                {
                                    href = GeneratePreSignedUrl(repositoryName, o.oid, HttpVerb.GET),
                                    expires_at = DateTime.Now + _urlExpiresAt
                                }
                            };
                        }
                        else
                        {
                            o.error = new
                            {
                                code = 404,
                                message = "Not Found."
                            };
                        }
                    });
                    break;
                case "verify":
                    break;//not supported yet.
            }

            var res = new StringResponse(JsonConvert.SerializeObject(response));
            context.Respond(res);
        }
    }
}
