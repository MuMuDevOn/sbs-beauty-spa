using CommunityToolkit.Mvvm.ComponentModel;

namespace SBSBeautySpa.Mobile.ViewModels
{
    /// <summary>
    /// A tappable filter tab/pill with its own IsSelected flag, so the View
    /// can style the active one directly. Shared by the Shop category tabs
    /// and the Gallery category sidebar. Id is null for an "All" tab.
    /// </summary>
    public partial class SelectableTab : ObservableObject
    {
        public string? Id { get; }
        public string Label { get; }

        [ObservableProperty]
        private bool isSelected;

        public SelectableTab(string? id, string label, bool isSelected = false)
        {
            Id = id;
            Label = label;
            IsSelected = isSelected;
        }
    }
}