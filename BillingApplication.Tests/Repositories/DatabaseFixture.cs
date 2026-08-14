using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Testcontainers.MsSql;
using Xunit;
using BillingApplication.Data;
using Dapper;

namespace BillingApplication.Tests.Repositories
{
    public class DatabaseFixture : IAsyncLifetime
    {
        private readonly MsSqlContainer _msSqlContainer;

        public DatabaseFixture()
        {
            _msSqlContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
                .WithPassword("StrongP@ssw0rd!")
                .Build();
        }

        public string ConnectionString => _msSqlContainer.GetConnectionString();

        // This gives us the connection string that explicitly selects BillingSystem,
        // once it has been created by the initialization script.
        public string AppConnectionString => new SqlConnectionStringBuilder(ConnectionString) 
        { 
            InitialCatalog = "BillingSystem" 
        }.ConnectionString;

        public DapperContext CreateContext()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    {"ConnectionStrings:DefaultConnection", AppConnectionString}
                })
                .Build();
            return new DapperContext(config);
        }

        public async Task InitializeAsync()
        {
            await _msSqlContainer.StartAsync();
            
            var scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "billingApplicationsql.sql");
            var script = await File.ReadAllTextAsync(scriptPath);
            
            using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();

            // Split only on lines that consist solely of "GO" (the real T-SQL batch separator) —
            // a naive substring split on "GO" would also match inside identifiers like "FormasPago".
            var batches = Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            foreach (var batch in batches)
            {
                if (string.IsNullOrWhiteSpace(batch)) continue;
                using var cmd = new SqlCommand(batch, connection);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        public async Task DisposeAsync()
        {
            await _msSqlContainer.DisposeAsync();
        }

        public async Task ResetDatabaseAsync()
        {
            using var connection = new SqlConnection(AppConnectionString);
            await connection.OpenAsync();
            await connection.ExecuteAsync(@"
                DELETE FROM DetallesFactura;
                DELETE FROM Facturas;
                DELETE FROM Articulos;
                DELETE FROM Clientes;
                DELETE FROM FormasPago;
            ");
        }
    }

    [CollectionDefinition("Database collection")]
    public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }
}
