using System;

namespace MySqlBackupTool.Client.EmbeddedForms
{
    /// <summary>
    /// Metadata about an embedded form type
    /// </summary>
    public class EmbeddedFormMetadata
    {
        /// <summary>
        /// Gets or sets the form type
        /// </summary>
        public Type FormType { get; set; } = typeof(object);

        /// <summary>
        /// Gets or sets the menu item name
        /// </summary>
        public string MenuItemName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the default title
        /// </summary>
        public string DefaultTitle { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the navigation path
        /// </summary>
        public string NavigationPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether the form requires confirmation to close
        /// </summary>
        public bool RequiresConfirmationToClose { get; set; }
    }
}
