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

        private void SetupOneCustomerOnePaymentMethod()
        {
            _customerRepoMock.Setup(repo => repo.GetAllAsync())
                .ReturnsAsync(new List<Customer> { new Customer { Id = 1, Nombre = "Juan" } });
            _paymentMethodRepoMock.Setup(repo => repo.GetAllAsync())
                .ReturnsAsync(new List<PaymentMethod> { new PaymentMethod { Id = 1, Nombre = "Efectivo" } });
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
        public async Task RunAsync_TrimsWhitespaceAroundMenuOption()
        {
            // Speech-to-text / assistive input often injects surrounding whitespace; the menu
            // used to reject " 4 " as an invalid option instead of recognizing it as "4".
            _productRepoMock.Setup(repo => repo.GetAllAsync()).ReturnsAsync(new List<Product>());

            var (app, writer) = CreateApp(" 4 \n\n6\n");

            await app.RunAsync();

            var output = writer.ToString();
            output.Should().Contain("--- Listado de Productos ---");
            output.Should().NotContain("Opción no válida");
        }

        [Fact]
        public async Task RunAsync_Option1_CreateInvoice_EndOfInputMidFlow_ExitsGracefully()
        {
            // GetValidInt hitting EOF (no more lines to read the customer ID from) used to print
            // a raw "fin de flujo" error before exiting; it now exits the same way every other
            // EOF path does, with no technical error text.
            var (app, writer) = CreateApp("1\n");

            await app.RunAsync();

            var output = writer.ToString();
            output.Should().Contain("Saliendo...");
            output.Should().NotContain("fin de flujo");
        }

        [Fact]
        public async Task RunAsync_Option1_CreateInvoice_NegativeCustomerId_RetriesWithRangeSpecificMessage()
        {
            // A negative/too-low value is a different failure than a non-numeric value; the
            // message must say so instead of reusing the "ingrese un número entero" text.
            var (app, writer) = CreateApp("1\n-1\n0\n6\n");

            await app.RunAsync();

            var output = writer.ToString();
            output.Should().Contain("❌ El valor debe ser mayor o igual a 0.");
            output.Should().NotContain("❌ Entrada no válida. Por favor, ingrese un número entero.");
        }

        [Fact]
        public async Task RunAsync_Option1_CreateInvoice_NonNumericCustomerId_RetriesWithParseMessage()
        {
            SetupOneCustomerOnePaymentMethod();
            _productRepoMock.Setup(repo => repo.GetAllAsync()).ReturnsAsync(new List<Product>());

            var (app, writer) = CreateApp("1\ninvalid\n1\n1\n0\n6\n");

            await app.RunAsync();

            var output = writer.ToString();
            output.Should().Contain("❌ Entrada no válida. Por favor, ingrese un número entero.");
            output.Should().Contain("No se agregaron productos a la factura.");
        }

        [Fact]
        public async Task RunAsync_Option1_CreateInvoice_ZeroCustomerId_CancelsCreation()
        {
            var (app, writer) = CreateApp("1\n0\n6\n");

            await app.RunAsync();

            var output = writer.ToString();
            output.Should().Contain("Creación de factura cancelada.");
            _invoiceServiceMock.Verify(s => s.CreateInvoiceAsync(It.IsAny<Invoice>(), It.IsAny<List<InvoiceDetail>>()), Times.Never);
        }

        [Fact]
        public async Task RunAsync_Option1_CreateInvoice_ZeroPaymentMethodId_CancelsCreation()
        {
            _customerRepoMock.Setup(repo => repo.GetAllAsync())
                .ReturnsAsync(new List<Customer> { new Customer { Id = 1, Nombre = "Juan" } });

            var (app, writer) = CreateApp("1\n1\n0\n6\n");

            await app.RunAsync();

            var output = writer.ToString();
            output.Should().Contain("Creación de factura cancelada.");
            _invoiceServiceMock.Verify(s => s.CreateInvoiceAsync(It.IsAny<Invoice>(), It.IsAny<List<InvoiceDetail>>()), Times.Never);
        }

        [Fact]
        public async Task RunAsync_Option1_CreateInvoice_UnknownCustomerId_RetriesUntilExistingIdEntered()
        {
            // Previously any integer >= 1 was accepted here even if it didn't exist in the
            // loaded customer list; the mismatch only surfaced later, deep inside the service
            // call, after the user had already picked products.
            _customerRepoMock.Setup(repo => repo.GetAllAsync())
                .ReturnsAsync(new List<Customer> { new Customer { Id = 5, Nombre = "Ana" } });
            _paymentMethodRepoMock.Setup(repo => repo.GetAllAsync()).ReturnsAsync(new List<PaymentMethod>());

            var (app, writer) = CreateApp("1\n99\n5\n0\n6\n");

            await app.RunAsync();

            var output = writer.ToString();
            output.Should().Contain("❌ El ID de cliente ingresado no existe en la lista. Intente nuevamente.");
            output.Should().Contain("Creación de factura cancelada.");
        }

        [Fact]
        public async Task RunAsync_Option1_CreateInvoice_UnknownPaymentMethodId_RetriesUntilExistingIdEntered()
        {
            _customerRepoMock.Setup(repo => repo.GetAllAsync())
                .ReturnsAsync(new List<Customer> { new Customer { Id = 1, Nombre = "Juan" } });
            _paymentMethodRepoMock.Setup(repo => repo.GetAllAsync())
                .ReturnsAsync(new List<PaymentMethod> { new PaymentMethod { Id = 7, Nombre = "Tarjeta" } });

            var (app, writer) = CreateApp("1\n1\n99\n7\n0\n6\n");

            await app.RunAsync();

            var output = writer.ToString();
            output.Should().Contain("❌ El ID de forma de pago ingresado no existe en la lista. Intente nuevamente.");
            // "7" is accepted (it exists), so the flow proceeds into the product loop; "0" ends
            // it immediately with no items added, which is a different message than a cancel.
            output.Should().Contain("No se agregaron productos a la factura.");
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
        public async Task RunAsync_InvalidOption_ShowsMessageWithValidRangeAndExits()
        {
            var (app, writer) = CreateApp("99\n6\n");

            await app.RunAsync();

            var output = writer.ToString();
            output.Should().Contain("❌ Opción no válida. Por favor, seleccione un número entre 1 y 6.");
            output.Should().Contain("Saliendo...");
        }

        [Fact]
        public async Task RunAsync_Option4_ListsProducts_WithLabeledFieldsAndPause()
        {
            var products = new List<Product>
            {
                new Product { Id = 1, Nombre = "Producto 1", PrecioUnitario = 10m, Stock = 5 }
            };
            _productRepoMock.Setup(repo => repo.GetAllAsync()).ReturnsAsync(products);

            var (app, writer) = CreateApp("4\n\n6\n");

            await app.RunAsync();

            var output = writer.ToString();
            output.Should().Contain("--- Listado de Productos ---");
            output.Should().Contain("ID: 1 | Nombre: Producto 1 | Precio: $10 | Stock: 5");
            output.Should().Contain("Presione Enter para continuar...");
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

            var (app, writer) = CreateApp("3\n1\n\n6\n"); // Menu -> Enter ID -> pause -> Exit

            await app.RunAsync();

            var output = writer.ToString();
            output.Should().Contain("--- Detalle de Factura ---");
            output.Should().Contain("Factura: INV-001");
            output.Should().Contain("Cliente: Juan");
            output.Should().Contain("Forma de Pago: Efectivo");
            output.Should().Contain("- Producto: Prod1 | Cantidad: 1 | Precio Unitario: $100 | Subtotal: $100");
        }

        [Fact]
        public async Task RunAsync_Option3_ShowInvoiceDetail_EndOfInputAtIdPrompt_ExitsGracefully()
        {
            // GetValidInt used to be called here with no surrounding try/catch at all, so EOF
            // while entering the invoice ID crashed the whole app with an unhandled exception.
            var (app, writer) = CreateApp("3\n");

            var act = async () => await app.RunAsync();

            await act.Should().NotThrowAsync();
            writer.ToString().Should().Contain("Saliendo...");
        }

        [Fact]
        public async Task RunAsync_Option1_CreatesInvoiceSuccessfully_AfterConfirmation()
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

            var (app, writer) = CreateApp("1\n1\n1\n1\n2\n0\nS\n6\n");

            await app.RunAsync();

            var output = writer.ToString();
            output.Should().Contain("Agregado: Prod1 x 2 = $20. Total acumulado: $20");
            output.Should().Contain("--- Resumen de la Factura ---");
            output.Should().Contain("Cliente ID: 1 | Forma de Pago ID: 1 | Ítems: 1 | Total: $20");
            output.Should().Contain("Factura creada exitosamente.");
            output.Should().Contain("Número: INV-001");
            output.Should().Contain("Total: $20");
            _invoiceServiceMock.Verify(s => s.CreateInvoiceAsync(It.IsAny<Invoice>(), It.IsAny<List<InvoiceDetail>>()), Times.Once);
        }

        [Fact]
        public async Task RunAsync_Option1_CreatesInvoiceSuccessfully_WithMultipleDistinctProducts()
        {
            // Every other success-path test adds exactly one product before ending the loop with
            // "0" — that alone can't prove the running total accumulates across iterations, or
            // that a second item actually gets appended to `details` rather than overwriting the
            // first. This drives the loop around twice with two different products.
            SetupOneCustomerOnePaymentMethod();
            var products = new List<Product>
            {
                new Product { Id = 1, Nombre = "Prod1", PrecioUnitario = 10m, Stock = 5 },
                new Product { Id = 2, Nombre = "Prod2", PrecioUnitario = 15m, Stock = 3 }
            };
            _productRepoMock.Setup(repo => repo.GetAllAsync()).ReturnsAsync(products);
            _productRepoMock.Setup(repo => repo.GetByIdAsync(1)).ReturnsAsync(products[0]);
            _productRepoMock.Setup(repo => repo.GetByIdAsync(2)).ReturnsAsync(products[1]);

            List<InvoiceDetail>? capturedDetails = null;
            _invoiceServiceMock.Setup(s => s.CreateInvoiceAsync(It.IsAny<Invoice>(), It.IsAny<List<InvoiceDetail>>()))
                               .Callback<Invoice, List<InvoiceDetail>>((_, details) => capturedDetails = details)
                               .ReturnsAsync(new Invoice { NumeroFactura = "INV-002", Total = 55m });

            // Product 1 x2 ($20), then Product 2 x1 ($15), then 0 to finish, then confirm.
            var (app, writer) = CreateApp("1\n1\n1\n1\n2\n2\n1\n0\nS\n6\n");

            await app.RunAsync();

            var output = writer.ToString();
            output.Should().Contain("Agregado: Prod1 x 2 = $20. Total acumulado: $20");
            output.Should().Contain("Agregado: Prod2 x 1 = $15. Total acumulado: $35");
            output.Should().Contain("Cliente ID: 1 | Forma de Pago ID: 1 | Ítems: 2 | Total: $35");

            capturedDetails.Should().NotBeNull();
            capturedDetails!.Should().HaveCount(2);
            capturedDetails.Sum(d => d.Subtotal).Should().Be(35m);
        }

        [Fact]
        public async Task RunAsync_Option1_CreateInvoice_ZeroQuantity_RejectedAndRetried()
        {
            SetupOneCustomerOnePaymentMethod();
            var products = new List<Product> { new Product { Id = 1, Nombre = "Prod1", PrecioUnitario = 10m, Stock = 5 } };
            _productRepoMock.Setup(repo => repo.GetAllAsync()).ReturnsAsync(products);
            _productRepoMock.Setup(repo => repo.GetByIdAsync(1)).ReturnsAsync(products[0]);

            // Quantity "0" must be rejected by the minValue:1 guard and re-prompted, not silently
            // accepted as a zero-item line.
            var (app, writer) = CreateApp("1\n1\n1\n1\n0\n1\n0\n6\n");

            await app.RunAsync();

            var output = writer.ToString();
            output.Should().Contain("❌ El valor debe ser mayor o igual a 1.");
            output.Should().Contain("Agregado: Prod1 x 1 = $10. Total acumulado: $10");
        }

        [Fact]
        public async Task RunAsync_Option1_CreateInvoice_ConfirmationAcceptsLowercaseSi()
        {
            SetupOneCustomerOnePaymentMethod();
            var products = new List<Product> { new Product { Id = 1, Nombre = "Prod1", PrecioUnitario = 10m, Stock = 5 } };
            _productRepoMock.Setup(repo => repo.GetAllAsync()).ReturnsAsync(products);
            _productRepoMock.Setup(repo => repo.GetByIdAsync(1)).ReturnsAsync(products[0]);
            _invoiceServiceMock.Setup(s => s.CreateInvoiceAsync(It.IsAny<Invoice>(), It.IsAny<List<InvoiceDetail>>()))
                               .ReturnsAsync(new Invoice { NumeroFactura = "INV-002", Total = 10m });

            var (app, writer) = CreateApp("1\n1\n1\n1\n1\n0\nsi\n6\n");

            await app.RunAsync();

            writer.ToString().Should().Contain("Factura creada exitosamente.");
            _invoiceServiceMock.Verify(s => s.CreateInvoiceAsync(It.IsAny<Invoice>(), It.IsAny<List<InvoiceDetail>>()), Times.Once);
        }

        [Fact]
        public async Task RunAsync_Option1_CreateInvoice_DecliningConfirmation_DoesNotCreateInvoice()
        {
            SetupOneCustomerOnePaymentMethod();
            var products = new List<Product> { new Product { Id = 1, Nombre = "Prod1", PrecioUnitario = 10m, Stock = 5 } };
            _productRepoMock.Setup(repo => repo.GetAllAsync()).ReturnsAsync(products);
            _productRepoMock.Setup(repo => repo.GetByIdAsync(1)).ReturnsAsync(products[0]);

            var (app, writer) = CreateApp("1\n1\n1\n1\n1\n0\nN\n6\n");

            await app.RunAsync();

            var output = writer.ToString();
            output.Should().Contain("Creación de factura cancelada.");
            output.Should().NotContain("Factura creada exitosamente.");
            _invoiceServiceMock.Verify(s => s.CreateInvoiceAsync(It.IsAny<Invoice>(), It.IsAny<List<InvoiceDetail>>()), Times.Never);
        }

        [Fact]
        public async Task RunAsync_Option1_CreateInvoice_EndOfInputAtConfirmation_TreatedAsDeclined()
        {
            // Hitting EOF right at the (S/N) prompt must not crash — it should be treated the
            // same as an explicit "N" rather than throwing on a null confirmation string.
            SetupOneCustomerOnePaymentMethod();
            var products = new List<Product> { new Product { Id = 1, Nombre = "Prod1", PrecioUnitario = 10m, Stock = 5 } };
            _productRepoMock.Setup(repo => repo.GetAllAsync()).ReturnsAsync(products);
            _productRepoMock.Setup(repo => repo.GetByIdAsync(1)).ReturnsAsync(products[0]);

            var (app, writer) = CreateApp("1\n1\n1\n1\n1\n0\n");

            var act = async () => await app.RunAsync();

            await act.Should().NotThrowAsync();
            var output = writer.ToString();
            output.Should().Contain("Creación de factura cancelada.");
            _invoiceServiceMock.Verify(s => s.CreateInvoiceAsync(It.IsAny<Invoice>(), It.IsAny<List<InvoiceDetail>>()), Times.Never);
        }

        [Fact]
        public async Task RunAsync_Option4_ListProducts_Exception_ShowsError()
        {
            _productRepoMock.Setup(repo => repo.GetAllAsync()).ThrowsAsync(new Exception("DB Error"));
            var (app, writer) = CreateApp("4\n\n6\n");
            await app.RunAsync();
            writer.ToString().Should().Contain("❌ Error: DB Error");
        }

        [Fact]
        public async Task RunAsync_Option5_ListCustomers_WithLabeledFields()
        {
            var customers = new List<Customer> { new Customer { Id = 1, Nombre = "Juan", Email = "j@j.com", Telefono = "123" } };
            _customerRepoMock.Setup(repo => repo.GetAllAsync()).ReturnsAsync(customers);
            var (app, writer) = CreateApp("5\n\n6\n");
            await app.RunAsync();
            writer.ToString().Should().Contain("ID: 1 | Nombre: Juan | Email: j@j.com | Teléfono: 123");
        }

        [Fact]
        public async Task RunAsync_Option5_ListCustomers_Exception_ShowsError()
        {
            _customerRepoMock.Setup(repo => repo.GetAllAsync()).ThrowsAsync(new Exception("DB Error Cust"));
            var (app, writer) = CreateApp("5\n\n6\n");
            await app.RunAsync();
            writer.ToString().Should().Contain("❌ Error: DB Error Cust");
        }

        [Fact]
        public async Task RunAsync_Option2_ListInvoices_WithLabeledFields()
        {
            var invoices = new List<Invoice> { new Invoice { Id = 1, NumeroFactura = "INV-1", Fecha = new DateTime(2023, 1, 1), Total = 50m } };
            _invoiceServiceMock.Setup(repo => repo.GetAllInvoicesAsync()).ReturnsAsync(invoices);
            var (app, writer) = CreateApp("2\n\n6\n");
            await app.RunAsync();
            writer.ToString().Should().Contain("ID: 1 | Factura: INV-1 | Fecha: 01/01/2023 | Total: $50");
        }

        [Fact]
        public async Task RunAsync_Option2_ListInvoices_Exception_ShowsError()
        {
            _invoiceServiceMock.Setup(repo => repo.GetAllInvoicesAsync()).ThrowsAsync(new Exception("DB Error Inv"));
            var (app, writer) = CreateApp("2\n\n6\n");
            await app.RunAsync();
            writer.ToString().Should().Contain("❌ Error: DB Error Inv");
        }

        [Fact]
        public async Task RunAsync_Option3_ShowInvoiceDetail_NotFound_SuggestsListingInvoices()
        {
            _invoiceServiceMock.Setup(s => s.GetInvoiceByIdAsync(1)).ReturnsAsync((Invoice)null!);
            var (app, writer) = CreateApp("3\n1\n\n6\n");
            await app.RunAsync();
            writer.ToString().Should().Contain("❌ Factura no encontrada. Puede consultar los ID disponibles utilizando la opción 2 ('Listar Facturas') del menú principal.");
        }

        [Fact]
        public async Task RunAsync_Option3_ShowInvoiceDetail_Exception()
        {
            _invoiceServiceMock.Setup(s => s.GetInvoiceByIdAsync(1)).ThrowsAsync(new Exception("Detail Error"));
            var (app, writer) = CreateApp("3\n1\n\n6\n");
            await app.RunAsync();
            writer.ToString().Should().Contain("❌ Error: Detail Error");
        }

        [Fact]
        public async Task RunAsync_Option1_CreateInvoice_ProductNotFound_ShowsActionableMessage()
        {
            SetupOneCustomerOnePaymentMethod();
            _productRepoMock.Setup(repo => repo.GetAllAsync()).ReturnsAsync(new List<Product>());
            _productRepoMock.Setup(repo => repo.GetByIdAsync(99)).ReturnsAsync((Product)null!);

            var (app, writer) = CreateApp("1\n1\n1\n99\n0\n6\n");
            await app.RunAsync();
            writer.ToString().Should().Contain("❌ Producto no encontrado. Verifique la lista de productos disponibles e ingrese un ID válido.");
        }

        [Fact]
        public async Task RunAsync_Option1_CreateInvoice_NoProductsAdded()
        {
            SetupOneCustomerOnePaymentMethod();
            _productRepoMock.Setup(repo => repo.GetAllAsync()).ReturnsAsync(new List<Product>());

            var (app, writer) = CreateApp("1\n1\n1\n0\n6\n");
            await app.RunAsync();
            writer.ToString().Should().Contain("No se agregaron productos a la factura.");
        }

        [Fact]
        public async Task RunAsync_Option1_CreateInvoice_Exception_ShowsTranslatedInnerDetail()
        {
            _customerRepoMock.Setup(repo => repo.GetAllAsync()).ThrowsAsync(new Exception("Create Error", new Exception("Inner Create Error")));
            var (app, writer) = CreateApp("1\n6\n");
            await app.RunAsync();
            var output = writer.ToString();
            output.Should().Contain("❌ Error: Create Error");
            output.Should().Contain("❌ Detalle técnico: Inner Create Error");
        }
    }
}
