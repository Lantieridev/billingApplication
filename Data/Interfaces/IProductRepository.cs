using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BillingApplication.Domain.Entities;

namespace BillingApplication.Data.Interfaces
{
    public interface IProductRepository : IRepository<Product>
    {

        Task<Product> GetByCodeAsync(string code);
        Task<IEnumerable<Product>> GetLowStockProductsAsync(int threshold = 10);
    }
}

