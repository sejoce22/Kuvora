namespace Kuvora.Application.Tenants;

public sealed record TenantDto(Guid Id, string Name, bool IsActive, DateTimeOffset CreatedAtUtc);