using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using BillingApplication.ConsoleUI;
using BillingApplication.Services.Interfaces;
using BillingApplication.Data.Interfaces;
using BillingApplication.Domain.Entities;
using Moq;
using Xunit;
using FluentAssertions;

namespace BillingApplication.Tests.ConsoleUI
{
    public class BillingAppTests
    {
        private readonly Mock<IInvoiceService> _invoiceServiceMock;
        private readonly Mock<IProductRepository> _productRepoMock;
        private readonly Mock<ICustomerRepository> _customerRepoMock;
        private readonly Mock<IPaymentMethodRepository> _paymentMethodRepoMock;
        private readonly IServiceProvider _serviceProvider;

        public BillingAppTests()
        {
            _invoiceServiceMock = new Mock<IInvoiceService>();
            _productRepoMock = new Mock<IProductRepository>();
            _customerRepoMock = new Mock<ICustomerRepository>();
            _paymentMethodRepoMock = new Mock<IPaymentMethodRepository>();

            var services = new ServiceCollection();
            services.AddScoped(_ => _invoiceServiceMock.Object);
            services.AddScoped(_ => _productRepoMock.Object);
            services.AddScoped(_ => _customerRepoMock.Object);
            services.AddScoped(_ => _paymentMethodRepoMock.Object);

            _serviceProvider = services.BuildServiceProvider();
        }

        private (BillingApp App, StringWriter Writer) CreateApp(string input)
        {
            var reader = new StringReader(input);
            var writer = new StringWriter();
            var app = new BillingApp(_serviceProvider, reader, writer);
            return (app, writer);
        }

        [Fact]
        public void Constructor_DefaultsToConsole_WhenReaderAndWriterAreNull()
        {
            var app = new BillingApp(_serviceProvider);
            app.Should().NotBeNull();
        }

        [Fact]
        public async Task RunAsync_EndOfInput_ExitsGracefully()
        {
            // _in.ReadLine() returning null (redirected input ran out) used to fall into the
            // "opción no válida" default branch forever instead of ever returning.
            var (app, writer) = CreateApp("");

            await app.RunAsync();

            writer.ToString().Should().Contain("Saliendo...");
        }

        [Fact]
        public async Task RunAsync_Option1_CreateInvoice_EndOfInputMidFlow_ShowsErrorThenExits()
        {
            // GetValidInt hitting EOF (no more lines to read the customer ID from) used to loop
            // forever re-printing the "entrada no válida" message.
            var (app, writer) = CreateApp("1\n");

            await app.RunAsync();

            var output = writer.ToString();
            output.Should().Contain("❌ Error: No se pudo leer la entrada (fin de flujo).");
            output.Should().Contain("Saliendo...");
        }

        [Fact]
        public async Task RunAsync_Option1_CreateInvoice_NonPositiveCustomerId_RetriesUntilValid()
        {
            _customerRepoMock.Setup(repo => repo.GetAllAsync()).ReturnsAsync(new List<Customer>());
            _paymentMethodRepoMock.Setup(repo => repo.GetAllAsync()).ReturnsAsync(new List<PaymentMethod>());
            _productRepoMock.Setup(repo => repo.GetAllAsync()).ReturnsAsync(new List<Product>());

            // "0" and "-1" are both rejected (customer IDs must be >= 1) before "1" is accepted.
            var (app, writer) = CreateApp("1\n0\n-1\n1\n1\n0\n6\n");

            await app.RunAsync();

            writer.ToString().Should().Contain("❌ Entrada no válida. Por favor, ingrese un número entero.");
        }

        [Fact]
        public async Task RunAsync_Option6_Exits()
        {
            var (app, writer) = CreateApp("6\n");
            
            await app.RunAsync();
            
            var output = writer.ToString();
            output.Should().Contain("Saliendo...");
        }

        [Fact]
        public async Task RunAsync_InvalidOption_ShowsMessageAndExits()
        {
            var (app, writer) = CreateApp("99\n6\n");
            
            await app.RunAsync();
            
            var output = writer.ToString();
            output.Should().Contain("Opción no válida.");
            output.Should().Contain("Saliendo...");
        }

        [Fact]
        public async Task RunAsync_Option4_ListsProducts()
        {
            var products = new List<Product>
            {
                new Product { Id = 1, Nombre = "Producto 1", PrecioUnitario = 10m, Stock = 5 }
            };
            _productRepoMock.Setup(repo => repo.GetAllAsync()).ReturnsAsync(products);

            var (app, writer) = CreateApp("4\n6\n");
            
            await app.RunAsync();
            
            var output = writer.ToString();
            output.Should().Contain("--- Listado de Productos ---");
            output.Should().Contain("#1: Producto 1 - $10 - Stock: 5");
        }

        [Fact]
        public async Task RunAsync_Option3_ShowsInvoiceDetail()
        {
            var invoice = new Invoice
            {
                Id = 1,
                NumeroFactura = "INV-001",
                Fecha = new DateTime(2023, 1, 1),
                Total = 100m,
                Customer = new Customer { Nombre = "Juan" },
                PaymentMethod = new PaymentMethod { Nombre = "Efectivo" },
                InvoiceDetails = new List<InvoiceDetail>
                {
                    new InvoiceDetail { Product = new Product { Nombre = "Prod1" }, Cantidad = 1, PrecioUnidad = 100m, Subtotal = 100m }
                }
            };

            _invoiceServiceMock.Setup(s => s.GetInvoiceByIdAsync(1)).ReturnsAsync(invoice);

            var (app, writer) = CreateApp("3\n1\n6\n"); // Menu -> Enter ID -> Exit
            
            await app.RunAsync();
            
            var output = writer.ToString();
            output.Should().Contain("--- Detalle de Factura ---");
            output.Should().Contain("Factura: INV-001");
            output.Should().Contain("Cliente: Juan");
            output.Should().Contain("Forma de Pago: Efectivo");
            output.Should().Contain("- Prod1: 1 x $100 = $100");
        }

        [Fact]
        public async Task RunAsync_Option1_CreatesInvoiceSuccessfully()
        {
            var customers = new List<Customer> { new Customer { Id = 1, Nombre = "Juan" } };
            _customerRepoMock.Setup(repo => repo.GetAllAsync()).ReturnsAsync(customers);

            var paymentMethods = new List<PaymentMethod> { new PaymentMethod { Id = 1, Nombre = "Efectivo" } };
            _paymentMethodRepoMock.Setup(repo => repo.GetAllAsync()).ReturnsAsync(paymentMethods);

            var products = new List<Product> { new Product { Id = 1, Nombre = "Prod1", PrecioUnitario = 10m, Stock = 5 } };
            _productRepoMock.Setup(repo => repo.GetAllAsync()).ReturnsAsync(products);
            _productRepoMock.Setup(repo => repo.GetByIdAsync(1)).ReturnsAsync(products[0]);

            var expectedInvoice = new Invoice { NumeroFactura = "INV-001", Total = 20m };
            _invoiceServiceMock.Setup(s => s.CreateInvoiceAsync(It.IsAny<Invoice>(), It.IsAny<List<InvoiceDetail>>()))
                               .ReturnsAsync(expectedInvoice);

            var (app, writer) = CreateApp("1\n1\n1\n1\n2\n0\n6\n"); 
            
            await app.RunAsync();
            
            var output = writer.ToString();
            output.Should().Contain("Factura creada exitosamente!");
            output.Should().Contain("Número: INV-001");
            output.Should().Contain("Total: $20");
        }

        [Fact]
        public async Task RunAsync_Option4_ListProducts_Exception_ShowsError()
        {
            _productRepoMock.Setup(repo => repo.GetAllAsync()).ThrowsAsync(new Exception("DB Error"));
            var (app, writer) = CreateApp("4\n6\n");
            await app.RunAsync();
            writer.ToString().Should().Contain("❌ Error: DB Error");
        }

        [Fact]
        public async Task RunAsync_Option5_ListCustomers()
        {
            var customers = new List<Customer> { new Customer { Id = 1, Nombre = "Juan", Email = "j@j.com", Telefono = "123" } };
            _customerRepoMock.Setup(repo => repo.GetAllAsync()).ReturnsAsync(customers);
            var (app, writer) = CreateApp("5\n6\n");
            await app.RunAsync();
            writer.ToString().Should().Contain("#1: Juan - j@j.com - 123");
        }

        [Fact]
        public async Task RunAsync_Option5_ListCustomers_Exception_ShowsError()
        {
            _customerRepoMock.Setup(repo => repo.GetAllAsync()).ThrowsAsync(new Exception("DB Error Cust"));
            var (app, writer) = CreateApp("5\n6\n");
            await app.RunAsync();
            writer.ToString().Should().Contain("❌ Error: DB Error Cust");
        }

        [Fact]
        public async Task RunAsync_Option2_ListInvoices()
        {
            var invoices = new List<Invoice> { new Invoice { Id = 1, NumeroFactura = "INV-1", Fecha = new DateTime(2023,1,1), Total = 50m } };
            _invoiceServiceMock.Setup(repo => repo.GetAllInvoicesAsync()).ReturnsAsync(invoices);
            var (app, writer) = CreateApp("2\n6\n");
            await app.RunAsync();
            writer.ToString().Should().Contain("#1: INV-1 - 01/01/2023 - $50");
        }

        [Fact]
        public async Task RunAsync_Option2_ListInvoices_Exception_ShowsError()
        {
            _invoiceServiceMock.Setup(repo => repo.GetAllInvoicesAsync()).ThrowsAsync(new Exception("DB Error Inv"));
            var (app, writer) = CreateApp("2\n6\n");
            await app.RunAsync();
            writer.ToString().Should().Contain("❌ Error: DB Error Inv");
        }

        [Fact]
        public async Task RunAsync_Option3_ShowInvoiceDetail_NotFound()
        {
            _invoiceServiceMock.Setup(s => s.GetInvoiceByIdAsync(1)).ReturnsAsync((Invoice)null!);
            var (app, writer) = CreateApp("3\n1\n6\n");
            await app.RunAsync();
            writer.ToString().Should().Contain("❌ Factura no encontrada.");
        }

        [Fact]
        public async Task RunAsync_Option3_ShowInvoiceDetail_Exception()
        {
            _invoiceServiceMock.Setup(s => s.GetInvoiceByIdAsync(1)).ThrowsAsync(new Exception("Detail Error"));
            var (app, writer) = CreateApp("3\n1\n6\n");
            await app.RunAsync();
            writer.ToString().Should().Contain("❌ Error: Detail Error");
        }

        [Fact]
        public async Task RunAsync_Option1_CreateInvoice_InvalidIntRetry()
        {
            var (app, writer) = CreateApp("1\ninvalid\n1\n1\n0\n6\n");
            
            _customerRepoMock.Setup(repo => repo.GetAllAsync()).ReturnsAsync(new List<Customer>());
            _paymentMethodRepoMock.Setup(repo => repo.GetAllAsync()).ReturnsAsync(new List<PaymentMethod>());
            _productRepoMock.Setup(repo => repo.GetAllAsync()).ReturnsAsync(new List<Product>());

            await app.RunAsync();
            writer.ToString().Should().Contain("❌ Entrada no válida. Por favor, ingrese un número entero.");
            writer.ToString().Should().Contain("No se agregaron productos a la factura.");
        }

        [Fact]
        public async Task RunAsync_Option1_CreateInvoice_ProductNotFound()
        {
            _customerRepoMock.Setup(repo => repo.GetAllAsync()).ReturnsAsync(new List<Customer>());
            _paymentMethodRepoMock.Setup(repo => repo.GetAllAsync()).ReturnsAsync(new List<PaymentMethod>());
            _productRepoMock.Setup(repo => repo.GetAllAsync()).ReturnsAsync(new List<Product>());
            _productRepoMock.Setup(repo => repo.GetByIdAsync(99)).ReturnsAsync((Product)null!);

            var (app, writer) = CreateApp("1\n1\n1\n99\n0\n6\n");
            await app.RunAsync();
            writer.ToString().Should().Contain("Producto no encontrado.");
        }

        [Fact]
        public async Task RunAsync_Option1_CreateInvoice_NoProductsAdded()
        {
            _customerRepoMock.Setup(repo => repo.GetAllAsync()).ReturnsAsync(new List<Customer>());
            _paymentMethodRepoMock.Setup(repo => repo.GetAllAsync()).ReturnsAsync(new List<PaymentMethod>());
            _productRepoMock.Setup(repo => repo.GetAllAsync()).ReturnsAsync(new List<Product>());

            var (app, writer) = CreateApp("1\n1\n1\n0\n6\n");
            await app.RunAsync();
            writer.ToString().Should().Contain("No se agregaron productos a la factura.");
        }

        [Fact]
        public async Task RunAsync_Option1_CreateInvoice_Exception()
        {
            _customerRepoMock.Setup(repo => repo.GetAllAsync()).ThrowsAsync(new Exception("Create Error", new Exception("Inner Create Error")));
            var (app, writer) = CreateApp("1\n6\n");
            await app.RunAsync();
            var output = writer.ToString();
            output.Should().Contain("❌ Error: Create Error");
            output.Should().Contain("❌ Inner Exception: Inner Create Error");
        }
    }
}
