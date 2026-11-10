using Kuvora.Application.Orders;
using Microsoft.EntityFrameworkCore;

namespace Kuvora.Infrastructure.Persistence;

// Usa el mismo AppDbContext (y por lo tanto la misma conexión y transacción) que el servicio de pedidos.
internal sealed class OrderNumberGenerator(AppDbContext db) : IOrderNumberGenerator
{
    // Una sola sentencia SQL: inserta la fila del restaurante con 1 o, si ya existe, suma 1, y devuelve el
    // valor nuevo. PostgreSQL bloquea esa fila hasta el commit, así que dos pedidos simultáneos del mismo
    // restaurante se ponen en fila y nunca reciben el mismo número (con MAX(number) + 1 sí podría pasar).
    // La consulta no es "componible" (no admite Single/First encima), por eso se materializa con ToListAsync.
    public async Task<long> NextAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var numbers = await db.Database.SqlQuery<long>($"""
            INSERT INTO order_counters (tenant_id, last_number)
            VALUES ({tenantId}, 1)
            ON CONFLICT (tenant_id) DO UPDATE SET last_number = order_counters.last_number + 1
            RETURNING last_number AS "Value"
            """).ToListAsync(cancellationToken);

        return numbers.Single();
    }
}
