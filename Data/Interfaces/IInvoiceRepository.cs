using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading.Tasks;
using BillingApplication.Domain.Entities;

namespace BillingApplication.Data.Interfaces
{
    public interface IInvoiceRepository : IRepository<Invoice>
    {
        Task<string> GenerateNextInvoiceNumberAsync();
        Task<bool> InvoiceNumberExistsAsync(string invoiceNumber);
        Task<Invoice> GetInvoiceWithDetailsAsync(int id);
        Task<int> AddInvoiceDetailAsync(InvoiceDetail detail);
        Task UpdateStockAsync(int productId, int quantity, string operation);
        Task<int> CreateInvoiceTransactionAsync(Invoice invoice, List<InvoiceDetail> details);
    }
}
