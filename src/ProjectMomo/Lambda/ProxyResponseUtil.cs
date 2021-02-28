using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Amazon.Lambda.APIGatewayEvents;

namespace ProjectMomo.Lambda
{
    public class ProxyResponseUtil
    {
        const string ENV_ACCESS_CONTROL_ALOOW_ORIGIN = "ACCESS_CONTROL_ALOOW_ORIGIN";

        private static string GetEnvironmentAccessControlAllowOrigin()
        {
            return Environment.GetEnvironmentVariable(ENV_ACCESS_CONTROL_ALOOW_ORIGIN);
        }

        private static IEnumerable<KeyValuePair<string, string>> Headers()
        {
            var acao = GetEnvironmentAccessControlAllowOrigin();
            if (!string.IsNullOrEmpty(acao)) yield return new KeyValuePair<string, string>("Access-Control-Allow-Origin", acao);
        }

        private static Dictionary<string, string> BuildHeaders()
        {
            return Headers().ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        public static APIGatewayProxyResponse Create(int StatusCode)
        {
            return new APIGatewayProxyResponse() {
                StatusCode = StatusCode,
                Headers = BuildHeaders(),
            };
        }

        public static APIGatewayProxyResponse InternalServerError()
        {
            return Create((int)HttpStatusCode.InternalServerError);
        }
    }
}