using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Text;
using Amazon;
using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Lambda.APIGatewayEvents;
using System.Net;

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
        public APIGatewayProxyResponse S3FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
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
            // TODO:Debug
            if (claims != null)
            {
                foreach (var kv in claims)
                {
                    context.Logger.LogLine($"claims[{kv.Key}] = {claims[kv.Value]}");
                }
            }
            var userId = string.Empty;

            try
            {
                // ユーザー名をフォルダ名にする。認証が通ってない場合はpublic
                var dirName = string.IsNullOrEmpty(userId) ? "public" : userId;
                var s3key = dirName + $"/{Guid.NewGuid().ToString()}.json";
                context.Logger.LogLine($"write s3 key : {s3key}");

                // S3にオブジェクトをPutする
                var result = PutObjectS3Async(regionEndpoint, s3bucketName, s3key, CreateStream("hoge"));
                return new APIGatewayProxyResponse() {
                    StatusCode = (int)HttpStatusCode.OK,
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
            var json = "{\"user_id\":\"" + userId + "\"}";
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
                var request = new PutObjectRequest()
                {
                    BucketName = bucketName,
                    Key = key,
                    InputStream = stream,
                };
                return s3c.PutObjectAsync(request);
            }
        }
    }
}
