namespace InventorySystem.Models.Interfaces
{
    public interface ISellableEntity
    {
        int Id { get; }
        string DisplayName { get; }
        string Description { get; }
        decimal SalePrice { get; }
        bool IsSellable { get; }
        string EntityType { get; }
        string? Code { get; }
    }
}