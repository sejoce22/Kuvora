using System.ComponentModel.DataAnnotations;
using Kuvora.Domain.Catalog;

namespace Kuvora.Application.Catalog;

public sealed class SaveCategoryRequest
{
    [Required, StringLength(Category.NameMaxLength, MinimumLength = 1)]
    public string Name { get; init; } = null!;

    [Range(0, int.MaxValue)]
    public int SortOrder { get; init; }
}
