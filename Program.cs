using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using BillingApplication.Data;
using BillingApplication.Data.Interfaces;
using BillingApplication.Data.Repositories;
using BillingApplication.Services.Interfaces;
using BillingApplication.Services;
using BillingApplication.Domain.Entities;

namespace BillingApplication.ConsoleUI
{
    class Program
    { 
        static async Task Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            var services = new ServiceCollection()
                .AddSingleton<IConfiguration>(configuration)
                .AddSingleton<DapperContext>()
                // Repositories
                .AddScoped<IInvoiceRepository, InvoiceRepository>()
                .AddScoped<IProductRepository, ProductRepository>()
                .AddScoped<IRepository<Product>>(sp => (IRepository<Product>)sp.GetRequiredService<IProductRepository>())
                .AddScoped<ICustomerRepository, CustomerRepository>()
                .AddScoped<IPaymentMethodRepository, PaymentMethodRepository>()
                // Services
                .AddScoped<IInvoiceService, InvoiceService>()
                .BuildServiceProvider();

            var app = new BillingApp(services, Console.In, Console.Out);
            await app.RunAsync();
        }
    }

    public class BillingApp
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly TextReader _in;
        private readonly TextWriter _out;

        public BillingApp(IServiceProvider serviceProvider, TextReader? reader = null, TextWriter? writer = null)
        {
            _serviceProvider = serviceProvider;
            _in = reader ?? Console.In;
            _out = writer ?? Console.Out;
        }

        private int GetValidInt(string prompt)
        {
            while (true)
            {
                _out.Write(prompt);
                var input = _in.ReadLine();
                if (int.TryParse(input, out int result))
                {
                    return result;
                }
                _out.WriteLine("❌ Entrada no válida. Por favor, ingrese un número entero.");
            }
        }

        public async Task RunAsync()
        {
            _out.WriteLine("=== Sistema de Facturación ===");

            while (true)
            {
                _out.WriteLine("\nMenú Principal:");
                _out.WriteLine("1. Crear Factura");
                _out.WriteLine("2. Listar Facturas");
                _out.WriteLine("3. Ver Detalle de Factura");
                _out.WriteLine("4. Listar Productos");
                _out.WriteLine("5. Listar Clientes");
                _out.WriteLine("6. Salir");
                _out.Write("Seleccione una opción: ");

                var option = _in.ReadLine();

                switch (option)
                {
                    case "1":
                        await CreateInvoiceAsync();
                        break;
                    case "2":
                        await ListInvoicesAsync();
                        break;
                    case "3":
                        await ShowInvoiceDetailAsync();
                        break;
                    case "4":
                        await ListProductsAsync();
                        break;
                    case "5":
                        await ListCustomersAsync();
                        break;
                    case "6":
                        _out.WriteLine("Saliendo...");
                        return;
                    default:
                        _out.WriteLine("Opción no válida.");
                        break;
                }
            }
        }

        private async Task CreateInvoiceAsync()
        {
            _out.WriteLine("\n--- Crear Nueva Factura ---");

            using var scope = _serviceProvider.CreateScope();
            var invoiceService = scope.ServiceProvider.GetRequiredService<IInvoiceService>();
            var productRepo = scope.ServiceProvider.GetRequiredService<IProductRepository>();
            var customerRepo = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();
            var paymentMethodRepo = scope.ServiceProvider.GetRequiredService<IPaymentMethodRepository>();

            try
            {
                // Listar clientes
                var customers = await customerRepo.GetAllAsync();
                _out.WriteLine("\nClientes disponibles:");
                foreach (var customer in customers)
                {
                    _out.WriteLine($"{customer.Id}: {customer.Nombre}");
                }

                int customerId = GetValidInt("ID del Cliente: ");

                // Listar formas de pago
                var paymentMethods = await paymentMethodRepo.GetAllAsync();
                _out.WriteLine("\nFormas de pago disponibles:");
                foreach (var pm in paymentMethods)
                {
                    _out.WriteLine($"{pm.Id}: {pm.Nombre}");
                }

                int paymentMethodId = GetValidInt("ID de Forma de Pago: ");

                // Listar productos
                var products = await productRepo.GetAllAsync();
                _out.WriteLine("\nProductos disponibles:");
                foreach (var product in products)
                {
                    _out.WriteLine($"{product.Id}: {product.Nombre} - ${product.PrecioUnitario} - Stock: {product.Stock}");
                }

                var details = new List<InvoiceDetail>();
                while (true)
                {
                    int productId = GetValidInt("\nID del Producto (0 para terminar): ");
                    if (productId == 0) break;

                    var product = await productRepo.GetByIdAsync(productId);
                    if (product == null)
                    {
                        _out.WriteLine("Producto no encontrado.");
                        continue;
                    }

                    int quantity = GetValidInt("Cantidad: ");

                    details.Add(new InvoiceDetail
                    {
                        ProductoId = productId,
                        Cantidad = quantity,
                        PrecioUnidad = product.PrecioUnitario,
                        Subtotal = quantity * product.PrecioUnitario
                    });
                }

                if (details.Count == 0)
                {
                    _out.WriteLine("No se agregaron productos a la factura.");
                    return;
                }

                var invoice = new Invoice
                {
                    ClienteId = customerId,
                    FormaPagoId = paymentMethodId,
                    Fecha = DateTime.Now
                };

                var createdInvoice = await invoiceService.CreateInvoiceAsync(invoice, details);

                _out.WriteLine($"\n✅ Factura creada exitosamente!");
                _out.WriteLine($"Número: {createdInvoice.NumeroFactura}");
                _out.WriteLine($"Total: ${createdInvoice.Total}");
            }
            catch (Exception ex)
            {
                _out.WriteLine($"❌ Error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    _out.WriteLine($"❌ Inner Exception: {ex.InnerException.Message}");
                }
            }
        }

        private async Task ListInvoicesAsync()
        {
            _out.WriteLine("\n--- Listado de Facturas ---");

            using var scope = _serviceProvider.CreateScope();
            var invoiceService = scope.ServiceProvider.GetRequiredService<IInvoiceService>();

            try
            {
                var invoices = await invoiceService.GetAllInvoicesAsync();
                foreach (var invoice in invoices)
                {
                    _out.WriteLine($"#{invoice.Id}: {invoice.NumeroFactura} - {invoice.Fecha:dd/MM/yyyy} - ${invoice.Total}");
                }
            }
            catch (Exception ex)
            {
                _out.WriteLine($"❌ Error: {ex.Message}");
            }
        }

        private async Task ShowInvoiceDetailAsync()
        {
            _out.WriteLine("\n--- Detalle de Factura ---");
            int invoiceId = GetValidInt("Ingrese el ID de la factura: ");

            using var scope = _serviceProvider.CreateScope();
            var invoiceService = scope.ServiceProvider.GetRequiredService<IInvoiceService>();

            try
            {
                var invoice = await invoiceService.GetInvoiceByIdAsync(invoiceId);
                if (invoice != null)
                {
                    _out.WriteLine($"\n📄 Factura: {invoice.NumeroFactura}");
                    _out.WriteLine($"📅 Fecha: {invoice.Fecha:dd/MM/yyyy}");
                    _out.WriteLine($"👤 Cliente: {invoice.Customer.Nombre}");
                    _out.WriteLine($"💳 Forma de Pago: {invoice.PaymentMethod.Nombre}");
                    _out.WriteLine($"💰 Total: ${invoice.Total}");

                    _out.WriteLine("\n🛒 Detalles:");
                    foreach (var detail in invoice.InvoiceDetails)
                    {
                        _out.WriteLine($"- {detail.Product.Nombre}: {detail.Cantidad} x ${detail.PrecioUnidad} = ${detail.Subtotal}");
                    }
                }
                else
                {
                    _out.WriteLine("❌ Factura no encontrada.");
                }
            }
            catch (Exception ex)
            {
                _out.WriteLine($"❌ Error: {ex.Message}");
            }
        }

        private async Task ListProductsAsync()
        {
            _out.WriteLine("\n--- Listado de Productos ---");

            using var scope = _serviceProvider.CreateScope();
            var productRepo = scope.ServiceProvider.GetRequiredService<IProductRepository>();

            try
            {
                var products = await productRepo.GetAllAsync();
                foreach (var product in products)
                {
                    _out.WriteLine($"#{product.Id}: {product.Nombre} - ${product.PrecioUnitario} - Stock: {product.Stock}");
                }
            }
            catch (Exception ex)
            {
                _out.WriteLine($"❌ Error: {ex.Message}");
            }
        }

        private async Task ListCustomersAsync()
        {
            _out.WriteLine("\n--- Listado de Clientes ---");

            using var scope = _serviceProvider.CreateScope();
            var customerRepo = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();

            try
            {
                var customers = await customerRepo.GetAllAsync();
                foreach (var customer in customers)
                {
                    _out.WriteLine($"#{customer.Id}: {customer.Nombre} - {customer.Email} - {customer.Telefono}");
                }
            }
            catch (Exception ex)
            {
                _out.WriteLine($"❌ Error: {ex.Message}");
            }
        }
    }
}

