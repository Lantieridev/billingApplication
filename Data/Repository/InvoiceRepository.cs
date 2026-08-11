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
                throw new ArgumentException("La operación debe ser 'INCREMENT' o 'DECREMENT'", nameof(operation));

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

                    // InvoiceService checks stock before starting this transaction, but that check
                    // and this update aren't atomic -- two concurrent invoices for the last unit
                    // could both pass the earlier check. The "AND Stock >= @Cantidad" guard makes
                    // the deduction itself the source of truth: if another transaction already
                    // consumed the stock, this UPDATE matches zero rows and we roll back instead
                    // of driving Stock negative.
                    var stockSql = "UPDATE Articulos SET Stock = Stock - @Cantidad WHERE Id = @ProductoId AND Stock >= @Cantidad";
                    var stockRowsAffected = await connection.ExecuteAsync(stockSql, new { Cantidad = detail.Cantidad, ProductoId = detail.ProductoId }, transaction: transaction);
                    if (stockRowsAffected == 0)
                    {
                        throw new InvalidOperationException($"Stock insuficiente para el producto ID {detail.ProductoId}.");
                    }
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

        // f.*, c.Nombre AS CustomerName, pm.Nombre AS PaymentMethodName used to be selected here,
        // but Invoice has no CustomerName/PaymentMethodName properties -- Dapper's simple
        // QueryFirstOrDefaultAsync<Invoice> silently ignored those columns, leaving
        // invoice.Customer/PaymentMethod null and crashing "Ver Detalle de Factura" with a
        // NullReferenceException. Fixed with explicit multi-mapping, same pattern already used
        // below for InvoiceDetail/Product.
        private const string InvoiceWithCustomerAndPaymentMethodSql = @"
            SELECT f.Id, f.NumeroFactura, f.Fecha, f.ClienteId, f.FormaPagoId, f.Subtotal, f.Total,
                   c.Id, c.Nombre, c.Direccion, c.Telefono, c.Email, c.Activo,
                   pm.Id, pm.Nombre, pm.Activo
            FROM Facturas f
            INNER JOIN Clientes c ON f.ClienteId = c.Id
            INNER JOIN FormasPago pm ON f.FormaPagoId = pm.Id
            WHERE f.Id = @Id";

        private static Invoice MapInvoiceWithCustomerAndPaymentMethod(Invoice invoice, Customer customer, PaymentMethod paymentMethod)
        {
            invoice.Customer = customer;
            invoice.PaymentMethod = paymentMethod;
            return invoice;
        }

        public async Task<Invoice> GetByIdAsync(int id)
        {
            using var connection = _context.CreateConnection();

            var result = await connection.QueryAsync<Invoice, Customer, PaymentMethod, Invoice>(
                InvoiceWithCustomerAndPaymentMethodSql,
                MapInvoiceWithCustomerAndPaymentMethod,
                new { Id = id },
                splitOn: "Id,Id");

            return result.FirstOrDefault()!;
        }

        public async Task<Invoice> GetInvoiceWithDetailsAsync(int id)
        {
            using var connection = _context.CreateConnection();

            var invoiceResult = await connection.QueryAsync<Invoice, Customer, PaymentMethod, Invoice>(
                InvoiceWithCustomerAndPaymentMethodSql,
                MapInvoiceWithCustomerAndPaymentMethod,
                new { Id = id },
                splitOn: "Id,Id");
            var invoice = invoiceResult.FirstOrDefault();

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
                SELECT f.Id, f.NumeroFactura, f.Fecha, f.ClienteId, f.FormaPagoId, f.Subtotal, f.Total,
                       c.Id, c.Nombre, c.Direccion, c.Telefono, c.Email, c.Activo,
                       pm.Id, pm.Nombre, pm.Activo
                FROM Facturas f
                INNER JOIN Clientes c ON f.ClienteId = c.Id
                INNER JOIN FormasPago pm ON f.FormaPagoId = pm.Id
                ORDER BY f.Fecha DESC";

            return await connection.QueryAsync<Invoice, Customer, PaymentMethod, Invoice>(
                sql,
                MapInvoiceWithCustomerAndPaymentMethod,
                splitOn: "Id,Id");
        }

        public async Task<string> GenerateNextInvoiceNumberAsync()
        {
            using var connection = _context.CreateConnection();

            var prefijo = $"FACT-{DateTime.Now.Year}-";

            // MAX(NumeroFactura) used to do a lexicographic (not numeric) string comparison in SQL
            // Server, so "FACT-2026-9" sorts above "FACT-2026-10" -- past the 9th invoice of a
            // year, MAX() kept returning "FACT-2026-9" forever, generating the same (already-taken)
            // number repeatedly. It also ignored the year filter entirely, so a prior year's higher
            // suffix could leak into this year's numbering. Fetching every number for the current
            // year's prefix and comparing the parsed suffixes in C# avoids both problems.
            var numerosDelAnio = await connection.QueryAsync<string>(
                "SELECT NumeroFactura FROM Facturas WHERE NumeroFactura LIKE @Prefijo + '%'",
                new { Prefijo = prefijo });

            var maxNumero = numerosDelAnio
                .Select(n => int.TryParse(n.Substring(prefijo.Length), out var num) ? num : (int?)null)
                .Where(num => num.HasValue)
                .Select(num => num!.Value)
                .DefaultIfEmpty(0)
                .Max();

            return $"{prefijo}{maxNumero + 1}";
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

