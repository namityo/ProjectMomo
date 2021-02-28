using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Util;
using ProjectMomo.Models;
using ProjectMomo.Models.DynamoDB;
using ProjectMomo.Extensions;
using Amazon.DynamoDBv2.Model;

namespace ProjectMomo.Lambda.Controllers
{
    public class InvoiceController
    {
        /// <summary>
        /// Environment variables for DynamoDB table name.
        /// </summary>
        private const string ENV_DYNAMODB_TABLE_NAME = "DYNAMODB_TABLE_NAME";

        /// <summary>
        /// Function Handler, Create Invoice.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task<APIGatewayProxyResponse> CreateAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            // DynamoDBのテーブル名を環境変数から取得
            var tableName = Environment.GetEnvironmentVariable(ENV_DYNAMODB_TABLE_NAME);
            if (string.IsNullOrEmpty(tableName))
            {
                context.Logger.LogLine($"error. The environment variable {ENV_DYNAMODB_TABLE_NAME} is not set.");
                return ProxyResponseUtil.InternalServerError();
            }

            // Bodyをデシリアライズする
            var invoiceRequest = JsonSerializer.Deserialize<InvoiceRequest>(request.Body);
            context.Logger.LogLine($"[invoiceRequest] BillTo-Name : {invoiceRequest.BillTo.Name}");

            // Cognito認証情報を取得する
            var userId = request.GetCognitoUserName();

            try
            {
                context.Logger.LogLine($"write DynamoDB Table : {tableName}");

                var dbClient = new AmazonDynamoDBClient();
                var dbContext = new DynamoDBContext(dbClient);
                AWSConfigsDynamoDB.Context.AddMapping(new TypeMapping(typeof(InvoiceItem), tableName));

                var item = new InvoiceItem() {
                    RequestId = Guid.NewGuid().ToString(),
                    UserId = userId,
                    Address = new Models.DynamoDB.InvoiceAddress() {
                        Name = "Hoge",
                    },
                    Details = new List<Models.DynamoDB.InvoiceDetails>() {
                        new Models.DynamoDB.InvoiceDetails() {
                            Name = "お菓子代",
                            BasePrice = 100,
                            TaxPrice = 10,
                        },
                    },
                };

                await dbContext.SaveAsync(item);
                
                return ProxyResponseUtil.Create((int)HttpStatusCode.OK);
            }
            catch (Exception e)
            {
                context.Logger.LogLine($"error. catch Exception by DynamoDB SaveAsync. {e.Message}");
                return ProxyResponseUtil.InternalServerError();
            }
        }

        /// <summary>
        /// Function Handler, Get Invoice.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task<APIGatewayProxyResponse> GetAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            // DynamoDBのテーブル名を環境変数から取得
            var tableName = Environment.GetEnvironmentVariable(ENV_DYNAMODB_TABLE_NAME);
            if (string.IsNullOrEmpty(tableName))
            {
                context.Logger.LogLine($"error. The environment variable {ENV_DYNAMODB_TABLE_NAME} is not set.");
                return ProxyResponseUtil.InternalServerError();
            }

            // keyを取得する
            var key = request.PathParameters.ContainsKey("key") ? request.PathParameters["key"] : string.Empty;
            if (string.IsNullOrEmpty(key))
            {
                context.Logger.LogLine($"error. key is not set");
                return ProxyResponseUtil.InternalServerError();
            }
            context.Logger.LogLine($"[get] key : {key}");

            // Cognito認証情報を取得する
            var userId = request.GetCognitoUserName();

            try
            {
                context.Logger.LogLine($"get DynamoDB Table : {tableName}");

                var dbClient = new AmazonDynamoDBClient();
                var dbContext = new DynamoDBContext(dbClient);
                AWSConfigsDynamoDB.Context.AddMapping(new TypeMapping(typeof(InvoiceItem), tableName));

                var item = await dbContext.LoadAsync<InvoiceItem>(key);
                
                var result =  ProxyResponseUtil.Create((int)HttpStatusCode.OK);
                result.Body = JsonSerializer.Serialize(item);
                return result;
            }
            catch (Exception e)
            {
                context.Logger.LogLine($"error. catch Exception by DynamoDB SaveAsync. {e.Message}");
                return ProxyResponseUtil.InternalServerError();
            }
        }

        /// <summary>
        /// Function Handler, Get Invoice.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task<APIGatewayProxyResponse> GetListAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            // DynamoDBのテーブル名を環境変数から取得
            var tableName = Environment.GetEnvironmentVariable(ENV_DYNAMODB_TABLE_NAME);
            if (string.IsNullOrEmpty(tableName))
            {
                context.Logger.LogLine($"error. The environment variable {ENV_DYNAMODB_TABLE_NAME} is not set.");
                return ProxyResponseUtil.InternalServerError();
            }

            // Cognito認証情報を取得する
            var userId = request.GetCognitoUserName();

            try
            {
                context.Logger.LogLine($"scan all DynamoDB Table : {tableName}");

                var dbContext = new DynamoDBContext(new AmazonDynamoDBClient());
                AWSConfigsDynamoDB.Context.AddMapping(new TypeMapping(typeof(InvoiceItem), tableName));

                // 全件検索
                var scanResponse = await dbContext.ScanAsync<InvoiceItem>(null).GetRemainingAsync();
                
                var result =  ProxyResponseUtil.Create((int)HttpStatusCode.OK);
                result.Body = JsonSerializer.Serialize(scanResponse);
                return result;
            }
            catch (Exception e)
            {
                context.Logger.LogLine($"error. catch Exception by DynamoDB SaveAsync. {e.Message}");
                return ProxyResponseUtil.InternalServerError();
            }
        }

        /// <summary>
        /// Function Handler, Update Invoice.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task<APIGatewayProxyResponse> UpdateAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            // DynamoDBのテーブル名を環境変数から取得
            var tableName = Environment.GetEnvironmentVariable(ENV_DYNAMODB_TABLE_NAME);
            if (string.IsNullOrEmpty(tableName))
            {
                context.Logger.LogLine($"error. The environment variable {ENV_DYNAMODB_TABLE_NAME} is not set.");
                return ProxyResponseUtil.InternalServerError();
            }

            // Bodyをデシリアライズする
            var key = request.PathParameters.ContainsKey("key") ? request.PathParameters["key"] : string.Empty;
            if (string.IsNullOrEmpty(key))
            {
                context.Logger.LogLine($"error. key is not set");
                return ProxyResponseUtil.InternalServerError();
            }
            context.Logger.LogLine($"[get] key : {key}");

            // Cognito認証情報を取得する
            var userId = request.GetCognitoUserName();

            try
            {
                context.Logger.LogLine($"get DynamoDB Table : {tableName}");

                var dbClient = new AmazonDynamoDBClient();
                var dbContext = new DynamoDBContext(dbClient);
                AWSConfigsDynamoDB.Context.AddMapping(new TypeMapping(typeof(InvoiceItem), tableName));

                var item = await dbContext.LoadAsync<InvoiceItem>(key);
                
                var item2 = new InvoiceItem() {
                    RequestId = item.RequestId,
                    UserId = userId,
                    Address = new Models.DynamoDB.InvoiceAddress() {
                        Name = "Hoge",
                    },
                    Details = new List<Models.DynamoDB.InvoiceDetails>() {
                        new Models.DynamoDB.InvoiceDetails() {
                            Name = "おみやげ代",
                            BasePrice = 200,
                            TaxPrice = 20,
                        },
                    },
                };

                await dbContext.SaveAsync(item2);
                return ProxyResponseUtil.Create((int)HttpStatusCode.OK);
            }
            catch (Exception e)
            {
                context.Logger.LogLine($"error. catch Exception by DynamoDB SaveAsync. {e.Message}");
                return ProxyResponseUtil.InternalServerError();
            }
        }
        
        /// <summary>
        /// Function Handler, Delete Invoice.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task<APIGatewayProxyResponse> DeleteAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            // DynamoDBのテーブル名を環境変数から取得
            var tableName = Environment.GetEnvironmentVariable(ENV_DYNAMODB_TABLE_NAME);
            if (string.IsNullOrEmpty(tableName))
            {
                context.Logger.LogLine($"error. The environment variable {ENV_DYNAMODB_TABLE_NAME} is not set.");
                return ProxyResponseUtil.InternalServerError();
            }

            // Bodyをデシリアライズする
            var key = request.PathParameters.ContainsKey("key") ? request.PathParameters["key"] : string.Empty;
            if (string.IsNullOrEmpty(key))
            {
                context.Logger.LogLine($"error. key is not set");
                return ProxyResponseUtil.InternalServerError();
            }
            context.Logger.LogLine($"[get] key : {key}");

            // Cognito認証情報を取得する
            var userId = request.GetCognitoUserName();

            try
            {
                context.Logger.LogLine($"delete item in DynamoDB Table : {tableName}");

                var dbClient = new AmazonDynamoDBClient();
                var dbContext = new DynamoDBContext(dbClient);
                AWSConfigsDynamoDB.Context.AddMapping(new TypeMapping(typeof(InvoiceItem), tableName));

                var item = new InvoiceItem() {
                    RequestId = key,
                };
                await dbContext.DeleteAsync(item);

                return ProxyResponseUtil.Create((int)HttpStatusCode.OK);
            }
            catch (Exception e)
            {
                context.Logger.LogLine($"error. catch Exception by DynamoDB SaveAsync. {e.Message}");
                return ProxyResponseUtil.InternalServerError();
            }
        }
    }
}