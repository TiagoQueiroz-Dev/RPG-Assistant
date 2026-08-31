namespace RpgWorld.Domain.Actors;

public sealed record InventoryItem
{
    public InventoryItem(string itemCode, int quantity)
    {
        if (string.IsNullOrWhiteSpace(itemCode)) throw new ArgumentException("Item code is required.", nameof(itemCode));
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        ItemCode = itemCode.Trim();
        Quantity = quantity;
    }

    public string ItemCode { get; init; }
    public int Quantity { get; init; }
}
