using System.ComponentModel.DataAnnotations;
using Kuvora.Domain.Orders;

namespace Kuvora.Application.Orders;

public sealed class OpenOrderRequest : IValidatableObject
{
    // Nullable + Required: un enum normal llegaría como 0 si el cliente lo omite y pasaría sin error.
    [Required]
    public OrderChannel? Channel { get; init; }

    public Guid? TableId { get; init; }

    [Range(Order.GuestCountMin, Order.GuestCountMax)]
    public int? GuestCount { get; init; }

    // Reglas que cruzan campos: [ApiController] las ejecuta y responde 400 automáticamente.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Channel is { } channel && !Enum.IsDefined(channel))
            yield return new ValidationResult("El canal no es válido.", [nameof(Channel)]);

        if (Channel == OrderChannel.DineIn && (TableId is null || TableId == Guid.Empty))
            yield return new ValidationResult("Un pedido de salón exige una mesa.", [nameof(TableId)]);

        if (Channel == OrderChannel.Counter && TableId is not null)
            yield return new ValidationResult("Un pedido de mostrador no lleva mesa.", [nameof(TableId)]);
    }
}
