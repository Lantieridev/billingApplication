using System;
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
                .AddScoped<IPaymentMethodRepository, PaymentMethodRepository>()  // ← AÑADIR ESTA LÍNEA
                .AddScoped<IProductService, ProductService>()
                .AddScoped<ICustomerService, CustomerService>()
                // Services
                .AddScoped<IInvoiceService, InvoiceService>()
                .BuildServiceProvider();

            var app = new BillingApp(services);
            await app.RunAsync();
        }
    }

    public class BillingApp
    {
        private readonly IServiceProvider _serviceProvider;

        public BillingApp(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task RunAsync()
        {
            Console.WriteLine("=== Sistema de Facturación ===");

            using var scope = _serviceProvider.CreateScope();
            var invoiceService = scope.ServiceProvider.GetRequiredService<IInvoiceService>();
            var productService = scope.ServiceProvider.GetRequiredService<IProductService>();
            var customerService = scope.ServiceProvider.GetRequiredService<ICustomerService>();
            var paymentMethodRepo = scope.ServiceProvider.GetRequiredService<IPaymentMethodRepository>();
            var productRepo = scope.ServiceProvider.GetRequiredService<IProductRepository>();

            var producto = await productRepo.GetByIdAsync(1);

            // Corrección: Usar nombres de propiedades correctos según la definición de Invoice e InvoiceDetail
            await invoiceService.CreateInvoiceAsync(
                new Invoice { ClienteId = 1, FormaPagoId = 1, Fecha = DateTime.Now },
                new List<InvoiceDetail>
                {
                    new()
                    {
                        ProductoId = 1,
                        Cantidad = 1,
                        PrecioUnidad = producto.PrecioUnitario,
                        Subtotal = 1 * producto.PrecioUnitario
                    }
                }
            );

            // Corrección: Usar nombres de propiedades correctos para Product
            await productService.CreateProductAsync(new Product
            {
                Codigo = "novo",
                Nombre = "Novo",
                Descripcion = "NNovo",
                PrecioUnitario = 10,
                Stock = 10
            });

            await customerService.CreateCustomerAsync(new Customer
            {
                Nombre = "NovoCliente",
                Direccion = "Aca",
                Telefono = "3524553422",
                Email = "novo@novo.novo"
            });

            // ... (resto del código sin cambios)
            productService.CreateProductAsync(new Product { Codigo = "novo", Nombre = "Novo", Descripcion = "NNovo", PrecioUnitario = 10, Stock = 10 });
            // Corrección de propiedades para Customer
            await customerService.CreateCustomerAsync(new Customer { Nombre = "NovoCliente", Direccion = "Aca", Telefono = "3524553422", Email = "novo@novo.novo" });

            // Corrección de propiedades para Invoice
            foreach (var invoice in await invoiceService.GetAllInvoicesAsync())
            {
                Console.WriteLine($"#{invoice.Id}: {invoice.NumeroFactura} - {invoice.Fecha:dd/MM/yyyy} - ${invoice.Total}");
            }

            // Corrección de propiedades para Product
            foreach (var product in await productService.GetAllProductsAsync())
            {
                Console.WriteLine($"#{product.Id}: Código: {product.Codigo} Nombre: {product.Nombre} Descripción: {product.Descripcion} PrecioUnitario: {product.PrecioUnitario} Stock: {product.Stock} ");
            }

            // Corrección de propiedades para Customer
            foreach (var customer in await customerService.GetAllCustomersAsync())
            {
                Console.WriteLine($"#{customer.Id}: Nombre: {customer.Nombre} Dirección: {customer.Direccion} Teléfono: {customer.Telefono} Email: {customer.Email} ");
            }
            /* while (true)
             {
                 Console.WriteLine("\nMenú Principal:");
                 Console.WriteLine("1. Crear Factura");
                 Console.WriteLine("2. Listar Facturas");
                 Console.WriteLine("3. Ver Detalle de Factura");
                 Console.WriteLine("4. Listar Productos");
                 Console.WriteLine("5. Listar Clientes");
                 Console.WriteLine("6. Salir");
                 Console.Write("Seleccione una opción: ");

                 var option = Console.ReadLine();

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
                         Console.WriteLine("Saliendo...");
                         return;
                     default:
                         Console.WriteLine("Opción no válida.");
                         break;
                 }
             }*/
        }

        private async Task CreateInvoiceAsync()
        {
            Console.WriteLine("\n--- Crear Nueva Factura ---");

            using var scope = _serviceProvider.CreateScope();
            var invoiceService = scope.ServiceProvider.GetRequiredService<IInvoiceService>();
            var productRepo = scope.ServiceProvider.GetRequiredService<IProductRepository>();      // ← CORREGIDO
            var customerRepo = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();    // ← CORREGIDO
            var paymentMethodRepo = scope.ServiceProvider.GetRequiredService<IPaymentMethodRepository>();  // ← CORREGIDO

            try
            {
                // Listar clientes
                var customers = await customerRepo.GetAllAsync();
                Console.WriteLine("\nClientes disponibles:");
                foreach (var customer in customers)
                {
                    Console.WriteLine($"{customer.Id}: {customer.Nombre}");
                }

                Console.Write("ID del Cliente: ");
                int customerId = int.Parse(Console.ReadLine());

                // Listar formas de pago
                var paymentMethods = await paymentMethodRepo.GetAllAsync();
                Console.WriteLine("\nFormas de pago disponibles:");
                foreach (var pm in paymentMethods)
                {
                    Console.WriteLine($"{pm.Id}: {pm.Nombre}");
                }

                Console.Write("ID de Forma de Pago: ");
                int paymentMethodId = int.Parse(Console.ReadLine());

                // Listar productos
                var products = await productRepo.GetAllAsync();
                Console.WriteLine("\nProductos disponibles:");
                foreach (var product in products)
                {
                    Console.WriteLine($"{product.Id}: {product.Nombre} - ${product.PrecioUnitario} - Stock: {product.Stock}");
                }

                var details = new List<InvoiceDetail>();
                while (true)
                {
                    Console.Write("\nID del Producto (0 para terminar): ");
                    int productId = int.Parse(Console.ReadLine());
                    if (productId == 0) break;

                    var product = await productRepo.GetByIdAsync(productId);
                    if (product == null)
                    {
                        Console.WriteLine("Producto no encontrado.");
                        continue;
                    }

                    Console.Write("Cantidad: ");
                    int quantity = int.Parse(Console.ReadLine());

                    if (product.Stock < quantity)
                    {
                        Console.WriteLine($"Stock insuficiente. Disponible: {product.Stock}");
                        continue;
                    }

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
                    Console.WriteLine("No se agregaron productos a la factura.");
                    return;
                }

                var invoice = new Invoice
                {
                    ClienteId = customerId,
                    FormaPagoId = paymentMethodId,
                    Fecha = DateTime.Now
                };

                var createdInvoice = await invoiceService.CreateInvoiceAsync(invoice, details);

                Console.WriteLine($"\n✅ Factura creada exitosamente!");
                Console.WriteLine($"Número: {createdInvoice.NumeroFactura}");
                Console.WriteLine($"Total: ${createdInvoice.Total}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"❌ Inner Exception: {ex.InnerException.Message}");
                }
            }
        }

        private async Task ListInvoicesAsync()
        {
            Console.WriteLine("\n--- Listado de Facturas ---");

            using var scope = _serviceProvider.CreateScope();
            var invoiceService = scope.ServiceProvider.GetRequiredService<IInvoiceService>();

            try
            {
                var invoices = await invoiceService.GetAllInvoicesAsync();
                foreach (var invoice in invoices)
                {
                    Console.WriteLine($"#{invoice.Id}: {invoice.NumeroFactura} - {invoice.Fecha:dd/MM/yyyy} - ${invoice.Total}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
            }
        }

        private async Task ShowInvoiceDetailAsync()
        {
            Console.WriteLine("\n--- Detalle de Factura ---");
            Console.Write("Ingrese el ID de la factura: ");

            if (int.TryParse(Console.ReadLine(), out int invoiceId))
            {
                using var scope = _serviceProvider.CreateScope();
                var invoiceService = scope.ServiceProvider.GetRequiredService<IInvoiceService>();

                try
                {
                    var invoice = await invoiceService.GetInvoiceByIdAsync(invoiceId);
                    if (invoice != null)
                    {
                        Console.WriteLine($"\n📄 Factura: {invoice.NumeroFactura}");
                        Console.WriteLine($"📅 Fecha: {invoice.Fecha:dd/MM/yyyy}");
                        Console.WriteLine($"👤 Cliente: {invoice.Customer.Nombre}");
                        Console.WriteLine($"💳 Forma de Pago: {invoice.PaymentMethod.Nombre}");
                        Console.WriteLine($"💰 Total: ${invoice.Total}");

                        Console.WriteLine("\n🛒 Detalles:");
                        foreach (var detail in invoice.InvoiceDetails)
                        {
                            Console.WriteLine($"- {detail.Product.Nombre}: {detail.Cantidad} x ${detail.PrecioUnidad} = ${detail.Subtotal}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("❌ Factura no encontrada.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("❌ ID no válido.");
            }
        }

        private async Task ListProductsAsync()
        {
            Console.WriteLine("\n--- Listado de Productos ---");

            using var scope = _serviceProvider.CreateScope();
            var productRepo = scope.ServiceProvider.GetRequiredService<IProductRepository>();

            try
            {
                var products = await productRepo.GetAllAsync();
                foreach (var product in products)
                {
                    Console.WriteLine($"#{product.Id}: {product.Nombre} - ${product.PrecioUnitario} - Stock: {product.Stock}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
            }
        }

        private async Task ListCustomersAsync()
        {
            Console.WriteLine("\n--- Listado de Clientes ---");

            using var scope = _serviceProvider.CreateScope();
            var customerRepo = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();

            try
            {
                var customers = await customerRepo.GetAllAsync();
                foreach (var customer in customers)
                {
                    Console.WriteLine($"#{customer.Id}: {customer.Nombre} - {customer.Email} - {customer.Telefono}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
            }
        }
    }
}