using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Text;
using Amazon;
using Amazon.Util;
using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.S3Events;
using System.Net;
using System.Text.Json;
using ProjectMomo.Models;
using ProjectMomo.Lambda;
using ProjectMomo.Models.DynamoDB;
using ProjectMomo.Extensions;
using OfficeOpenXml;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace ProjectMomo
{
    public class Function
    {
        
        /// <summary>
        /// A simple function that takes a string and does a ToUpper
        /// </summary>
        /// <param name="input"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public string FunctionHandler(string input, ILambdaContext context)
        {
            return input?.ToUpper();
        }

        /// <summary>
        /// Function Handler, S3 Put Object
        /// </summary>
        /// <param name="input"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task<APIGatewayProxyResponse> S3FunctionHandlerAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            const string ENV_S3_BUCKET_NAME = "S3_BUCKET_NAME";

            // リージョンの取得
            var regionEndpoint = GetEnvironmentRegionEndpoint();
            if (regionEndpoint == null) 
            {
                context.Logger.LogLine("error. don't set environment region.");
                return new APIGatewayProxyResponse() {
                    StatusCode = (int)HttpStatusCode.InternalServerError,
                };
            }

            // S3バケット名を環境変数から取得
            var s3bucketName = Environment.GetEnvironmentVariable(ENV_S3_BUCKET_NAME);
            if (string.IsNullOrEmpty(s3bucketName))
            {
                context.Logger.LogLine($"error. The environment variable {ENV_S3_BUCKET_NAME} is not set.");
                return new APIGatewayProxyResponse() {
                    StatusCode = (int)HttpStatusCode.InternalServerError,
                };
            }

            // Cognito認証情報を取得する
            // https://stackoverflow.com/questions/29928401/how-to-get-the-cognito-identity-id-in-aws-lambda
            var claims = request.RequestContext?.Authorizer?.Claims;
            // Cognito認証情報が取得できたらusernameを取得する
            var userId = claims?["cognito:username"] ?? string.Empty;

            try
            {
                // ユーザー名をフォルダ名にする。認証が通ってない場合はpublic
                var dirName = string.IsNullOrEmpty(userId) ? "public" : userId;
                var s3key = dirName + $"/{Guid.NewGuid().ToString()}.json";
                context.Logger.LogLine($"write s3 key : {s3key}");

                // S3にオブジェクトをPutする
                var result = await PutObjectS3Async(regionEndpoint, s3bucketName, s3key, CreateStream(userId));
                context.Logger.LogLine($"put s3 reulst:{result.HttpStatusCode.ToString()}");
                
                return new APIGatewayProxyResponse() {
                    StatusCode = (int)HttpStatusCode.OK,
                    Headers = new Dictionary<string, string>() {
                        {"Access-Control-Allow-Origin", "*"},
                    },
                };
            }
            catch (Exception e)
            {
                context.Logger.LogLine("error. catch Exception by PutObjectS3.");
                context.Logger.LogLine(e.Message);
                return new APIGatewayProxyResponse() {
                    StatusCode = (int)HttpStatusCode.InternalServerError,
                };
            }
        }

        private Stream CreateStream(string userId)
        {
            //var json = "{\"user_id\":\"" + userId + "\"}";
            //return new MemoryStream(Encoding.UTF8.GetBytes(json));

            var obj = new InvoiceRequest() {
                Details = new [] {
                    new Models.InvoiceDetails() {
                        Description = "原稿料",
                        UnitCost = 10000M,
                        Quantity = 1,
                        Amount = 10000M},
                },
            };
            var json = JsonSerializer.Serialize<InvoiceRequest>(obj);
            return new MemoryStream(Encoding.UTF8.GetBytes(json));
        }

        /// <summary>
        /// 予約済み環境変数からLambdaが実行されているRegionEndpointを取得します
        /// </summary>
        /// <returns></returns>
        private RegionEndpoint GetEnvironmentRegionEndpoint()
        {
            string environmentRegion = Environment.GetEnvironmentVariable("AWS_REGION");
            var endpoint = (from re in RegionEndpoint.EnumerableAllRegions
                            where re.SystemName.Equals(environmentRegion)
                            select re).FirstOrDefault();

            return endpoint;
        }

        /// <summary>
        /// S3にオブジェクトをPutします
        /// </summary>
        /// <param name="regionEndpoint">S3のRegionEndpoint</param>
        /// <param name="bucketName">S3のバケット名</param>
        /// <param name="key">S3に格納するキー</param>
        /// <param name="stream">格納するデータ</param>
        /// <returns></returns>
        private Task<PutObjectResponse> PutObjectS3Async(
            RegionEndpoint regionEndpoint, 
            string bucketName, 
            string key, 
            Stream stream)
        {
            using (var s3c = new AmazonS3Client(regionEndpoint))
            {
                var request = new PutObjectRequest() {
                    BucketName = bucketName,
                    Key = key,
                    InputStream = stream,
                };
                return s3c.PutObjectAsync(request);
            }
        }

        private Task<GetObjectResponse> GetObjectS3Async(
            RegionEndpoint regionEndpoint,
            string bucketName,
            string key)
        {
            using (var s3c = new AmazonS3Client(regionEndpoint))
            {
                return s3c.GetObjectAsync(new GetObjectRequest() {
                    BucketName = bucketName,
                    Key = key,
                });
            }
        }

        private async Task<ListObjectsV2Response> ListObjectS3Async(
            RegionEndpoint regionEndpoint,
            string bucketName,
            string prefix = "")
        {
            using (var s3c = new AmazonS3Client(regionEndpoint))
            {
                return await s3c.ListObjectsV2Async(new ListObjectsV2Request() {
                    BucketName = bucketName,
                    Prefix = prefix,
                });
            }
        }


        /// <summary>
        /// S3オブジェクトのイベントで動作するHandler
        /// </summary>
        /// <param name="s3Event"></param>
        /// <param name="context"></param>
        public async Task S3PutHandlerAsync(S3Event s3Event, ILambdaContext context)
        {
            const string ENV_S3_BUCKET_NAME = "S3_BUCKET_NAME";

            // リージョンの取得
            var regionEndpoint = GetEnvironmentRegionEndpoint();
            if (regionEndpoint == null) 
            {
                throw new Exception("error. don't set environment region.");
            }

            // S3バケット名を環境変数から取得
            var s3bucketName = Environment.GetEnvironmentVariable(ENV_S3_BUCKET_NAME);
            if (string.IsNullOrEmpty(s3bucketName))
            {
                throw new Exception($"error. The environment variable {ENV_S3_BUCKET_NAME} is not set.");
            }

            foreach (var record in s3Event.Records)
            {
                var s3 = record.S3;
                context.Logger.LogLine($"[{record.EventSource} - {record.EventTime}] Bucket = {s3.Bucket.Name}, Key = {s3.Object.Key}");

                using (var response = await GetObjectS3Async(regionEndpoint, s3.Bucket.Name, s3.Object.Key))
                using (var stream = response.ResponseStream)
                {
                    var jsonObj = await JsonSerializer.DeserializeAsync<InvoiceRequest>(stream);

                    //// Debug Log
                    // context.Logger.LogLine($"Hoge : {jsonObj.Hoge}, Hage : {jsonObj.Hage}");
                    // foreach (var detail in jsonObj.Details.OrEmptyIfNull())
                    // {
                    //     context.Logger.LogLine($"Detail : {detail.Name} - {detail.BasePrice} + {detail.TaxPrice}");
                    // }

                    // S3にオブジェクトをPutする
                    var s3dir = s3.Object.Key.Split('/')[0];
                    var s3key = s3dir + $"/{Guid.NewGuid().ToString()}.xlsx";
                    context.Logger.LogLine($"write s3 key : {s3key}");

                    var result = await PutObjectS3Async(regionEndpoint, s3bucketName, s3key, new MemoryStream(CreateExcelInvoice(jsonObj)));
                    context.Logger.LogLine($"put s3 result:{result.HttpStatusCode.ToString()}");
                }
            }   
        }

        private byte[] CreateExcelInvoice(InvoiceRequest invoiceRequest)
        {
            using (var package = new ExcelPackage())
            {
                var sheet = package.Workbook.Worksheets.Add("Invoice");
                sheet.Cells["A1"].Value = invoiceRequest.BillTo.Name;
                sheet.Cells["A2"].Value = invoiceRequest.BillTo.ZipCode;

                var details = from detail in invoiceRequest.Details.OrEmptyIfNull()
                              select new object[] {detail.Description, detail.UnitCost, detail.Quantity,};
                sheet.Cells["A3"].LoadFromArrays(details);

                return package.GetAsByteArray();
            }
        }

        public async Task<APIGatewayProxyResponse> S3GetListHandlerAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            const string ENV_S3_BUCKET_NAME = "S3_BUCKET_NAME";

            // リージョンの取得
            var regionEndpoint = GetEnvironmentRegionEndpoint();
            if (regionEndpoint == null) 
            {
                context.Logger.LogLine("error. don't set environment region.");
                return new APIGatewayProxyResponse() {
                    StatusCode = (int)HttpStatusCode.InternalServerError,
                };
            }

            // S3バケット名を環境変数から取得
            var s3bucketName = Environment.GetEnvironmentVariable(ENV_S3_BUCKET_NAME);
            if (string.IsNullOrEmpty(s3bucketName))
            {
                context.Logger.LogLine($"error. The environment variable {ENV_S3_BUCKET_NAME} is not set.");
                return new APIGatewayProxyResponse() {
                    StatusCode = (int)HttpStatusCode.InternalServerError,
                };
            }

            // Cognito認証情報を取得する
            // https://stackoverflow.com/questions/29928401/how-to-get-the-cognito-identity-id-in-aws-lambda
            var claims = request.RequestContext?.Authorizer?.Claims;
            // Cognito認証情報が取得できたらusernameを取得する
            var userId = claims?["cognito:username"] ?? "public";


            var response = await ListObjectS3Async(regionEndpoint, s3bucketName, userId + "/");
            if (response.HttpStatusCode == HttpStatusCode.OK)
            {
                context.Logger.LogLine($"OK. ListObject S3.");

                var sb = new StringBuilder();
                sb.Append("{\"objects\":[");

                var strs = from obj in response.S3Objects
                           let key = obj.Key.Split('/').Last()
                           let size = obj.Size
                           select "{" + string.Format("\"key\":\"{0}\",\"size\":{1}", key, size) + "}";
                sb.Append(string.Join(',', strs));
                sb.Append("]}");

                return new APIGatewayProxyResponse() {
                    StatusCode = (int)HttpStatusCode.OK,
                    Headers = new Dictionary<string, string>() {
                        {"Access-Control-Allow-Origin", "*"},
                        {"Content-Type", "application/json"},
                    },
                    Body = sb.ToString(),
                };
            }
            else
            {
                context.Logger.LogLine($"error. ListObject S3.");
                return new APIGatewayProxyResponse() {
                    StatusCode = (int)HttpStatusCode.InternalServerError,
                };
            }
        }

        public async Task<APIGatewayProxyResponse> S3GetObjectHandlerAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            const string ENV_S3_BUCKET_NAME = "S3_BUCKET_NAME";

            // リージョンの取得
            var regionEndpoint = GetEnvironmentRegionEndpoint();
            if (regionEndpoint == null) 
            {
                context.Logger.LogLine("error. don't set environment region.");
                return new APIGatewayProxyResponse() {
                    StatusCode = (int)HttpStatusCode.InternalServerError,
                };
            }

            // S3バケット名を環境変数から取得
            var s3bucketName = Environment.GetEnvironmentVariable(ENV_S3_BUCKET_NAME);
            if (string.IsNullOrEmpty(s3bucketName))
            {
                context.Logger.LogLine($"error. The environment variable {ENV_S3_BUCKET_NAME} is not set.");
                return new APIGatewayProxyResponse() {
                    StatusCode = (int)HttpStatusCode.InternalServerError,
                };
            }

            // pathパラメーターが存在しない
            if (!request.PathParameters.ContainsKey("name"))
            {
                context.Logger.LogLine($"error. unset path parameter 'name'.");
                return new APIGatewayProxyResponse() {
                    StatusCode = (int)HttpStatusCode.InternalServerError,
                };
            }

            // Cognito認証情報を取得する
            // https://stackoverflow.com/questions/29928401/how-to-get-the-cognito-identity-id-in-aws-lambda
            var claims = request.RequestContext?.Authorizer?.Claims;
            // Cognito認証情報が取得できたらusernameを取得する
            var userId = claims?["cognito:username"] ?? "public";

            var s3key = userId + "/" + request.PathParameters["name"];
            context.Logger.LogLine($"get object s3 key : {s3key}");

            using(var response = await GetObjectS3Async(regionEndpoint, s3bucketName, s3key))
            {
                if (response.HttpStatusCode == HttpStatusCode.OK)
                {
                    context.Logger.LogLine($"OK. GetObject S3. key : {s3key}");
                    using (var stream = response.ResponseStream)
                    using (var ms = new MemoryStream())
                    {
                        await stream.CopyToAsync(ms);
                        ms.Position = 0;

                        return new APIGatewayProxyResponse() {
                            StatusCode = (int)HttpStatusCode.OK,
                            Headers = new Dictionary<string, string>() {
                                {"Access-Control-Allow-Origin", "*"},
                                {"Content-Type", response.Headers.ContentType},
                            },
                            IsBase64Encoded = true,
                            Body = Convert.ToBase64String(ms.ToArray()),
                        };
                    }
                }
                else
                {
                    context.Logger.LogLine($"error. GetObject S3.");
                    return new APIGatewayProxyResponse() {
                        StatusCode = (int)HttpStatusCode.InternalServerError,
                    };
                }
            }
        }

        // /// <summary>
        // /// Function Handler, Invoice Save DynamoDB
        // /// </summary>
        // /// <param name="request"></param>
        // /// <param name="context"></param>
        // /// <returns></returns>
        // public async Task<APIGatewayProxyResponse> PostInvoiceHandlerAsync(APIGatewayProxyRequest request, ILambdaContext context)
        // {
        //     const string ENV_DYNAMODB_TABLE_NAME = "DYNAMODB_TABLE_NAME";

        //     // DynamoDBのテーブル名を環境変数から取得
        //     var tableName = Environment.GetEnvironmentVariable(ENV_DYNAMODB_TABLE_NAME);
        //     if (string.IsNullOrEmpty(tableName))
        //     {
        //         context.Logger.LogLine($"error. The environment variable {ENV_DYNAMODB_TABLE_NAME} is not set.");
        //         return ProxyResponseUtil.InternalServerError();
        //     }

        //     // Bodyをデシリアライズする
        //     var invoiceRequest = JsonSerializer.Deserialize<InvoiceRequest>(request.Body);
        //     context.Logger.LogLine($"[invoiceRequest] BillTo-Name : {invoiceRequest.BillTo.Name}");

        //     // Cognito認証情報を取得する
        //     // https://stackoverflow.com/questions/29928401/how-to-get-the-cognito-identity-id-in-aws-lambda
        //     var claims = request.RequestContext?.Authorizer?.Claims;
        //     // Cognito認証情報が取得できたらusernameを取得する
        //     var userId = claims?["cognito:username"] ?? "public";

        //     try
        //     {
        //         context.Logger.LogLine($"write DynamoDB Table : {tableName}");

        //         var dbClient = new AmazonDynamoDBClient();
        //         var dbContext = new DynamoDBContext(dbClient);
        //         AWSConfigsDynamoDB.Context.AddMapping(new TypeMapping(typeof(InvoiceItem), tableName));

        //         var item = new InvoiceItem() {
        //             RequestId = Guid.NewGuid().ToString(),
        //             UserId = userId,
        //             Address = new Models.DynamoDB.InvoiceAddress() {
        //                 Name = "Hoge",
        //             },
        //             Details = new List<Models.DynamoDB.InvoiceDetails>() {
        //                 new Models.DynamoDB.InvoiceDetails() {
        //                     Name = "お菓子代",
        //                     BasePrice = 100,
        //                     TaxPrice = 10,
        //                 },
        //             },
        //         };

        //         await dbContext.SaveAsync(item);
                
        //         return ProxyResponseUtil.Create((int)HttpStatusCode.OK);
        //     }
        //     catch (Exception e)
        //     {
        //         context.Logger.LogLine($"error. catch Exception by DynamoDB SaveAsync. {e.Message}");
        //         return ProxyResponseUtil.InternalServerError();
        //     }
        // }
    }
}
