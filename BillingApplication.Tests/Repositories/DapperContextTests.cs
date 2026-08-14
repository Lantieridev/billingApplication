using System;
using BillingApplication.Data;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;
using FluentAssertions;

namespace BillingApplication.Tests.Repositories
{
    public class DapperContextTests
    {
        [Fact]
        public void Constructor_ThrowsInvalidOperationException_WhenConnectionStringIsNull()
        {
            var config = new ConfigurationBuilder().Build();
            Action act = () => new DapperContext(config);
            act.Should().Throw<InvalidOperationException>().WithMessage("Missing DefaultConnection");
        }
    }
}
