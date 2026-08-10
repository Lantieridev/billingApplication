using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Dapper;
using System.Data;
using System.Threading.Tasks;
using System.Collections.Generic;
using BillingApplication.Domain.Entities;
using BillingApplication.Data.Interfaces;
using System.Runtime.InteropServices;

namespace BillingApplication.Data.Repositories
{
    public class InvoiceRepository : IInvoiceRepository
    {
        private readonly DapperContext _context;

        public InvoiceRepository(DapperContext context)
        {
            _context = context;
        }

        public async Task<int> AddAsync(Invoice invoice)
        {
            using var connection = _context.CreateConnection();
            var parameters = new DynamicParameters();

            parameters.Add("@NumeroFactura", invoice.NumeroFactura);
            parameters.Add("@Fecha", invoice.Fecha);
            parameters.Add("@ClienteId", invoice.ClienteId);
            parameters.Add("@FormaPagoId", invoice.FormaPagoId);
            parameters.Add("@Subtotal", invoice.Subtotal);
            parameters.Add("@Total", invoice.Total);
            parameters.Add("@NuevaFacturaId", dbType: DbType.Int32, direction: ParameterDirection.Output);

            await connection.ExecuteAsync("sp_Facturas_Insert", parameters,
                commandType: CommandType.StoredProcedure);

            return parameters.Get<int>("@NuevaFacturaId");
        }

        public async Task<int> AddInvoiceDetailAsync(InvoiceDetail detail)
        {
            using var connection = _context.CreateConnection();
            var parameters = new DynamicParameters();

            parameters.Add("@FacturaId", detail.FacturaId);
            parameters.Add("@ArticuloId", detail.ProductoId);
            parameters.Add("@Cantidad", detail.Cantidad);
            parameters.Add("@PrecioUnitario", detail.PrecioUnidad);
            parameters.Add("@Subtotal", detail.Subtotal);
            parameters.Add("@NuevoDetalleId", dbType: DbType.Int32, direction: ParameterDirection.Output);

            await connection.ExecuteAsync("sp_DetallesFactura_Insert", parameters,
                commandType: CommandType.StoredProcedure);

            return parameters.Get<int>("@NuevoDetalleId");
        }

        public async Task UpdateStockAsync(int productId, int quantity, string operation)
        {
            using var connection = _context.CreateConnection();
            var parameters = new DynamicParameters();

            parameters.Add("@ArticuloId", productId);
            parameters.Add("@Cantidad", quantity);
            parameters.Add("@Operacion", operation);

            await connection.ExecuteAsync("sp_Articulos_UpdateStock", parameters,
                commandType: CommandType.StoredProcedure);
        }

        public async Task<Invoice> GetByIdAsync(int id)
        {
            using var connection = _context.CreateConnection();

            var sql = @"
                SELECT f.*, c.Nombre as CustomerName, pm.Nombre as PaymentMethodName
                FROM Facturas f
                INNER JOIN Clientes c ON f.ClienteId = c.Id
                INNER JOIN FormasPago pm ON f.FormaPagoId = pm.Id
                WHERE f.Id = @Id";

            return await connection.QueryFirstOrDefaultAsync<Invoice>(sql, new { Id = id });
        }

        public async Task<Invoice> GetInvoiceWithDetailsAsync(int id)
        {
            using var connection = _context.CreateConnection();

            var invoice = await connection.QueryFirstOrDefaultAsync<Invoice>(
                @"SELECT f.*, c.Nombre as CustomerName, pm.Nombre as PaymentMethodName
                  FROM Facturas f
                  INNER JOIN Clientes c ON f.ClienteId = c.Id
                  INNER JOIN FormasPago pm ON f.FormaPagoId = pm.Id
                  WHERE f.Id = @Id", new { Id = id });

            if (invoice != null)
            {
                var details = await connection.QueryAsync<InvoiceDetail, Product, InvoiceDetail>(
                    @"SELECT df.*, p.*
                      FROM DetallesFactura df
                      INNER JOIN Articulos p ON df.ArticuloId = p.Id
                      WHERE df.FacturaId = @Id",
                    (detail, product) => {
                        detail.Product = product;
                        return detail;
                    },
                    new { Id = id },
                    splitOn: "Id"
                );

                invoice.InvoiceDetails = details.AsList();
            }

            return invoice;
        }

        public async Task<IEnumerable<Invoice>> GetAllAsync()
        {
            using var connection = _context.CreateConnection();

            var sql = @"
                SELECT f.*, c.Nombre as CustomerName, pm.Nombre as PaymentMethodName
                FROM Facturas f
                INNER JOIN Clientes c ON f.ClienteId = c.Id
                INNER JOIN FormasPago pm ON f.FormaPagoId = pm.Id
                ORDER BY f.Fecha DESC";

            return await connection.QueryAsync<Invoice>(sql);
        }

        public async Task<string> GenerateNextInvoiceNumberAsync()
        {
            using var connection = _context.CreateConnection();

            var ultimoNumero = await connection.QueryFirstOrDefaultAsync<string>(
                "SELECT MAX(NumeroFactura) AS NumeroFactura FROM Facturas");

            var prefijo = $"FACT-{DateTime.Now.Year}-";

            if (string.IsNullOrEmpty(ultimoNumero))
                return $"{prefijo}1";

            return $"{prefijo}{int.Parse(ultimoNumero.Substring(10)) + 1}";
        }

        public async Task<bool> InvoiceNumberExistsAsync(string invoiceNumber)
        {
            using var connection = _context.CreateConnection();

            var sql = "SELECT COUNT(1) FROM Facturas WHERE NumeroFactura = @InvoiceNumber";
            var count = await connection.ExecuteScalarAsync<int>(sql, new { InvoiceNumber = invoiceNumber });

            return count > 0;
        }

        public async Task UpdateAsync(Invoice invoice)
        {
            using var connection = _context.CreateConnection();

            var sql = @"
                UPDATE Facturas 
                SET NumeroFactura = @InvoiceNumber, 
                    Fecha = @Date, 
                    ClienteId = @CustomerId, 
                    FormaPagoId = @PaymentMethodId, 
                    Subtotal = @Subtotal, 
                    Total = @Total 
                WHERE Id = @Id";

            await connection.ExecuteAsync(sql, invoice);
        }

        public async Task DeleteAsync(int id)
        {
            using var connection = _context.CreateConnection();

            var sql = "DELETE FROM Facturas WHERE Id = @Id";
            await connection.ExecuteAsync(sql, new { Id = id });
        }
    }
}