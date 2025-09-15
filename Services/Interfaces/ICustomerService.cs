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
    public interface ICustomerService
    {
        Task<int> CreateCustomerAsync(Customer customer);
        Task<Customer> GetCustomerByIdAsync(int id);
        Task<IEnumerable<Customer>> GetAllCustomersAsync();
    }
}