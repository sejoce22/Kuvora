using Kuvora.Domain.Tables;

namespace Kuvora.Domain.Tests.Tables;

public class DiningTableTests
{
    private static readonly Guid TenantId = Guid.CreateVersion7();

    [Fact]
    public void Constructor_WithValidData_TrimsTextAndIsActive()
    {
        var table = new DiningTable(TenantId, "  Mesa 4 ", 4, "  Terraza ");

        Assert.Equal("Mesa 4", table.Name);
        Assert.Equal(4, table.Capacity);
        Assert.Equal("Terraza", table.Zone);
        Assert.Equal(TenantId, table.TenantId);
        Assert.True(table.IsActive);
    }

    [Fact]
    public void Constructor_WithBlankZone_StoresNull()
    {
        var table = new DiningTable(TenantId, "Mesa 4", 4, "   ");

        Assert.Null(table.Zone);
    }

    [Fact]
    public void Constructor_WithEmptyTenantId_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() => new DiningTable(Guid.Empty, "Mesa 4", 4));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithBlankName_Throws(string name)
    {
        Assert.ThrowsAny<ArgumentException>(() => new DiningTable(TenantId, name, 4));
    }

    [Fact]
    public void Constructor_WithTooLongName_Throws()
    {
        var name = new string('a', DiningTable.NameMaxLength + 1);

        Assert.Throws<ArgumentException>(() => new DiningTable(TenantId, name, 4));
    }

    [Fact]
    public void Constructor_WithTooLongZone_Throws()
    {
        var zone = new string('a', DiningTable.ZoneMaxLength + 1);

        Assert.Throws<ArgumentException>(() => new DiningTable(TenantId, "Mesa 4", 4, zone));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(51)]
    public void Constructor_WithCapacityOutOfRange_Throws(int capacity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DiningTable(TenantId, "Mesa 4", capacity));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    public void Constructor_WithCapacityAtLimits_IsAllowed(int capacity)
    {
        var table = new DiningTable(TenantId, "Mesa 4", capacity);

        Assert.Equal(capacity, table.Capacity);
    }

    [Fact]
    public void Update_WithValidData_ChangesAllFields()
    {
        var table = new DiningTable(TenantId, "Mesa 4", 4, "Salón");

        table.Update("  Mesa 5 ", 6, "Terraza");

        Assert.Equal("Mesa 5", table.Name);
        Assert.Equal(6, table.Capacity);
        Assert.Equal("Terraza", table.Zone);
    }

    [Fact]
    public void Update_WithInvalidCapacity_KeepsPreviousValues()
    {
        var table = new DiningTable(TenantId, "Mesa 4", 4, "Salón");

        Assert.Throws<ArgumentOutOfRangeException>(() => table.Update("Mesa 9", 0, "Terraza"));

        Assert.Equal("Mesa 4", table.Name);
        Assert.Equal(4, table.Capacity);
        Assert.Equal("Salón", table.Zone);
    }

    [Fact]
    public void DeactivateAndActivate_ToggleIsActive()
    {
        var table = new DiningTable(TenantId, "Mesa 4", 4);

        table.Deactivate();
        Assert.False(table.IsActive);

        table.Activate();
        Assert.True(table.IsActive);
    }
}