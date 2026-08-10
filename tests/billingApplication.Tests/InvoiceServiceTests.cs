using BillingApplication.Data.Interfaces;
using BillingApplication.Domain.Entities;
using BillingApplication.Services;
using Moq;

namespace billingApplication.Tests;

public class InvoiceServiceTests
{
    [Fact]
    public async Task CreateInvoiceAsync_ShouldGenerateNumberCalculateTotalsAndUpdateStock()
    {
        var invoiceRepositoryMock = new Mock<IInvoiceRepository>();
        var productRepositoryMock = new Mock<IProductRepository>();

        var details = new List<InvoiceDetail>
        {
            new() { ProductoId = 1, Cantidad = 2, PrecioUnidad = 10m },
            new() { ProductoId = 2, Cantidad = 1, PrecioUnidad = 20m }
        };

        var invoice = new Invoice { ClienteId = 1, FormaPagoId = 1 };
        var persistedInvoice = new Invoice
        {
            Id = 15,
            NumeroFactura = "FACT-2026-15",
            Subtotal = 40m,
            Total = 40m
        };

        productRepositoryMock.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(new Product { Id = 1, Nombre = "Producto A", Stock = 10 });
        productRepositoryMock.Setup(r => r.GetByIdAsync(2))
            .ReturnsAsync(new Product { Id = 2, Nombre = "Producto B", Stock = 10 });

        invoiceRepositoryMock.Setup(r => r.GenerateNextInvoiceNumberAsync())
            .ReturnsAsync("FACT-2026-15");
        invoiceRepositoryMock.Setup(r => r.AddAsync(It.IsAny<Invoice>()))
            .ReturnsAsync(15);
        invoiceRepositoryMock.Setup(r => r.GetInvoiceWithDetailsAsync(15))
            .ReturnsAsync(persistedInvoice);

        var sut = new InvoiceService(invoiceRepositoryMock.Object, productRepositoryMock.Object);

        var result = await sut.CreateInvoiceAsync(invoice, details);

        Assert.Equal("FACT-2026-15", invoice.NumeroFactura);
        Assert.Equal(40m, invoice.Subtotal);
        Assert.Equal(40m, invoice.Total);
        Assert.Equal(20m, details[0].Subtotal);
        Assert.Equal(20m, details[1].Subtotal);
        Assert.Equal(15, details[0].FacturaId);
        Assert.Equal(15, details[1].FacturaId);
        Assert.Equal(15, result.Id);

        invoiceRepositoryMock.Verify(r => r.AddInvoiceDetailAsync(It.IsAny<InvoiceDetail>()), Times.Exactly(2));
        invoiceRepositoryMock.Verify(r => r.UpdateStockAsync(1, 2, "DECREMENT"), Times.Once);
        invoiceRepositoryMock.Verify(r => r.UpdateStockAsync(2, 1, "DECREMENT"), Times.Once);
    }

    [Fact]
    public async Task CreateInvoiceAsync_WhenStockIsInsufficient_ShouldThrowAndNotPersistInvoice()
    {
        var invoiceRepositoryMock = new Mock<IInvoiceRepository>();
        var productRepositoryMock = new Mock<IProductRepository>();
        var invoice = new Invoice { ClienteId = 1, FormaPagoId = 1 };
        var details = new List<InvoiceDetail>
        {
            new() { ProductoId = 3, Cantidad = 5, PrecioUnidad = 15m }
        };

        productRepositoryMock.Setup(r => r.GetByIdAsync(3))
            .ReturnsAsync(new Product { Id = 3, Nombre = "Producto C", Stock = 2 });

        var sut = new InvoiceService(invoiceRepositoryMock.Object, productRepositoryMock.Object);

        var exception = await Assert.ThrowsAsync<Exception>(() => sut.CreateInvoiceAsync(invoice, details));

        Assert.Contains("Stock insuficiente", exception.Message);
        invoiceRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Invoice>()), Times.Never);
        invoiceRepositoryMock.Verify(r => r.AddInvoiceDetailAsync(It.IsAny<InvoiceDetail>()), Times.Never);
        invoiceRepositoryMock.Verify(r => r.UpdateStockAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GenerateNextInvoiceNumberAsync_ShouldReturnRepositoryValue()
    {
        var invoiceRepositoryMock = new Mock<IInvoiceRepository>();
        var productRepositoryMock = new Mock<IProductRepository>();

        invoiceRepositoryMock.Setup(r => r.GenerateNextInvoiceNumberAsync())
            .ReturnsAsync("FACT-2026-99");

        var sut = new InvoiceService(invoiceRepositoryMock.Object, productRepositoryMock.Object);
        var result = await sut.GenerateNextInvoiceNumberAsync();

        Assert.Equal("FACT-2026-99", result);
    }
}
