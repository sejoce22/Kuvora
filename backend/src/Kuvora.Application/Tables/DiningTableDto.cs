namespace Kuvora.Application.Tables;

public sealed record DiningTableDto(Guid Id, string Name, int Capacity, string? Zone, bool IsActive);
