using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BillingApplication.Domain.Entities;
using BillingApplication.Services.Interfaces;
using BillingApplication.Data.Interfaces;

namespace BillingApplication.Services
{
    public class InvoiceService : IInvoiceService
    {
        private readonly IInvoiceRepository _invoiceRepository;
        private readonly IProductRepository _productRepository;  // ← INTERFAZ ESPECÍFICA
        private readonly ICustomerRepository _customerRepository;
        private readonly IPaymentMethodRepository _paymentMethodRepository;

        public InvoiceService(
            IInvoiceRepository invoiceRepository,
            IProductRepository productRepository,
            ICustomerRepository customerRepository,
            IPaymentMethodRepository paymentMethodRepository)
        {
            _invoiceRepository = invoiceRepository;
            _productRepository = productRepository;
            _customerRepository = customerRepository;
            _paymentMethodRepository = paymentMethodRepository;
        }

        public async Task<Invoice> CreateInvoiceAsync(Invoice invoice, List<InvoiceDetail> details)
        {
            if (details == null || details.Count == 0)
            {
                throw new InvalidOperationException("La factura debe tener al menos un detalle.");
            }

            var customer = await _customerRepository.GetByIdAsync(invoice.ClienteId);
            if (customer == null || !customer.Activo)
            {
                throw new InvalidOperationException($"Cliente con ID {invoice.ClienteId} no encontrado o inactivo.");
            }

            var paymentMethod = await _paymentMethodRepository.GetByIdAsync(invoice.FormaPagoId);
            if (paymentMethod == null || !paymentMethod.Activo)
            {
                throw new InvalidOperationException($"Forma de pago con ID {invoice.FormaPagoId} no encontrada o inactiva.");
            }

            // Validar stock para cada producto
            foreach (var detail in details)
            {
                if (detail.Cantidad <= 0)
                {
                    throw new InvalidOperationException("La cantidad del producto debe ser mayor a cero.");
                }

                var product = await _productRepository.GetByIdAsync(detail.ProductoId);
                if (product == null)
                {
                    throw new InvalidOperationException($"Producto con ID {detail.ProductoId} no encontrado.");
                }
                if (product.Stock < detail.Cantidad)
                {
                    throw new InvalidOperationException($"Stock insuficiente. Disponible: {product.Stock}");
                }

                // El precio siempre debe salir del producto persistido, no del payload del
                // llamador -- hoy el único llamador (la consola) ya lo hace bien, pero la
                // regla de negocio debe vivir en el service, no depender de que cada llamador
                // se acuerde de copiarla correctamente.
                detail.PrecioUnidad = product.PrecioUnitario;
            }

            try
            {
                // Generar número de factura
                invoice.NumeroFactura = await _invoiceRepository.GenerateNextInvoiceNumberAsync();

                // Calcular totales
                CalculateInvoiceTotals(invoice, details);

                // Crear factura + detalles + descuento de stock en una única transacción atómica:
                // si algo falla a mitad de camino, se hace rollback completo (no queda stock
                // descontado sin factura registrada, ni factura sin sus detalles).
                invoice.Id = await _invoiceRepository.CreateInvoiceTransactionAsync(invoice, details);

                return await _invoiceRepository.GetInvoiceWithDetailsAsync(invoice.Id);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error creating invoice: {ex.Message}", ex);
            }
        }

        private void CalculateInvoiceTotals(Invoice invoice, List<InvoiceDetail> details)
        {
            invoice.Subtotal = 0;
            foreach (var detail in details)
            {
                detail.Subtotal = detail.Cantidad * detail.PrecioUnidad;
                invoice.Subtotal += detail.Subtotal;
            }
            invoice.Total = invoice.Subtotal;
        }

        public async Task<Invoice> GetInvoiceByIdAsync(int id)
        {
            return await _invoiceRepository.GetInvoiceWithDetailsAsync(id);
        }

        public async Task<IEnumerable<Invoice>> GetAllInvoicesAsync()
        {
            return await _invoiceRepository.GetAllAsync();
        }

        public async Task<string> GenerateNextInvoiceNumberAsync()
        {
            return await _invoiceRepository.GenerateNextInvoiceNumberAsync();
        }
    }
}