using Kuvora.Application.Common;
using Kuvora.Domain.Orders;
using Kuvora.Domain.Tables;
using Microsoft.EntityFrameworkCore;

namespace Kuvora.Application.Tables;

internal sealed class DiningTableService(IAppDbContext db, ICurrentUser currentUser) : IDiningTableService
{
    public async Task<IReadOnlyList<DiningTableDto>> GetAllAsync(
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        // Sin Where por TenantId: el filtro global ya limita a mi restaurante
        var query = db.DiningTables.AsNoTracking();

        if (!includeInactive)
            query = query.Where(t => t.IsActive);

        // El orden "natural" (Mesa 2 antes que Mesa 10) lo resuelve la pantalla; aquí solo se agrupa por zona
        return await query
            .OrderBy(t => t.Zone)
            .ThenBy(t => t.Name)
            .Select(t => new DiningTableDto(t.Id, t.Name, t.Capacity, t.Zone, t.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<DiningTableDto> CreateAsync(
        SaveDiningTableRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureNameIsAvailableAsync(request.Name, Guid.Empty, cancellationToken);

        // La mesa siempre queda en el restaurante de quien la crea
        var table = new DiningTable(currentUser.TenantId, request.Name, request.Capacity, request.Zone);

        db.DiningTables.Add(table);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(table);
    }

    public async Task<DiningTableDto> UpdateAsync(
        Guid id,
        SaveDiningTableRequest request,
        CancellationToken cancellationToken)
    {
        var table = await GetByIdAsync(id, cancellationToken);

        await EnsureNameIsAvailableAsync(request.Name, id, cancellationToken);

        table.Update(request.Name, request.Capacity, request.Zone);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(table);
    }

    public async Task<DiningTableDto> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        var table = await GetByIdAsync(id, cancellationToken);

        // Una mesa con clientes sentados no puede desaparecer de la sala.
        if (!isActive)
        {
            var hasOpenOrder = await db.Orders.AnyAsync(
                o => o.TableId == id && o.Status == OrderStatus.Open,
                cancellationToken);

            if (hasOpenOrder)
                throw new ConflictException("No se puede desactivar una mesa con un pedido abierto.");
        }

        if (isActive)
            table.Activate();
        else
            table.Deactivate();

        await db.SaveChangesAsync(cancellationToken);

        return ToDto(table);
    }

    private async Task<DiningTable> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await db.DiningTables.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
        ?? throw new NotFoundException("La mesa no existe.");

    private async Task EnsureNameIsAvailableAsync(string name, Guid excludeId, CancellationToken cancellationToken)
    {
        var normalizedName = name.Trim().ToLowerInvariant();

        var exists = await db.DiningTables.AnyAsync(
            t => t.Id != excludeId && t.Name.ToLower() == normalizedName,
            cancellationToken);

        if (exists)
            throw new ConflictException("Ya existe una mesa con ese nombre.");
    }

    private static DiningTableDto ToDto(DiningTable table) =>
        new(table.Id, table.Name, table.Capacity, table.Zone, table.IsActive);
}
