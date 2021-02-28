using System;

namespace ProjectMomo.Models
{
    public class InvoiceRequest
    {
        public string InvoiceNumber { get; set; }

        public DateTime InvoiceDate { get; set; }

        public InvoiceAddress BillTo { get; set; }

        public InvoiceAddress ShipTo { get; set; }

        public InvoiceDetails[] Details { get; set; }
    }

    public class InvoiceAddress
    {
        public string Name { get; set; }

        public string ZipCode { get; set; }
    }

    public class InvoiceDetails
    {
        public string Description { get; set; }

        public string Remarks { get; set; }

        public decimal UnitCost { get; set; }

        public int Quantity { get; set; }

        public decimal Amount { get; set; }
    }
}