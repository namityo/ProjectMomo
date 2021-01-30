using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Xunit;
using Amazon.Lambda.Core;
using Amazon.Lambda.TestUtilities;

using ProjectMomo;
using Amazon.Lambda.APIGatewayEvents;

namespace ProjectMomo.Tests
{
    public class FunctionTest
    {
        [Fact]
        public void TestToUpperFunction()
        {

            // Invoke the lambda function and confirm the string was upper cased.
            var function = new Function();
            var context = new TestLambdaContext();
            var upperCase = function.FunctionHandler("hello world", context);

            Assert.Equal("HELLO WORLD", upperCase);
        }

        [Fact]
        public void TestS3FunctionHandler()
        {
            Environment.SetEnvironmentVariable("AWS_REGION", "ap-northeast-1");

            var function = new Function();
            // var result = function.S3FunctionHandler(new APIGatewayProxyRequest(), new TestLambdaContext());

            //Assert.Equal(200, result.StatusCode);
        }

        [Fact]
        public void TestCreateExcelInvoice()
        {
            var function = new Function();
            var data = function.CreateExcelInvoice(new Models.InvoiceRequest() {
                Hoge = "hoge",
                Hage = "hage",
                Details = new Models.InvoiceDetails[] {
                    new Models.InvoiceDetails() { Name = "aaa", BasePrice = 1000M, TaxPrice = 100M},
                    new Models.InvoiceDetails() { Name = "bbb", BasePrice = 2000M, TaxPrice = 200M},
                    new Models.InvoiceDetails() { Name = "ccc", BasePrice = 3000M, TaxPrice = 300M},
                }
            });

            System.IO.File.WriteAllBytes("test.xlsx", data);
        }
    }
}
