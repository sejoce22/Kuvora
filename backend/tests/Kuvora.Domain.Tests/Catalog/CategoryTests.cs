using Kuvora.Domain.Catalog;

namespace Kuvora.Domain.Tests.Catalog;

public class CategoryTests
{
    private static readonly Guid TenantId = Guid.CreateVersion7();

    [Fact]
    public void Constructor_WithValidData_TrimsNameAndIsActive()
    {
        var category = new Category(TenantId, "  Bebidas ", 2);

        Assert.Equal("Bebidas", category.Name);
        Assert.Equal(2, category.SortOrder);
        Assert.Equal(TenantId, category.TenantId);
        Assert.True(category.IsActive);
    }

    [Fact]
    public void Constructor_WithEmptyTenantId_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() => new Category(Guid.Empty, "Bebidas"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithBlankName_Throws(string name)
    {
        Assert.ThrowsAny<ArgumentException>(() => new Category(TenantId, name));
    }

    [Fact]
    public void Constructor_WithTooLongName_Throws()
    {
        var name = new string('a', Category.NameMaxLength + 1);

        Assert.Throws<ArgumentException>(() => new Category(TenantId, name));
    }

    [Fact]
    public void Constructor_WithNegativeSortOrder_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Category(TenantId, "Bebidas", -1));
    }

    [Fact]
    public void Update_WithValidData_ChangesNameAndSortOrder()
    {
        var category = new Category(TenantId, "Bebidas", 1);

        category.Update("  Postres ", 5);

        Assert.Equal("Postres", category.Name);
        Assert.Equal(5, category.SortOrder);
    }

    [Fact]
    public void Update_WithInvalidName_KeepsPreviousValues()
    {
        var category = new Category(TenantId, "Bebidas", 1);

        Assert.ThrowsAny<ArgumentException>(() => category.Update("   ", 9));

        Assert.Equal("Bebidas", category.Name);
        Assert.Equal(1, category.SortOrder);
    }

    [Fact]
    public void DeactivateAndActivate_ToggleIsActive()
    {
        var category = new Category(TenantId, "Bebidas");

        category.Deactivate();
        Assert.False(category.IsActive);

        category.Activate();
        Assert.True(category.IsActive);
    }
}