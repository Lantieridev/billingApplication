using BillingApplication.Data.Interfaces;
using BillingApplication.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks;

namespace BillingApplication.Services.Interfaces
{
    public interface IProductService
    {
        Task<int> CreateProductAsync(Product product);
        Task<Product> GetProductByIdAsync(int id);
        Task<IEnumerable<Product>> GetAllProductsAsync();
    }
}