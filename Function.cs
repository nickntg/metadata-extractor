using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using MetadataExtractor;
using Newtonsoft.Json;
using Directory = MetadataExtractor.Directory;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace MetadataExtractorFunction
{
    public class Function
    {
        private static async Task Main()
        {
            Func<S3EventNotification, ILambdaContext, string> func = FunctionHandler;
            using var handlerWrapper = HandlerWrapper.GetHandlerWrapper(func, new DefaultLambdaJsonSerializer());
            using var bootstrap = new LambdaBootstrap(handlerWrapper);
            await bootstrap.RunAsync();
        }

        public static string FunctionHandler(S3EventNotification input, ILambdaContext context)
        {
            var logger = context.Logger;

            using (var s3Client = new AmazonS3Client())
            {
                foreach (var record in input.Records)
                {
                    var dic = ExtractMetadata(record, s3Client, logger);
                    if (dic != null)
                    {
                        SaveMetaData(record, dic, s3Client, logger);
                    }
                }
            }

            return "Ok";
        }

        private static void SaveMetaData(S3EventNotification.S3EventNotificationRecord record,
            Dictionary<string, string> dic,
            IAmazonS3 s3Client,
            ILambdaLogger logger)
        {
            try
            {
                var newKey = $"{record.S3.Object.Key}.metadata.json";

                var result = s3Client.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = record.S3.Bucket.Name,
                    Key = newKey,
                    ContentType = "application/json",
                    ContentBody = JsonConvert.SerializeObject(dic)
                }, CancellationToken.None).Result;

                if (result.HttpStatusCode != HttpStatusCode.OK)
                {
                    throw new InvalidOperationException($"Could not save {newKey}");
                }
            }
            catch (Exception ex)
            {
                logger.LogLine(ex.Message);
                throw;
            }
        }

        private static Dictionary<string, string> ExtractMetadata(S3EventNotification.S3EventNotificationRecord record,
            IAmazonS3 s3Client,
            ILambdaLogger logger)
        {
            if (record.S3.Object.Key.EndsWith(".json") ||
                record.S3.Object.Key.EndsWith(".txt"))
            {
                logger.LogLine("Skipping text content");
                return null;
            }

            using (var ms = new MemoryStream())
            {
                try
                {
                    var result = s3Client.GetObjectAsync(new GetObjectRequest
                    {
                        BucketName = record.S3.Bucket.Name,
                        Key = record.S3.Object.Key
                    }, CancellationToken.None).Result;

                    if (result.HttpStatusCode != HttpStatusCode.OK)
                    {
                        logger.LogLine($"Could not read {record.S3.Bucket.Name}://{record.S3.Object.Key}");
                        logger.LogLine($"S3 status code {result.HttpStatusCode}");
                        return null;
                    }

                    result.ResponseStream.CopyTo(ms);
                    ms.Position = 0;
                }
                catch (Exception ex)
                {
                    logger.LogLine($"Error reading {record.S3.Bucket.Name}://{record.S3.Object.Key}");
                    logger.LogLine(ex.ToString());
                    return null;
                }

                IReadOnlyList<Directory> directories;

                try
                {
                    directories = ImageMetadataReader.ReadMetadata(ms);
                }
                catch (Exception ex)
                {
                    logger.LogLine($"Could not parse {record.S3.Bucket.Name}://{record.S3.Object.Key}");
                    logger.LogLine(ex.ToString());
                    return null;
                }

                var dic = new Dictionary<string, string>();

                foreach (var directory in directories)
                {
                    foreach (var tag in directory.Tags)
                    {
                        try
                        {
                            dic.Add($"{directory.Name}.{tag.Name}", tag.Description);
                        }
                        catch (Exception ex)
                        {
                            logger.LogLine($"Error adding tag {tag.Name} - ignoring this error");
                            logger.LogLine(ex.ToString());
                        }
                    }
                }

                return dic;
            }
        }
    }
}