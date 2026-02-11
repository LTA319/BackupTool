using System;

namespace MySqlBackupTool.Client.EmbeddedForms
{
    /// <summary>
    /// Interface for forms that can be embedded in the main window
    /// </summary>
    public interface IEmbeddedForm
    {
        /// <summary>
        /// Gets the display title for this embedded form
        /// </summary>
        string Title { get; }

        /// <summary>
        /// Gets the navigation path for breadcrumb display
        /// </summary>
        string NavigationPath { get; }

        /// <summary>
        /// Called when the form is activated (shown)
        /// </summary>
        void OnActivated();

        /// <summary>
        /// Called when the form is deactivated (hidden)
        /// </summary>
        void OnDeactivated();

        /// <summary>
        /// Called to check if the form can be closed
        /// </summary>
        /// <returns>True if the form can be closed, false otherwise</returns>
        bool CanClose();

        /// <summary>
        /// Event raised when the form requests to be closed
        /// </summary>
        event EventHandler? CloseRequested;

        /// <summary>
        /// Event raised when the form's title changes
        /// </summary>
        event EventHandler<string>? TitleChanged;

        /// <summary>
        /// Event raised when the form's status message changes
        /// </summary>
        event EventHandler<string>? StatusChanged;
    }
}
