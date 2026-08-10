using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BillingApplication.Data;
using BillingApplication.Data.Repositories;
using BillingApplication.Domain.Entities;
using Dapper;
using FluentAssertions;
using Xunit;

namespace BillingApplication.Tests.Repositories
{
    [Collection("Database collection")]
    public class InvoiceRepositoryTests : IAsyncLifetime
    {
        private readonly DatabaseFixture _fixture;
        private readonly InvoiceRepository _sut;
        private readonly DapperContext _context;

        public InvoiceRepositoryTests(DatabaseFixture fixture)
        {
            _fixture = fixture;
            _context = _fixture.CreateContext();
            _sut = new InvoiceRepository(_context);
        }

        public async Task InitializeAsync()
        {
            await _fixture.ResetDatabaseAsync();
        }

        public Task DisposeAsync() => Task.CompletedTask;

        private async Task<(int customerId, int pmId, int productId)> SetupTestDataAsync()
        {
            using var connection = _context.CreateConnection();
            
            var customerId = await connection.QuerySingleAsync<int>(
                "INSERT INTO Clientes (Nombre, Activo) VALUES ('Test Customer', 1); SELECT CAST(SCOPE_IDENTITY() as int);");
                
            var pmId = await connection.QuerySingleAsync<int>(
                "INSERT INTO FormasPago (Nombre, Activo) VALUES ('Test PM', 1); SELECT CAST(SCOPE_IDENTITY() as int);");
                
            var productId = await connection.QuerySingleAsync<int>(
                "INSERT INTO Articulos (Codigo, Nombre, Descripcion, PrecioUnitario, Stock, Activo) VALUES ('T1', 'Prod', 'Desc', 10, 100, 1); SELECT CAST(SCOPE_IDENTITY() as int);");

            return (customerId, pmId, productId);
        }

        [Fact]
        public async Task GenerateNextInvoiceNumberAsync_ReturnsFormattedString()
        {
            var invoiceNumber = await _sut.GenerateNextInvoiceNumberAsync();
            
            invoiceNumber.Should().StartWith("FACT-");
            invoiceNumber.Length.Should().BeGreaterThan(4);
        }

        [Fact]
        public async Task CreateInvoiceTransactionAsync_SavesInvoiceAndDetails_AndDecreasesStock()
        {
            var (customerId, pmId, productId) = await SetupTestDataAsync();

            var invoice = new Invoice
            {
                ClienteId = customerId,
                FormaPagoId = pmId,
                Fecha = DateTime.Now,
                NumeroFactura = await _sut.GenerateNextInvoiceNumberAsync(),
                Subtotal = 20,
                Total = 20
            };

            var details = new List<InvoiceDetail>
            {
                new InvoiceDetail
                {
                    ProductoId = productId,
                    Cantidad = 2,
                    PrecioUnidad = 10,
                    Subtotal = 20
                }
            };

            var invoiceId = await _sut.CreateInvoiceTransactionAsync(invoice, details);

            invoiceId.Should().BeGreaterThan(0);

            var savedInvoice = await _sut.GetInvoiceWithDetailsAsync(invoiceId);
            
            savedInvoice.Should().NotBeNull();
            savedInvoice.ClienteId.Should().Be(customerId);
            savedInvoice.FormaPagoId.Should().Be(pmId);
            savedInvoice.InvoiceDetails.Should().HaveCount(1);
            savedInvoice.InvoiceDetails.First().ProductoId.Should().Be(productId);

            // Verify stock decreased
            using var connection = _context.CreateConnection();
            var stock = await connection.ExecuteScalarAsync<int>("SELECT Stock FROM Articulos WHERE Id = @Id", new { Id = productId });
            stock.Should().Be(98);
        }

        [Fact]
        public async Task GetAllAsync_ReturnsInvoices()
        {
            var (customerId, pmId, productId) = await SetupTestDataAsync();
            var invoice = new Invoice
            {
                ClienteId = customerId,
                FormaPagoId = pmId,
                Fecha = DateTime.Now,
                NumeroFactura = "INV-TEST",
                Subtotal = 0,
                Total = 0
            };
            
            await _sut.CreateInvoiceTransactionAsync(invoice, new List<InvoiceDetail>());

            var invoices = await _sut.GetAllAsync();

            invoices.Should().NotBeEmpty();
            invoices.First().NumeroFactura.Should().Be("INV-TEST");
        }
        
        [Fact]
        public async Task GetByIdAsync_ReturnsInvoice()
        {
            var (customerId, pmId, productId) = await SetupTestDataAsync();
            var invoice = new Invoice
            {
                ClienteId = customerId,
                FormaPagoId = pmId,
                Fecha = DateTime.Now,
                NumeroFactura = "INV-TEST2",
                Subtotal = 0,
                Total = 0
            };
            
            var id = await _sut.CreateInvoiceTransactionAsync(invoice, new List<InvoiceDetail>());

            var result = await _sut.GetByIdAsync(id);

            result.Should().NotBeNull();
            result.NumeroFactura.Should().Be("INV-TEST2");
        }

        [Fact]
        public async Task AddAsync_AddsInvoiceAndReturnsId()
        {
            var (customerId, pmId, _) = await SetupTestDataAsync();
            var invoice = new Invoice
            {
                ClienteId = customerId,
                FormaPagoId = pmId,
                Fecha = DateTime.Now,
                NumeroFactura = "INV-ADD",
                Subtotal = 10,
                Total = 10
            };

            var id = await _sut.AddAsync(invoice);
            id.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task AddInvoiceDetailAsync_AddsDetailAndReturnsId()
        {
            var (customerId, pmId, productId) = await SetupTestDataAsync();
            var invoice = new Invoice { ClienteId = customerId, FormaPagoId = pmId, Fecha = DateTime.Now, NumeroFactura = "INV-DTL", Subtotal = 10, Total = 10 };
            var invoiceId = await _sut.AddAsync(invoice);

            var detail = new InvoiceDetail
            {
                FacturaId = invoiceId,
                ProductoId = productId,
                Cantidad = 1,
                PrecioUnidad = 10,
                Subtotal = 10
            };

            var id = await _sut.AddInvoiceDetailAsync(detail);
            id.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task UpdateStockAsync_IncrementsAndDecrementsStock()
        {
            var (_, _, productId) = await SetupTestDataAsync();
            
            await _sut.UpdateStockAsync(productId, 10, "INCREMENT");
            var stockInc = await _context.CreateConnection().ExecuteScalarAsync<int>("SELECT Stock FROM Articulos WHERE Id = @Id", new { Id = productId });
            stockInc.Should().Be(110);
            
            await _sut.UpdateStockAsync(productId, 5, "DECREMENT");
            var stockDec = await _context.CreateConnection().ExecuteScalarAsync<int>("SELECT Stock FROM Articulos WHERE Id = @Id", new { Id = productId });
            stockDec.Should().Be(105);
        }

        [Fact]
        public async Task UpdateStockAsync_InvalidOperation_ThrowsArgumentException()
        {
            var (_, _, productId) = await SetupTestDataAsync();
            var action = () => _sut.UpdateStockAsync(productId, 10, "INVALID");
            await action.Should().ThrowAsync<ArgumentException>();
        }

        [Fact]
        public async Task UpdateAsync_UpdatesExistingInvoice()
        {
            var (customerId, pmId, _) = await SetupTestDataAsync();
            var invoice = new Invoice { ClienteId = customerId, FormaPagoId = pmId, Fecha = DateTime.Now, NumeroFactura = "INV-UPD1", Subtotal = 10, Total = 10 };
            var id = await _sut.AddAsync(invoice);

            invoice.Id = id;
            invoice.NumeroFactura = "INV-UPD2";
            invoice.Total = 20;

            await _sut.UpdateAsync(invoice);

            var updated = await _sut.GetByIdAsync(id);
            updated.NumeroFactura.Should().Be("INV-UPD2");
            updated.Total.Should().Be(20);
        }

        [Fact]
        public async Task DeleteAsync_DeletesInvoice()
        {
            var (customerId, pmId, _) = await SetupTestDataAsync();
            var invoice = new Invoice { ClienteId = customerId, FormaPagoId = pmId, Fecha = DateTime.Now, NumeroFactura = "INV-DEL", Subtotal = 10, Total = 10 };
            var id = await _sut.AddAsync(invoice);

            await _sut.DeleteAsync(id);

            var result = await _sut.GetByIdAsync(id);
            result.Should().BeNull();
        }

        [Fact]
        public async Task InvoiceNumberExistsAsync_ReturnsTrueIfExists()
        {
            var (customerId, pmId, _) = await SetupTestDataAsync();
            var invoice = new Invoice { ClienteId = customerId, FormaPagoId = pmId, Fecha = DateTime.Now, NumeroFactura = "INV-EXIST", Subtotal = 10, Total = 10 };
            await _sut.AddAsync(invoice);

            var exists = await _sut.InvoiceNumberExistsAsync("INV-EXIST");
            exists.Should().BeTrue();
            
            var notExists = await _sut.InvoiceNumberExistsAsync("INV-NOTEXIST");
            notExists.Should().BeFalse();
        }

        [Fact]
        public async Task GenerateNextInvoiceNumberAsync_WithExistingInvoices_ReturnsNextNumber()
        {
            var (customerId, pmId, _) = await SetupTestDataAsync();
            var prefijo = $"FACT-{DateTime.Now.Year}-";
            var invoice = new Invoice { ClienteId = customerId, FormaPagoId = pmId, Fecha = DateTime.Now, NumeroFactura = $"{prefijo}100", Subtotal = 10, Total = 10 };
            await _sut.AddAsync(invoice);

            var nextNum = await _sut.GenerateNextInvoiceNumberAsync();
            nextNum.Should().Be($"{prefijo}101");
        }

        [Fact]
        public async Task CreateInvoiceTransactionAsync_FailsAndRollsBack()
        {
            var (customerId, pmId, _) = await SetupTestDataAsync();
            var invoice = new Invoice { ClienteId = customerId, FormaPagoId = pmId, Fecha = DateTime.Now, NumeroFactura = "INV-FAIL", Subtotal = 10, Total = 10 };
            
            var details = new List<InvoiceDetail>
            {
                new InvoiceDetail { ProductoId = 9999, Cantidad = 1, PrecioUnidad = 10, Subtotal = 10 }
            };

            var action = () => _sut.CreateInvoiceTransactionAsync(invoice, details);
            await action.Should().ThrowAsync<Exception>();

            var exists = await _sut.InvoiceNumberExistsAsync("INV-FAIL");
            exists.Should().BeFalse();
        }
    }
}
