using System.Linq;
using System.Threading.Tasks;
using BillingApplication.Data.Repositories;
using BillingApplication.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace BillingApplication.Tests.Repositories
{
    [Collection("Database collection")]
    public class CustomerRepositoryTests : IAsyncLifetime
    {
        private readonly DatabaseFixture _fixture;
        private readonly CustomerRepository _sut;

        public CustomerRepositoryTests(DatabaseFixture fixture)
        {
            _fixture = fixture;
            _sut = new CustomerRepository(_fixture.CreateContext());
        }

        public async Task InitializeAsync()
        {
            await _fixture.ResetDatabaseAsync();
        }

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task AddAsync_AddsCustomerAndReturnsId()
        {
            var customer = new Customer
            {
                Nombre = "Juan Perez",
                Direccion = "Av Siempre Viva 123",
                Telefono = "555-1234",
                Email = "juan@example.com",
                Activo = true
            };

            var id = await _sut.AddAsync(customer);

            id.Should().BeGreaterThan(0);
            
            var result = await _sut.GetByIdAsync(id);
            result.Should().NotBeNull();
            result.Nombre.Should().Be("Juan Perez");
        }

        [Fact]
        public async Task UpdateAsync_UpdatesExistingCustomer()
        {
            var customer = new Customer
            {
                Nombre = "Maria Lopez",
                Activo = true
            };
            var id = await _sut.AddAsync(customer);
            
            customer.Id = id;
            customer.Nombre = "Maria Garcia";
            
            await _sut.UpdateAsync(customer);
            
            var result = await _sut.GetByIdAsync(id);
            result.Nombre.Should().Be("Maria Garcia");
        }

        [Fact]
        public async Task DeleteAsync_SoftDeletesCustomer()
        {
            var customer = new Customer { Nombre = "Test Delete", Activo = true };
            var id = await _sut.AddAsync(customer);

            await _sut.DeleteAsync(id);

            var result = await _sut.GetByIdAsync(id);
            result.Should().BeNull("because soft deleted customers are filtered out by Activo = 1");
        }

        [Fact]
        public async Task GetAllAsync_ReturnsOnlyActiveCustomers()
        {
            await _sut.AddAsync(new Customer { Nombre = "Activo", Activo = true });
            var id2 = await _sut.AddAsync(new Customer { Nombre = "A Borrar", Activo = true });
            
            await _sut.DeleteAsync(id2);

            var results = await _sut.GetAllAsync();
            
            results.Should().ContainSingle(c => c.Nombre == "Activo");
            results.Should().NotContain(c => c.Id == id2);
        }

        [Fact]
        public async Task SearchAsync_FindsMatchingCustomers()
        {
            await _sut.AddAsync(new Customer { Nombre = "Juan Perez", Email = "juan@example.com", Activo = true });
            await _sut.AddAsync(new Customer { Nombre = "Ana Lopez", Email = "ana@example.com", Activo = true });

            var results = await _sut.SearchAsync("perez");
            
            results.Should().ContainSingle(c => c.Nombre == "Juan Perez");
        }

        [Fact]
        public async Task GetByEmailAsync_ReturnsMatchingCustomer()
        {
            await _sut.AddAsync(new Customer { Nombre = "Juan Perez", Email = "juan@example.com", Activo = true });

            var result = await _sut.GetByEmailAsync("juan@example.com");
            
            result.Should().NotBeNull();
            result.Nombre.Should().Be("Juan Perez");
        }
    }
}
