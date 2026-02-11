using System;

namespace MySqlBackupTool.Client.EmbeddedForms
{
    /// <summary>
    /// Represents the current navigation state
    /// </summary>
    public class NavigationState
    {
        /// <summary>
        /// Gets or sets the type name of the form
        /// </summary>
        public string FormType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the title of the form
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the navigation path
        /// </summary>
        public string NavigationPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the timestamp when the form was activated
        /// </summary>
        public DateTime ActivatedAt { get; set; }
    }
}
