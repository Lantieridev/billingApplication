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
    public class CustomerRepository : ICustomerRepository
    {
        private readonly DapperContext _context;

        public CustomerRepository(DapperContext context)
        {
            _context = context;
        }

        public async Task<Customer> GetByIdAsync(int id)
        {
            using var connection = _context.CreateConnection();
            var sql = "SELECT * FROM Clientes WHERE Id = @Id AND Activo = 1";
            return (await connection.QueryFirstOrDefaultAsync<Customer>(sql, new { Id = id }))!;
        }

        public async Task<IEnumerable<Customer>> GetAllAsync()
        {
            using var connection = _context.CreateConnection();
            var sql = "SELECT * FROM Clientes WHERE Activo = 1 ORDER BY Nombre";
            return await connection.QueryAsync<Customer>(sql);
        }

        public async Task<int> AddAsync(Customer customer)
        {
            using var connection = _context.CreateConnection();
            var sql = @"
                INSERT INTO Clientes (Nombre, Direccion, Telefono, Email, Activo)
                VALUES (@Name, @Address, @Phone, @Email, @Active);
                SELECT CAST(SCOPE_IDENTITY() as int)";

            return await connection.ExecuteScalarAsync<int>(sql, new
            {
                Name = customer.Nombre,
                Address = customer.Direccion,
                Phone = customer.Telefono,
                Email = customer.Email,
                Active = customer.Activo
            });
        }

        public async Task UpdateAsync(Customer customer)
        {
            using var connection = _context.CreateConnection();
            var sql = @"
                UPDATE Clientes 
                SET Nombre = @Name, 
                    Direccion = @Address, 
                    Telefono = @Phone, 
                    Email = @Email
                WHERE Id = @Id";

            await connection.ExecuteAsync(sql, new
            {
                Id = customer.Id,
                Name = customer.Nombre,
                Address = customer.Direccion,
                Phone = customer.Telefono,
                Email = customer.Email
            });
        }

        public async Task DeleteAsync(int id)
        {
            using var connection = _context.CreateConnection();
            var sql = "UPDATE Clientes SET Activo = 0 WHERE Id = @Id";
            await connection.ExecuteAsync(sql, new { Id = id });
        }

        public async Task<IEnumerable<Customer>> SearchAsync(string searchTerm)
        {
            using var connection = _context.CreateConnection();
            var sql = @"
                SELECT * FROM Clientes 
                WHERE Activo = 1 
                AND (Nombre LIKE '%' + @SearchTerm + '%' 
                     OR Email LIKE '%' + @SearchTerm + '%' 
                     OR Telefono LIKE '%' + @SearchTerm + '%')
                ORDER BY Nombre";

            return await connection.QueryAsync<Customer>(sql, new { SearchTerm = searchTerm });
        }

        public async Task<Customer> GetByEmailAsync(string email)
        {
            using var connection = _context.CreateConnection();
            var sql = "SELECT * FROM Clientes WHERE Email = @Email AND Activo = 1";
            return (await connection.QueryFirstOrDefaultAsync<Customer>(sql, new { Email = email }))!;
        }
    }
}

