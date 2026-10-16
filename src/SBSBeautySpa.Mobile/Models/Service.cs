using CommunityToolkit.Mvvm.ComponentModel;

namespace SBSBeautySpa.Mobile.Models
{
    public partial class Service : ObservableObject
    {
        public string Id { get; set; } = string.Empty;
        public string CategoryId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal DepositAmount { get; set; }
        public int DurationMinutes { get; set; }
        public int BufferMinutes { get; set; }

        [ObservableProperty]
        private bool isActive = true;

        public string? ImageUrl { get; set; }
        public List<string> AddOnServiceIds { get; set; } = new();
    }
}