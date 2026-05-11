namespace MyAspNetApp.Models
{
    /// <summary>
    /// View model for Quick Actions component
    /// Used to render quick action buttons above text input fields
    /// </summary>
    public class QuickActionsViewModel
    {
        /// <summary>
        /// The HTML ID of the input field to target
        /// </summary>
        public string InputId { get; set; } = string.Empty;

        /// <summary>
        /// Collection of quick action buttons to display
        /// </summary>
        public IEnumerable<QuickActionButton> Buttons { get; set; } = new List<QuickActionButton>();

        /// <summary>
        /// Optional CSS class to apply to the container
        /// </summary>
        public string CssClass { get; set; } = string.Empty;

        /// <summary>
        /// Whether to show the quick actions group by default
        /// Default: true
        /// </summary>
        public bool IsVisible { get; set; } = true;
    }

    /// <summary>
    /// Represents a single quick action button
    /// </summary>
    public class QuickActionButton
    {
        /// <summary>
        /// Display label for the button
        /// </summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// Value to insert or set when button is clicked
        /// </summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// If true, sets the entire input value. If false, inserts at cursor
        /// Default: false
        /// </summary>
        public bool IsSelect { get; set; } = false;

        /// <summary>
        /// Optional CSS class for custom styling
        /// </summary>
        public string CssClass { get; set; } = string.Empty;

        /// <summary>
        /// Optional tooltip/title text
        /// </summary>
        public string Title { get; set; } = string.Empty;
    }
}
