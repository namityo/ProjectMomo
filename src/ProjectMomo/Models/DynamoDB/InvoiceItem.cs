using System.Collections.Generic;
using Amazon.DynamoDBv2.DataModel;

namespace ProjectMomo.Models.DynamoDB
{
    [DynamoDBTable("Invoice")]
    public class InvoiceItem
    {
        [DynamoDBHashKey("RequestId")]
        public string RequestId { get; set; }

        public string UserId { get; set; }

        public InvoiceAddress Address { get; set; }

        public List<InvoiceDetails> Details { get; set; }
    }

    public class InvoiceAddress
    {
        public string Name { get; set; }
    }

    public class InvoiceDetails
    {
        public string Name { get; set; }

        public decimal BasePrice { get; set; }

        public decimal TaxPrice { get; set; }
    }
}