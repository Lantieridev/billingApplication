using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BillingApplication.Domain.Entities
{
    public class Product
    {
        public int Id { get; set; }
        public string Codigo { get; set; } = null!;
        public string Nombre { get; set; } = null!;
        public string Descripcion { get; set; } = null!;
        public decimal PrecioUnitario { get; set; }
        public int Stock { get; set; }
        public bool Activo { get; set; } = true;
    }
}

