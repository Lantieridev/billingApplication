using System.Threading.Tasks;
using BillingApplication.Data.Repositories;
using BillingApplication.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace BillingApplication.Tests.Repositories
{
    [Collection("Database collection")]
    public class ProductRepositoryTests : IAsyncLifetime
    {
        private readonly DatabaseFixture _fixture;
        private readonly ProductRepository _sut;

        public ProductRepositoryTests(DatabaseFixture fixture)
        {
            _fixture = fixture;
            _sut = new ProductRepository(_fixture.CreateContext());
        }

        public async Task InitializeAsync()
        {
            await _fixture.ResetDatabaseAsync();
        }

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task AddAsync_AddsProductAndReturnsId()
        {
            var product = new Product
            {
                Codigo = "PROD1",
                Nombre = "Producto 1",
                Descripcion = "Desc",
                PrecioUnitario = 15.5m,
                Stock = 100,
                Activo = true
            };

            var id = await _sut.AddAsync(product);

            id.Should().BeGreaterThan(0);
            
            var result = await _sut.GetByIdAsync(id);
            result.Should().NotBeNull();
            result.Nombre.Should().Be("Producto 1");
        }

        [Fact]
        public async Task UpdateAsync_UpdatesExistingProduct()
        {
            var product = new Product
            {
                Codigo = "PROD2",
                Nombre = "P2",
                Descripcion = "Desc",
                PrecioUnitario = 10m,
                Stock = 10,
                Activo = true
            };
            var id = await _sut.AddAsync(product);
            
            product.Id = id;
            product.PrecioUnitario = 20m;
            
            await _sut.UpdateAsync(product);
            
            var result = await _sut.GetByIdAsync(id);
            result.PrecioUnitario.Should().Be(20m);
        }

        [Fact]
        public async Task DeleteAsync_SoftDeletesProduct()
        {
            var product = new Product { Codigo = "P3", Nombre = "P3", Descripcion = "D", Activo = true };
            var id = await _sut.AddAsync(product);

            await _sut.DeleteAsync(id);

            var result = await _sut.GetByIdAsync(id);
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetAllAsync_ReturnsOnlyActiveProducts()
        {
            await _sut.AddAsync(new Product { Codigo = "P4", Nombre = "Activo", Descripcion = "D", Activo = true });
            var id2 = await _sut.AddAsync(new Product { Codigo = "P5", Nombre = "A Borrar", Descripcion = "D", Activo = true });
            
            await _sut.DeleteAsync(id2);

            var results = await _sut.GetAllAsync();
            
            results.Should().ContainSingle(p => p.Nombre == "Activo");
            results.Should().NotContain(p => p.Id == id2);
        }

        [Fact]
        public async Task GetByCodeAsync_ReturnsMatchingProduct()
        {
            await _sut.AddAsync(new Product { Codigo = "P7", Nombre = "Prod 7", Descripcion = "D", Activo = true });

            var result = await _sut.GetByCodeAsync("P7");
            
            result.Should().NotBeNull();
            result.Nombre.Should().Be("Prod 7");
        }

        [Fact]
        public async Task GetLowStockProductsAsync_ReturnsProductsBelowThreshold()
        {
            await _sut.AddAsync(new Product { Codigo = "P10", Nombre = "Low1", Descripcion = "D", PrecioUnitario = 10m, Stock = 5, Activo = true });
            await _sut.AddAsync(new Product { Codigo = "P11", Nombre = "High1", Descripcion = "D", PrecioUnitario = 10m, Stock = 15, Activo = true });

            var results = await _sut.GetLowStockProductsAsync(10);
            
            results.Should().Contain(p => p.Nombre == "Low1");
            results.Should().NotContain(p => p.Nombre == "High1");
        }
    }
}
