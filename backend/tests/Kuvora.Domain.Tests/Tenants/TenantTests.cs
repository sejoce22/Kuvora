using Kuvora.Domain.Tenants;

namespace Kuvora.Domain.Tests.Tenants;

public class TenantTests
{
    [Fact]
    public void Constructor_WithValidName_TrimsName()
    {
        var tenant = new Tenant("  Restaurante Demo  ");

        Assert.Equal("Restaurante Demo", tenant.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithEmptyName_Throws(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(() => new Tenant(name!));
    }

    [Fact]
    public void Constructor_NewTenant_IsActive()
    {
        var tenant = new Tenant("Restaurante Demo");

        Assert.True(tenant.IsActive);
    }

    [Fact]
    public void Constructor_TwoTenants_HaveDifferentNonEmptyIds()
    {
        var first = new Tenant("Restaurante Uno");
        var second = new Tenant("Restaurante Dos");

        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.NotEqual(Guid.Empty, second.Id);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void Constructor_NewTenant_CreatedAtIsUtc()
    {
        var tenant = new Tenant("Restaurante Demo");

        Assert.Equal(TimeSpan.Zero, tenant.CreatedAtUtc.Offset);
    }
}