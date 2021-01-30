namespace ProjectMomo.Models
{
    public class InvoiceRequest
    {
        public string Hoge { get; set; }

        public string Hage { get; set; }

        public InvoiceDetails[] Details { get; set; }
    }

    public class InvoiceDetails
    {
        public string Name { get; set; }

        public decimal BasePrice { get; set; }

        public decimal TaxPrice { get; set; }
    }
}