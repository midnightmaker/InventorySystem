using InventorySystem.Models;
using InventorySystem.Models.Enums;

namespace InventorySystem.ViewModels
{
    public class CustomerIndexViewModel
    {
        public IEnumerable<Customer> Customers { get; set; } = new List<Customer>();
        public string? SearchTerm { get; set; }
        public CustomerType? CustomerType { get; set; }
        public bool? ActiveOnly { get; set; }
        public IEnumerable<CustomerType> CustomerTypes { get; set; } = Enum.GetValues<CustomerType>();
    }
}