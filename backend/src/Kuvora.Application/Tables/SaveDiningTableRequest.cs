using System.ComponentModel.DataAnnotations;
using Kuvora.Domain.Tables;

namespace Kuvora.Application.Tables;

public sealed class SaveDiningTableRequest
{
    [Required, StringLength(DiningTable.NameMaxLength, MinimumLength = 1)]
    public string Name { get; init; } = null!;

    [Range(DiningTable.MinCapacity, DiningTable.MaxCapacity)]
    public int Capacity { get; init; }

    [StringLength(DiningTable.ZoneMaxLength)]
    public string? Zone { get; init; }
}
