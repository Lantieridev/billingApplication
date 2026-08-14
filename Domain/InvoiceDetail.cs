using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BillingApplication.Domain.Entities
{
    public class InvoiceDetail
    {
        public int Id { get; set; }
        public int FacturaId { get; set; }
        public int ProductoId { get; set; }
        public int Cantidad { get; set; }
        public decimal PrecioUnidad { get; set; }
        public decimal Subtotal { get; set; }

        // Navigation property
        public Product Product { get; set; } = null!;
    }
}

