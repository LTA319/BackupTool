using System;
using System.Windows.Forms;

namespace MySqlBackupTool.Client.EmbeddedForms
{
    /// <summary>
    /// Default welcome screen displayed when no tool is active
    /// </summary>
    public partial class WelcomeControl : UserControl, IEmbeddedForm
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WelcomeControl"/> class
        /// </summary>
        public WelcomeControl()
        {
            InitializeComponent();
        }

        #region IEmbeddedForm Implementation

        /// <summary>
        /// Gets the display title for this embedded form
        /// </summary>
        public string Title => "Welcome";

        /// <summary>
        /// Gets the navigation path for breadcrumb display
        /// </summary>
        public string NavigationPath => "Home";

        /// <summary>
        /// Event raised when the form requests to be closed
        /// </summary>
        public event EventHandler? CloseRequested;

        /// <summary>
        /// Event raised when the form's title changes
        /// </summary>
        public event EventHandler<string>? TitleChanged;

        /// <summary>
        /// Event raised when the form's status message changes
        /// </summary>
        public event EventHandler<string>? StatusChanged;

        /// <summary>
        /// Called when the form is activated (shown)
        /// </summary>
        public void OnActivated()
        {
            // Welcome screen doesn't need special activation logic
            // Could load recent activity here if needed
        }

        /// <summary>
        /// Called when the form is deactivated (hidden)
        /// </summary>
        public void OnDeactivated()
        {
            // Welcome screen doesn't need special deactivation logic
        }

        /// <summary>
        /// Called to check if the form can be closed
        /// </summary>
        /// <returns>True if the form can be closed, false otherwise</returns>
        public bool CanClose()
        {
            // Welcome screen can always be closed
            return true;
        }

        #endregion
    }
}
