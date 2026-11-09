using System.ComponentModel.DataAnnotations;
using Kuvora.Domain.Catalog;

namespace Kuvora.Application.Catalog;

public sealed class SaveProductRequest
{
    // Guid? para que un cuerpo sin categoría falle con 400; un Guid normal llegaría como Guid.Empty.
    [Required]
    public Guid? CategoryId { get; init; }

    [Required, StringLength(Product.NameMaxLength, MinimumLength = 1)]
    public string Name { get; init; } = null!;

    [StringLength(Product.DescriptionMaxLength)]
    public string? Description { get; init; }

    // Pesos enteros. El tope (100 millones) evita errores de tipeo, no es una regla del dominio.
    // Operandos double: con int, un long muy grande lanzaría una excepción en lugar de un 400.
    [Range(0d, 100_000_000d)]
    public long Price { get; init; }
}
