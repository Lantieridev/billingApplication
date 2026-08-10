using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BillingApplication.Domain.Entities;

namespace BillingApplication.Domain.Entities
{
    public class Invoice
    {
        public int Id { get; set; }
        public string NumeroFactura { get; set; } = null!;
        public DateTime Fecha { get; set; } = DateTime.Now;
        public int ClienteId { get; set; }
        public int FormaPagoId { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Total { get; set; }

        // Navigation properties (solo para uso en la aplicación)
        public Customer Customer { get; set; } = null!;
        public PaymentMethod PaymentMethod { get; set; } = null!;
        public List<InvoiceDetail> InvoiceDetails { get; set; } = new List<InvoiceDetail>();
    }
}

