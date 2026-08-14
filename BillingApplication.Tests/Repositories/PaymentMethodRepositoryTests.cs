using System.Threading.Tasks;
using BillingApplication.Data.Repositories;
using BillingApplication.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace BillingApplication.Tests.Repositories
{
    [Collection("Database collection")]
    public class PaymentMethodRepositoryTests : IAsyncLifetime
    {
        private readonly DatabaseFixture _fixture;
        private readonly PaymentMethodRepository _sut;

        public PaymentMethodRepositoryTests(DatabaseFixture fixture)
        {
            _fixture = fixture;
            _sut = new PaymentMethodRepository(_fixture.CreateContext());
        }

        public async Task InitializeAsync()
        {
            await _fixture.ResetDatabaseAsync();
        }

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task AddAsync_AddsPaymentMethodAndReturnsId()
        {
            var pm = new PaymentMethod
            {
                Nombre = "Efectivo",
                Activo = true
            };

            var id = await _sut.AddAsync(pm);

            id.Should().BeGreaterThan(0);
            
            var result = await _sut.GetByIdAsync(id);
            result.Should().NotBeNull();
            result.Nombre.Should().Be("Efectivo");
        }

        [Fact]
        public async Task UpdateAsync_UpdatesExistingPaymentMethod()
        {
            var pm = new PaymentMethod { Nombre = "PM1", Activo = true };
            var id = await _sut.AddAsync(pm);
            
            pm.Id = id;
            pm.Nombre = "PM1 Modificado";
            
            await _sut.UpdateAsync(pm);
            
            var result = await _sut.GetByIdAsync(id);
            result.Nombre.Should().Be("PM1 Modificado");
        }

        [Fact]
        public async Task DeleteAsync_SoftDeletesPaymentMethod()
        {
            var pm = new PaymentMethod { Nombre = "PM2", Activo = true };
            var id = await _sut.AddAsync(pm);

            await _sut.DeleteAsync(id);

            var result = await _sut.GetByIdAsync(id);
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetAllAsync_ReturnsOnlyActivePaymentMethods()
        {
            await _sut.AddAsync(new PaymentMethod { Nombre = "PM Activo", Activo = true });
            var id2 = await _sut.AddAsync(new PaymentMethod { Nombre = "PM Inactivo", Activo = true });
            
            await _sut.DeleteAsync(id2);

            var results = await _sut.GetAllAsync();
            
            results.Should().ContainSingle(p => p.Nombre == "PM Activo");
            results.Should().NotContain(p => p.Id == id2);
        }

        [Fact]
        public async Task GetByNameAsync_ReturnsMatchingPaymentMethod()
        {
            await _sut.AddAsync(new PaymentMethod { Nombre = "Tarjeta Visa", Activo = true });

            var result = await _sut.GetByNameAsync("Tarjeta Visa");
            
            result.Should().NotBeNull();
            result.Nombre.Should().Be("Tarjeta Visa");
        }
    }
}
