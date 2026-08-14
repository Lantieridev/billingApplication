using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using BillingApplication.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BillingApplication.Data.Interfaces
{
    public interface ICustomerRepository : IRepository<Customer>
    {

        Task<IEnumerable<Customer>> SearchAsync(string searchTerm);
        Task<Customer> GetByEmailAsync(string email);
    }
}


