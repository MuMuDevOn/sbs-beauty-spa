namespace SBSBeautySpa.Mobile.Models
{
    // Represents a service category (like "Nails", "Lashes", "Press-Ons").
    //
    // What it's for: Services point to a category via Service.CategoryId.
    // So instead of every service knowing its full category info, it just
    // stores the category's ID, and this class holds the category details.
    //
    // How it's managed:
    //   - CRUD (create/update/delete) via serviceCategories.js (Cloud Functions)
    //   - Reading is public — anyone can see the list of categories
    public class ServiceCategory
    {
        // The unique ID of this category.
        public string Id { get; set; } = string.Empty;

        // The category name shown to users.
        // Example: "Nails", "Lashes", "Press-Ons"
        public string Name { get; set; } = string.Empty;

        // A short description of the category.
        public string Description { get; set; } = string.Empty;

        // Whether this category is currently shown.
        // True by default.
        public bool IsActive { get; set; } = true;
    }
}