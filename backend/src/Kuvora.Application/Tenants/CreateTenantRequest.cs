using System.ComponentModel.DataAnnotations;

namespace Kuvora.Application.Tenants;

public sealed class CreateTenantRequest
{
    [Required]
    [StringLength(120, MinimumLength = 1)]
    public string Name { get; init; } = string.Empty;
}