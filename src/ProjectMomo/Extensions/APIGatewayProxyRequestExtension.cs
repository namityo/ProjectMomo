using Amazon.Lambda.APIGatewayEvents;

namespace ProjectMomo.Extensions
{
    public static class APIGatewayProxyRequestExtension
    {
        public static string GetCognitoUserName(this APIGatewayProxyRequest request)
        {
            // Cognito認証情報を取得する
            // https://stackoverflow.com/questions/29928401/how-to-get-the-cognito-identity-id-in-aws-lambda
            var claims = request.RequestContext?.Authorizer?.Claims;
            // Cognito認証情報が取得できたらusernameを取得する
            var userId = (claims?.ContainsKey("cognito:username") ?? false) ? claims["cognito:username"] : "public";

            return userId;
        }
    }
}