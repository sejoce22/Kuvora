using Kuvora.Domain.Catalog;

namespace Kuvora.Domain.Tests.Catalog;

public class ProductTests
{
    private static readonly Guid TenantId = Guid.CreateVersion7();
    private static readonly Guid CategoryId = Guid.CreateVersion7();

    [Fact]
    public void Constructor_WithValidData_TrimsTextAndIsActive()
    {
        var product = new Product(TenantId, CategoryId, "  Pastel de choclo ", 8900, "  Con pino y pollo ");

        Assert.Equal("Pastel de choclo", product.Name);
        Assert.Equal("Con pino y pollo", product.Description);
        Assert.Equal(8900, product.Price);
        Assert.Equal(TenantId, product.TenantId);
        Assert.Equal(CategoryId, product.CategoryId);
        Assert.True(product.IsActive);
    }

    [Fact]
    public void Constructor_WithBlankDescription_StoresNull()
    {
        var product = new Product(TenantId, CategoryId, "Pisco sour", 4500, "   ");

        Assert.Null(product.Description);
    }

    [Fact]
    public void Constructor_WithEmptyTenantId_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() => new Product(Guid.Empty, CategoryId, "Pisco sour", 4500));
    }

    [Fact]
    public void Constructor_WithEmptyCategoryId_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() => new Product(TenantId, Guid.Empty, "Pisco sour", 4500));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithBlankName_Throws(string name)
    {
        Assert.ThrowsAny<ArgumentException>(() => new Product(TenantId, CategoryId, name, 4500));
    }

    [Fact]
    public void Constructor_WithTooLongName_Throws()
    {
        var name = new string('a', Product.NameMaxLength + 1);

        Assert.Throws<ArgumentException>(() => new Product(TenantId, CategoryId, name, 4500));
    }

    [Fact]
    public void Constructor_WithTooLongDescription_Throws()
    {
        var description = new string('a', Product.DescriptionMaxLength + 1);

        Assert.Throws<ArgumentException>(() =>
            new Product(TenantId, CategoryId, "Pisco sour", 4500, description));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-1000)]
    public void Constructor_WithNegativePrice_Throws(long price)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Product(TenantId, CategoryId, "Pisco sour", price));
    }

    [Fact]
    public void Constructor_WithZeroPrice_IsAllowed()
    {
        var product = new Product(TenantId, CategoryId, "Agua de cortesía", 0);

        Assert.Equal(0, product.Price);
    }

    [Fact]
    public void Update_WithValidData_ChangesAllFields()
    {
        var product = new Product(TenantId, CategoryId, "Pisco sour", 4500);
        var otherCategoryId = Guid.CreateVersion7();

        product.Update(otherCategoryId, "  Pisco sour doble ", 6500, "Con limón de Pica");

        Assert.Equal(otherCategoryId, product.CategoryId);
        Assert.Equal("Pisco sour doble", product.Name);
        Assert.Equal(6500, product.Price);
        Assert.Equal("Con limón de Pica", product.Description);
    }

    [Fact]
    public void Update_WithInvalidPrice_KeepsPreviousValues()
    {
        var product = new Product(TenantId, CategoryId, "Pisco sour", 4500);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            product.Update(Guid.CreateVersion7(), "Otro nombre", -1));

        Assert.Equal(CategoryId, product.CategoryId);
        Assert.Equal("Pisco sour", product.Name);
        Assert.Equal(4500, product.Price);
    }

    [Fact]
    public void DeactivateAndActivate_ToggleIsActive()
    {
        var product = new Product(TenantId, CategoryId, "Pisco sour", 4500);

        product.Deactivate();
        Assert.False(product.IsActive);

        product.Activate();
        Assert.True(product.IsActive);
    }
}