namespace Kuvora.Api.Authorization;

public static class Policies
{
    public const string AdminOnly = nameof(AdminOnly);
    public const string ManageOrders = nameof(ManageOrders);
    public const string UseKitchen = nameof(UseKitchen);
    public const string TakePayments = nameof(TakePayments);
}