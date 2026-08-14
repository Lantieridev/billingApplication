using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BillingApplication.Domain.Entities;
using BillingApplication.Services;
using BillingApplication.Data.Interfaces;
using Moq;
using Xunit;
using FluentAssertions;

namespace BillingApplication.Tests.Services
{
    public class InvoiceServiceTests
    {
        private readonly Mock<IInvoiceRepository> _invoiceRepoMock;
        private readonly Mock<IProductRepository> _productRepoMock;
        private readonly Mock<ICustomerRepository> _customerRepoMock;
        private readonly Mock<IPaymentMethodRepository> _paymentMethodRepoMock;
        private readonly InvoiceService _sut; // System Under Test

        private const int ValidClienteId = 1;
        private const int ValidFormaPagoId = 1;

        public InvoiceServiceTests()
        {
            _invoiceRepoMock = new Mock<IInvoiceRepository>();
            _productRepoMock = new Mock<IProductRepository>();
            _customerRepoMock = new Mock<ICustomerRepository>();
            _paymentMethodRepoMock = new Mock<IPaymentMethodRepository>();

            // Every test gets a valid, active Customer/PaymentMethod by default -- the tests that
            // specifically exercise that validation override these setups themselves.
            _customerRepoMock.Setup(repo => repo.GetByIdAsync(ValidClienteId))
                .ReturnsAsync(new Customer { Id = ValidClienteId, Activo = true });
            _paymentMethodRepoMock.Setup(repo => repo.GetByIdAsync(ValidFormaPagoId))
                .ReturnsAsync(new PaymentMethod { Id = ValidFormaPagoId, Activo = true });

            _sut = new InvoiceService(_invoiceRepoMock.Object, _productRepoMock.Object, _customerRepoMock.Object, _paymentMethodRepoMock.Object);
        }

        private static Invoice ValidInvoice() => new() { ClienteId = ValidClienteId, FormaPagoId = ValidFormaPagoId };

        [Fact]
        public async Task CreateInvoiceAsync_WithNullDetails_ThrowsInvalidOperationException()
        {
            var invoice = ValidInvoice();

            Func<Task> act = async () => await _sut.CreateInvoiceAsync(invoice, null!);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("La factura debe tener al menos un detalle.");
        }

        [Fact]
        public async Task CreateInvoiceAsync_WithEmptyDetails_ThrowsInvalidOperationException()
        {
            var invoice = ValidInvoice();
            var details = new List<InvoiceDetail>();

            Func<Task> act = async () => await _sut.CreateInvoiceAsync(invoice, details);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("La factura debe tener al menos un detalle.");
        }

        [Fact]
        public async Task CreateInvoiceAsync_WithNonExistentCustomer_ThrowsInvalidOperationException()
        {
            var invoice = new Invoice { ClienteId = 99, FormaPagoId = ValidFormaPagoId };
            var details = new List<InvoiceDetail> { new() { ProductoId = 1, Cantidad = 1 } };
            _customerRepoMock.Setup(repo => repo.GetByIdAsync(99)).ReturnsAsync((Customer)null!);

            Func<Task> act = async () => await _sut.CreateInvoiceAsync(invoice, details);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Cliente con ID 99 no encontrado o inactivo.");
        }

        [Fact]
        public async Task CreateInvoiceAsync_WithInactiveCustomer_ThrowsInvalidOperationException()
        {
            var invoice = new Invoice { ClienteId = 2, FormaPagoId = ValidFormaPagoId };
            var details = new List<InvoiceDetail> { new() { ProductoId = 1, Cantidad = 1 } };
            _customerRepoMock.Setup(repo => repo.GetByIdAsync(2)).ReturnsAsync(new Customer { Id = 2, Activo = false });

            Func<Task> act = async () => await _sut.CreateInvoiceAsync(invoice, details);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Cliente con ID 2 no encontrado o inactivo.");
        }

        [Fact]
        public async Task CreateInvoiceAsync_WithNonExistentPaymentMethod_ThrowsInvalidOperationException()
        {
            var invoice = new Invoice { ClienteId = ValidClienteId, FormaPagoId = 99 };
            var details = new List<InvoiceDetail> { new() { ProductoId = 1, Cantidad = 1 } };
            _paymentMethodRepoMock.Setup(repo => repo.GetByIdAsync(99)).ReturnsAsync((PaymentMethod)null!);

            Func<Task> act = async () => await _sut.CreateInvoiceAsync(invoice, details);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Forma de pago con ID 99 no encontrada o inactiva.");
        }

        [Fact]
        public async Task CreateInvoiceAsync_WithInactivePaymentMethod_ThrowsInvalidOperationException()
        {
            var invoice = new Invoice { ClienteId = ValidClienteId, FormaPagoId = 2 };
            var details = new List<InvoiceDetail> { new() { ProductoId = 1, Cantidad = 1 } };
            _paymentMethodRepoMock.Setup(repo => repo.GetByIdAsync(2)).ReturnsAsync(new PaymentMethod { Id = 2, Activo = false });

            Func<Task> act = async () => await _sut.CreateInvoiceAsync(invoice, details);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Forma de pago con ID 2 no encontrada o inactiva.");
        }

        [Fact]
        public async Task CreateInvoiceAsync_WithNegativeQuantity_ThrowsInvalidOperationException()
        {
            var invoice = ValidInvoice();
            var details = new List<InvoiceDetail>
            {
                new InvoiceDetail { ProductoId = 1, Cantidad = 0 }
            };

            Func<Task> act = async () => await _sut.CreateInvoiceAsync(invoice, details);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("La cantidad del producto debe ser mayor a cero.");
        }

        [Fact]
        public async Task CreateInvoiceAsync_WithNonExistentProduct_ThrowsInvalidOperationException()
        {
            var invoice = ValidInvoice();
            var details = new List<InvoiceDetail>
            {
                new InvoiceDetail { ProductoId = 99, Cantidad = 5 }
            };

            _productRepoMock.Setup(repo => repo.GetByIdAsync(99)).ReturnsAsync((Product)null!);

            Func<Task> act = async () => await _sut.CreateInvoiceAsync(invoice, details);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Producto con ID 99 no encontrado.");
        }

        [Fact]
        public async Task CreateInvoiceAsync_WithInsufficientStock_ThrowsInvalidOperationException()
        {
            var invoice = ValidInvoice();
            var details = new List<InvoiceDetail>
            {
                new InvoiceDetail { ProductoId = 1, Cantidad = 10 }
            };

            var product = new Product { Id = 1, Stock = 5 };
            _productRepoMock.Setup(repo => repo.GetByIdAsync(1)).ReturnsAsync(product);

            Func<Task> act = async () => await _sut.CreateInvoiceAsync(invoice, details);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Stock insuficiente. Disponible: 5");
        }

        [Fact]
        public async Task CreateInvoiceAsync_ValidInvoice_CalculatesTotalsAndSaves()
        {
            // Arrange
            var invoice = new Invoice { Id = 0, ClienteId = ValidClienteId, FormaPagoId = ValidFormaPagoId };
            var details = new List<InvoiceDetail>
            {
                new InvoiceDetail { ProductoId = 1, Cantidad = 2, PrecioUnidad = 50m },
                new InvoiceDetail { ProductoId = 2, Cantidad = 1, PrecioUnidad = 100m }
            };

            var product1 = new Product { Id = 1, Stock = 10, PrecioUnitario = 50m };
            var product2 = new Product { Id = 2, Stock = 5, PrecioUnitario = 100m };

            _productRepoMock.Setup(repo => repo.GetByIdAsync(1)).ReturnsAsync(product1);
            _productRepoMock.Setup(repo => repo.GetByIdAsync(2)).ReturnsAsync(product2);

            _invoiceRepoMock.Setup(repo => repo.GenerateNextInvoiceNumberAsync()).ReturnsAsync("INV-001");
            _invoiceRepoMock.Setup(repo => repo.CreateInvoiceTransactionAsync(It.IsAny<Invoice>(), It.IsAny<List<InvoiceDetail>>())).ReturnsAsync(1);

            var savedInvoice = new Invoice { Id = 1, NumeroFactura = "INV-001", Total = 200m };
            _invoiceRepoMock.Setup(repo => repo.GetInvoiceWithDetailsAsync(1)).ReturnsAsync(savedInvoice);

            // Act
            var result = await _sut.CreateInvoiceAsync(invoice, details);

            // Assert
            result.Should().BeEquivalentTo(savedInvoice);

            // Verify totals were calculated correctly before saving
            _invoiceRepoMock.Verify(repo => repo.CreateInvoiceTransactionAsync(
                It.Is<Invoice>(i => i.Subtotal == 200m && i.Total == 200m && i.NumeroFactura == "INV-001"),
                It.Is<List<InvoiceDetail>>(d =>
                    d[0].Subtotal == 100m &&
                    d[1].Subtotal == 100m)
            ), Times.Once);
        }

        [Fact]
        public async Task CreateInvoiceAsync_PayloadPriceDiffersFromProduct_UsesProductPrice()
        {
            // The caller-provided PrecioUnidad (999m, clearly wrong) must never be trusted --
            // the service owns pricing and always sources it from the persisted Product.
            var invoice = ValidInvoice();
            var details = new List<InvoiceDetail>
            {
                new InvoiceDetail { ProductoId = 1, Cantidad = 2, PrecioUnidad = 999m }
            };

            var product = new Product { Id = 1, Stock = 10, PrecioUnitario = 25m };
            _productRepoMock.Setup(repo => repo.GetByIdAsync(1)).ReturnsAsync(product);

            _invoiceRepoMock.Setup(repo => repo.GenerateNextInvoiceNumberAsync()).ReturnsAsync("INV-002");
            _invoiceRepoMock.Setup(repo => repo.CreateInvoiceTransactionAsync(It.IsAny<Invoice>(), It.IsAny<List<InvoiceDetail>>())).ReturnsAsync(1);
            _invoiceRepoMock.Setup(repo => repo.GetInvoiceWithDetailsAsync(1)).ReturnsAsync(new Invoice { Id = 1 });

            await _sut.CreateInvoiceAsync(invoice, details);

            _invoiceRepoMock.Verify(repo => repo.CreateInvoiceTransactionAsync(
                It.IsAny<Invoice>(),
                It.Is<List<InvoiceDetail>>(d => d[0].PrecioUnidad == 25m && d[0].Subtotal == 50m)
            ), Times.Once);
        }

        [Fact]
        public async Task CreateInvoiceAsync_RepositoryThrowsException_WrapsInException()
        {
            // Arrange
            var invoice = ValidInvoice();
            var details = new List<InvoiceDetail>
            {
                new InvoiceDetail { ProductoId = 1, Cantidad = 1, PrecioUnidad = 10m }
            };

            var product = new Product { Id = 1, Stock = 10 };
            _productRepoMock.Setup(repo => repo.GetByIdAsync(1)).ReturnsAsync(product);

            _invoiceRepoMock.Setup(repo => repo.GenerateNextInvoiceNumberAsync()).ThrowsAsync(new Exception("Database error"));

            // Act
            Func<Task> act = async () => await _sut.CreateInvoiceAsync(invoice, details);

            // Assert
            await act.Should().ThrowAsync<Exception>()
                .WithMessage("Error al crear la factura: Database error");
        }

        [Fact]
        public async Task GetInvoiceByIdAsync_ReturnsInvoiceFromRepository()
        {
            var expectedInvoice = new Invoice { Id = 1 };
            _invoiceRepoMock.Setup(repo => repo.GetInvoiceWithDetailsAsync(1)).ReturnsAsync(expectedInvoice);

            var result = await _sut.GetInvoiceByIdAsync(1);

            result.Should().BeEquivalentTo(expectedInvoice);
            _invoiceRepoMock.Verify(repo => repo.GetInvoiceWithDetailsAsync(1), Times.Once);
        }

        [Fact]
        public async Task GetAllInvoicesAsync_ReturnsAllInvoicesFromRepository()
        {
            var expectedInvoices = new List<Invoice> { new Invoice { Id = 1 }, new Invoice { Id = 2 } };
            _invoiceRepoMock.Setup(repo => repo.GetAllAsync()).ReturnsAsync(expectedInvoices);

            var result = await _sut.GetAllInvoicesAsync();

            result.Should().BeEquivalentTo(expectedInvoices);
            _invoiceRepoMock.Verify(repo => repo.GetAllAsync(), Times.Once);
        }

        [Fact]
        public async Task GenerateNextInvoiceNumberAsync_ReturnsStringFromRepository()
        {
            _invoiceRepoMock.Setup(repo => repo.GenerateNextInvoiceNumberAsync()).ReturnsAsync("INV-123");

            var result = await _sut.GenerateNextInvoiceNumberAsync();

            result.Should().Be("INV-123");
            _invoiceRepoMock.Verify(repo => repo.GenerateNextInvoiceNumberAsync(), Times.Once);
        }
    }
}
