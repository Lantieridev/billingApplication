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
        private const string InvoiceCancelledMessage = "Creación de factura cancelada.";

        private readonly IServiceProvider _serviceProvider;
        private readonly TextReader _in;
        private readonly TextWriter _out;

        public BillingApp(IServiceProvider serviceProvider, TextReader? reader = null, TextWriter? writer = null)
        {
            _serviceProvider = serviceProvider;
            _in = reader ?? Console.In;
            _out = writer ?? Console.Out;
        }

        private int GetValidInt(string prompt, int minValue = int.MinValue)
        {
            while (true)
            {
                _out.Write(prompt);
                var input = _in.ReadLine();
                if (input == null)
                {
                    // End of stream (e.g. redirected/piped input ran out). Deliberately NOT
                    // caught here: it propagates up to RunAsync, which is the single place
                    // that handles EOF and prints "Saliendo..." exactly once (see RunAsync).
                    throw new EndOfStreamException("No se pudo leer la entrada (fin de flujo).");
                }
                if (!int.TryParse(input, out int result))
                {
                    _out.WriteLine("❌ Entrada no válida. Por favor, ingrese un número entero.");
                    continue;
                }
                if (result < minValue)
                {
                    _out.WriteLine($"❌ El valor debe ser mayor o igual a {minValue}.");
                    continue;
                }
                return result;
            }
        }

        private bool GetConfirmation(string prompt)
        {
            // Unlike GetValidInt, EOF here is treated as "No" rather than thrown: declining a
            // confirmation is always a safe default (nothing destructive happens), so there's no
            // need to unwind all the way out to RunAsync's EOF handler for this specific prompt.
            _out.Write(prompt);
            var input = _in.ReadLine()?.Trim().ToUpperInvariant();
            return input == "S" || input == "SI" || input == "SÍ";
        }

        private void Pause()
        {
            _out.WriteLine("\nPresione Enter para continuar...");
            _in.ReadLine();
        }

        public async Task RunAsync()
        {
            _out.WriteLine("=== Sistema de Facturación ===");

            while (true)
            {
                _out.WriteLine("\n--- Menú Principal ---");
                _out.WriteLine("1. Crear Factura");
                _out.WriteLine("2. Listar Facturas");
                _out.WriteLine("3. Ver Detalle de Factura");
                _out.WriteLine("4. Listar Productos");
                _out.WriteLine("5. Listar Clientes");
                _out.WriteLine("6. Salir");
                _out.Write("Seleccione una opción (1-6): ");

                var option = _in.ReadLine()?.Trim();
                if (option == null)
                {
                    _out.WriteLine("Saliendo...");
                    return;
                }

                try
                {
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
                            _out.WriteLine("❌ Opción no válida. Por favor, seleccione un número entre 1 y 6.");
                            break;
                    }
                }
                catch (EndOfStreamException)
                {
                    // Single, centralized EOF exit point: every GetValidInt call in every menu
                    // action funnels here instead of each action printing its own exit message
                    // and letting the loop come back around to read (and fail) the menu prompt.
                    _out.WriteLine("Saliendo...");
                    return;
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
                var customerId = await SelectCustomerIdAsync(customerRepo);
                if (customerId == null) return;

                var paymentMethodId = await SelectPaymentMethodIdAsync(paymentMethodRepo);
                if (paymentMethodId == null) return;

                var (details, total) = await BuildInvoiceDetailsAsync(productRepo);
                if (details.Count == 0)
                {
                    _out.WriteLine("No se agregaron productos a la factura.");
                    return;
                }

                await ConfirmAndPersistInvoiceAsync(invoiceService, customerId.Value, paymentMethodId.Value, details, total);
            }
            catch (Exception ex) when (ex is not EndOfStreamException)
            {
                _out.WriteLine($"❌ Error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    _out.WriteLine($"❌ Detalle técnico: {ex.InnerException.Message}");
                }
            }
        }

        private async Task<int?> SelectCustomerIdAsync(ICustomerRepository customerRepo)
        {
            var customers = await customerRepo.GetAllAsync();
            _out.WriteLine("\nClientes disponibles:");
            foreach (var customer in customers)
            {
                _out.WriteLine($"ID: {customer.Id} | Nombre: {customer.Nombre}");
            }

            while (true)
            {
                var customerId = GetValidInt("Ingrese el ID del cliente (0 para cancelar): ", minValue: 0);
                if (customerId == 0)
                {
                    _out.WriteLine(InvoiceCancelledMessage);
                    return null;
                }
                if (ExistsById(customers, customerId, c => c.Id)) return customerId;
                _out.WriteLine("❌ El ID de cliente ingresado no existe en la lista. Intente nuevamente.");
            }
        }

        private async Task<int?> SelectPaymentMethodIdAsync(IPaymentMethodRepository paymentMethodRepo)
        {
            var paymentMethods = await paymentMethodRepo.GetAllAsync();
            _out.WriteLine("\nFormas de pago disponibles:");
            foreach (var pm in paymentMethods)
            {
                _out.WriteLine($"ID: {pm.Id} | Nombre: {pm.Nombre}");
            }

            while (true)
            {
                var paymentMethodId = GetValidInt("Ingrese el ID de la forma de pago (0 para cancelar): ", minValue: 0);
                if (paymentMethodId == 0)
                {
                    _out.WriteLine(InvoiceCancelledMessage);
                    return null;
                }
                if (ExistsById(paymentMethods, paymentMethodId, pm => pm.Id)) return paymentMethodId;
                _out.WriteLine("❌ El ID de forma de pago ingresado no existe en la lista. Intente nuevamente.");
            }
        }

        private async Task<(List<InvoiceDetail> Details, decimal Total)> BuildInvoiceDetailsAsync(IProductRepository productRepo)
        {
            var products = await productRepo.GetAllAsync();
            _out.WriteLine("\nProductos disponibles:");
            foreach (var product in products)
            {
                _out.WriteLine($"ID: {product.Id} | Nombre: {product.Nombre} | Precio: ${product.PrecioUnitario} | Stock: {product.Stock}");
            }

            var details = new List<InvoiceDetail>();
            decimal runningTotal = 0m;
            while (true)
            {
                int productId = GetValidInt("\nIngrese el ID del producto (0 para finalizar la carga): ", minValue: 0);
                if (productId == 0) break;

                var product = await productRepo.GetByIdAsync(productId);
                if (product == null)
                {
                    _out.WriteLine("❌ Producto no encontrado. Verifique la lista de productos disponibles e ingrese un ID válido.");
                    continue;
                }

                int quantity = GetValidInt("Ingrese la cantidad: ", minValue: 1);

                var subtotal = quantity * product.PrecioUnitario;
                details.Add(new InvoiceDetail
                {
                    ProductoId = productId,
                    Cantidad = quantity,
                    PrecioUnidad = product.PrecioUnitario,
                    Subtotal = subtotal
                });

                runningTotal += subtotal;
                _out.WriteLine($"  Agregado: {product.Nombre} x {quantity} = ${subtotal}. Total acumulado: ${runningTotal}");
            }

            return (details, runningTotal);
        }

        private async Task ConfirmAndPersistInvoiceAsync(IInvoiceService invoiceService, int customerId, int paymentMethodId, List<InvoiceDetail> details, decimal total)
        {
            _out.WriteLine("\n--- Resumen de la Factura ---");
            _out.WriteLine($"Cliente ID: {customerId} | Forma de Pago ID: {paymentMethodId} | Ítems: {details.Count} | Total: ${total}");

            if (!GetConfirmation("¿Confirma la creación de la factura? (S/N): "))
            {
                _out.WriteLine(InvoiceCancelledMessage);
                return;
            }

            var invoice = new Invoice
            {
                ClienteId = customerId,
                FormaPagoId = paymentMethodId,
                Fecha = DateTime.Now
            };

            var createdInvoice = await invoiceService.CreateInvoiceAsync(invoice, details);

            _out.WriteLine($"\n✅ Factura creada exitosamente.");
            _out.WriteLine($"Número: {createdInvoice.NumeroFactura}");
            _out.WriteLine($"Total: ${createdInvoice.Total}");
        }

        private static bool ExistsById<T>(IEnumerable<T> items, int id, Func<T, int> idSelector)
        {
            foreach (var item in items)
            {
                if (idSelector(item) == id) return true;
            }
            return false;
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
                    _out.WriteLine($"ID: {invoice.Id} | Factura: {invoice.NumeroFactura} | Fecha: {invoice.Fecha:dd/MM/yyyy} | Total: ${invoice.Total}");
                }
            }
            catch (Exception ex)
            {
                _out.WriteLine($"❌ Error: {ex.Message}");
            }

            Pause();
        }

        private async Task ShowInvoiceDetailAsync()
        {
            _out.WriteLine("\n--- Detalle de Factura ---");
            _out.WriteLine("(Si no conoce el ID, consulte primero la opción 2 'Listar Facturas' en el menú principal)");

            // GetValidInt used to be called here with no surrounding try/catch of any kind, so
            // hitting EOF while entering the invoice ID crashed the whole app with an unhandled
            // exception. Now it's simply allowed to propagate — RunAsync's centralized catch
            // handles it the same way as every other menu action.
            int invoiceId = GetValidInt("Ingrese el ID de la factura: ", minValue: 1);

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
                        _out.WriteLine($"- Producto: {detail.Product.Nombre} | Cantidad: {detail.Cantidad} | Precio Unitario: ${detail.PrecioUnidad} | Subtotal: ${detail.Subtotal}");
                    }
                }
                else
                {
                    _out.WriteLine("❌ Factura no encontrada. Puede consultar los ID disponibles utilizando la opción 2 ('Listar Facturas') del menú principal.");
                }
            }
            catch (Exception ex)
            {
                _out.WriteLine($"❌ Error: {ex.Message}");
            }

            Pause();
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
                    _out.WriteLine($"ID: {product.Id} | Nombre: {product.Nombre} | Precio: ${product.PrecioUnitario} | Stock: {product.Stock}");
                }
            }
            catch (Exception ex)
            {
                _out.WriteLine($"❌ Error: {ex.Message}");
            }

            Pause();
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
                    _out.WriteLine($"ID: {customer.Id} | Nombre: {customer.Nombre} | Email: {customer.Email} | Teléfono: {customer.Telefono}");
                }
            }
            catch (Exception ex)
            {
                _out.WriteLine($"❌ Error: {ex.Message}");
            }

            Pause();
        }
    }
}
