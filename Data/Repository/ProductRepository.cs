using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using BillingApplication.Domain.Entities;
using Dapper;
using BillingApplication.Data.Interfaces;

namespace BillingApplication.Data.Repositories
{
    public class ProductRepository : IProductRepository
    {
        private readonly DapperContext _context;

        public ProductRepository(DapperContext context)
        {
            _context = context;
        }

        public async Task<Product> GetByIdAsync(int id)
        {
            using var connection = _context.CreateConnection();
            var sql = "SELECT * FROM Articulos WHERE Id = @Id AND Activo = 1";
            return (await connection.QueryFirstOrDefaultAsync<Product>(sql, new { Id = id }))!;
        }

        public async Task<IEnumerable<Product>> GetAllAsync()
        {
            using var connection = _context.CreateConnection();
            var sql = "SELECT * FROM Articulos WHERE Activo = 1 ORDER BY Nombre";
            return await connection.QueryAsync<Product>(sql);
        }

        public async Task<int> AddAsync(Product product)
        {
            using var connection = _context.CreateConnection();
            var sql = @"
                INSERT INTO Articulos (Codigo, Nombre, Descripcion, PrecioUnitario, Stock, Activo)
                VALUES (@Code, @Name, @Description, @UnitPrice, @Stock, @Active);
                SELECT CAST(SCOPE_IDENTITY() as int)";

            return await connection.ExecuteScalarAsync<int>(sql, new
            {
                Code = product.Codigo,
                Name = product.Nombre,
                Description = product.Descripcion,
                UnitPrice = product.PrecioUnitario,
                Stock = product.Stock,
                Active = product.Activo
            });
        }

        public async Task UpdateAsync(Product product)
        {
            using var connection = _context.CreateConnection();
            var sql = @"
                UPDATE Articulos 
                SET Codigo = @Code, 
                    Nombre = @Name, 
                    Descripcion = @Description, 
                    PrecioUnitario = @UnitPrice, 
                    Stock = @Stock
                WHERE Id = @Id";

            await connection.ExecuteAsync(sql, new
            {
                Id = product.Id,
                Code = product.Codigo,
                Name = product.Nombre,
                Description = product.Descripcion,
                UnitPrice = product.PrecioUnitario,
                Stock = product.Stock
            });
        }

        public async Task DeleteAsync(int id)
        {
            using var connection = _context.CreateConnection();
            var sql = "UPDATE Articulos SET Activo = 0 WHERE Id = @Id";
            await connection.ExecuteAsync(sql, new { Id = id });
        }

        public async Task<Product> GetByCodeAsync(string code)
        {
            using var connection = _context.CreateConnection();
            var sql = "SELECT * FROM Articulos WHERE Codigo = @Code AND Activo = 1";
            return (await connection.QueryFirstOrDefaultAsync<Product>(sql, new { Code = code }))!;
        }

        public async Task<IEnumerable<Product>> GetLowStockProductsAsync(int threshold = 10)
        {
            using var connection = _context.CreateConnection();
            var sql = "SELECT * FROM Articulos WHERE Stock <= @Threshold AND Activo = 1 ORDER BY Stock";
            return await connection.QueryAsync<Product>(sql, new { Threshold = threshold });
        }
    }
}



