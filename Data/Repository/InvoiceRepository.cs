using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Dapper;
using BillingApplication.Domain.Entities;
using BillingApplication.Data.Interfaces;

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
            var sql = @"
                INSERT INTO Facturas (NumeroFactura, Fecha, ClienteId, FormaPagoId, Subtotal, Total)
                VALUES (@NumeroFactura, @Fecha, @ClienteId, @FormaPagoId, @Subtotal, @Total);
                SELECT CAST(SCOPE_IDENTITY() as int)";

            return await connection.ExecuteScalarAsync<int>(sql, invoice);
        }

        public async Task<int> AddInvoiceDetailAsync(InvoiceDetail detail)
        {
            using var connection = _context.CreateConnection();
            var sql = @"
                INSERT INTO DetallesFactura (FacturaId, ArticuloId, Cantidad, PrecioUnidad, Subtotal)
                VALUES (@FacturaId, @ProductoId, @Cantidad, @PrecioUnidad, @Subtotal);
                SELECT CAST(SCOPE_IDENTITY() as int)";

            return await connection.ExecuteScalarAsync<int>(sql, detail);
        }

        public async Task UpdateStockAsync(int productId, int quantity, string operation)
        {
            if (operation != "INCREMENT" && operation != "DECREMENT")
                throw new ArgumentException("Operation must be 'INCREMENT' or 'DECREMENT'", nameof(operation));

            using var connection = _context.CreateConnection();
            var opSql = operation == "INCREMENT" ? "+" : "-";
            var sql = $"UPDATE Articulos SET Stock = Stock {opSql} @Cantidad WHERE Id = @ArticuloId";

            await connection.ExecuteAsync(sql, new { ArticuloId = productId, Cantidad = quantity });
        }

        public async Task<int> CreateInvoiceTransactionAsync(Invoice invoice, List<InvoiceDetail> details)
        {
            using var connection = _context.CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();

            try
            {
                var invoiceSql = @"
                    INSERT INTO Facturas (NumeroFactura, Fecha, ClienteId, FormaPagoId, Subtotal, Total)
                    VALUES (@NumeroFactura, @Fecha, @ClienteId, @FormaPagoId, @Subtotal, @Total);
                    SELECT CAST(SCOPE_IDENTITY() as int)";

                var invoiceId = await connection.ExecuteScalarAsync<int>(invoiceSql, invoice, transaction: transaction);

                foreach (var detail in details)
                {
                    detail.FacturaId = invoiceId;
                    var detailSql = @"
                        INSERT INTO DetallesFactura (FacturaId, ArticuloId, Cantidad, PrecioUnidad, Subtotal)
                        VALUES (@FacturaId, @ProductoId, @Cantidad, @PrecioUnidad, @Subtotal);
                        SELECT CAST(SCOPE_IDENTITY() as int)";

                    await connection.ExecuteScalarAsync<int>(detailSql, detail, transaction: transaction);

                    var stockSql = "UPDATE Articulos SET Stock = Stock - @Cantidad WHERE Id = @ProductoId";
                    await connection.ExecuteAsync(stockSql, new { Cantidad = detail.Cantidad, ProductoId = detail.ProductoId }, transaction: transaction);
                }

                transaction.Commit();
                return invoiceId;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
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

            return (await connection.QueryFirstOrDefaultAsync<Invoice>(sql, new { Id = id }))!;
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
                // DetallesFactura and Articulos both have a primary key literally named "Id" —
                // aliasing DetalleFactura's own Id avoids ambiguity in Dapper's splitOn boundary,
                // which otherwise treats the very first "Id" column (df.Id) as the split point.
                var details = await connection.QueryAsync<InvoiceDetail, Product, InvoiceDetail>(
                    @"SELECT df.Id AS DetalleId, df.FacturaId, df.ArticuloId AS ProductoId, df.Cantidad, df.PrecioUnidad, df.Subtotal,
                             p.Id, p.Codigo, p.Nombre, p.Descripcion, p.PrecioUnitario, p.Stock, p.Activo
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

            return invoice!;
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
                SET NumeroFactura = @NumeroFactura, 
                    Fecha = @Fecha, 
                    ClienteId = @ClienteId, 
                    FormaPagoId = @FormaPagoId, 
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

