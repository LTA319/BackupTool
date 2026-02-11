using System;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;

namespace MySqlBackupTool.Client.EmbeddedForms
{
    /// <summary>
    /// Handles errors that occur during embedded form operations
    /// </summary>
    public class EmbeddedFormErrorHandler
    {
        private readonly ILogger<EmbeddedFormErrorHandler> _logger;
        private readonly Action _recoverToWelcomeScreen;

        /// <summary>
        /// Initializes a new instance of the <see cref="EmbeddedFormErrorHandler"/> class
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="recoverToWelcomeScreen">Action to recover to welcome screen</param>
        public EmbeddedFormErrorHandler(
            ILogger<EmbeddedFormErrorHandler> logger,
            Action recoverToWelcomeScreen)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _recoverToWelcomeScreen = recoverToWelcomeScreen ?? throw new ArgumentNullException(nameof(recoverToWelcomeScreen));
        }

        /// <summary>
        /// Handles errors during form creation
        /// </summary>
        /// <param name="ex">The exception that occurred</param>
        /// <param name="formType">The type of form being created</param>
        public void HandleCreationError(Exception ex, Type formType)
        {
            _logger.LogError(ex, "Error creating embedded form of type {FormType}", formType.Name);
            
            MessageBox.Show(
                $"Failed to create form: {ex.Message}",
                "Form Creation Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            RecoverToWelcomeScreen();
        }

        /// <summary>
        /// Handles errors during form activation
        /// </summary>
        /// <param name="ex">The exception that occurred</param>
        /// <param name="form">The form being activated</param>
        public void HandleActivationError(Exception ex, IEmbeddedForm form)
        {
            _logger.LogError(ex, "Error activating embedded form {FormTitle}", form?.Title ?? "Unknown");
            
            MessageBox.Show(
                $"Failed to activate form: {ex.Message}",
                "Form Activation Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            RecoverToWelcomeScreen();
        }

        /// <summary>
        /// Handles errors during form deactivation
        /// </summary>
        /// <param name="ex">The exception that occurred</param>
        /// <param name="form">The form being deactivated</param>
        public void HandleDeactivationError(Exception ex, IEmbeddedForm form)
        {
            _logger.LogError(ex, "Error deactivating embedded form {FormTitle}", form?.Title ?? "Unknown");
            
            // Don't show message box for deactivation errors - just log and continue
            // The new form activation should proceed
        }

        /// <summary>
        /// Attempts to recover from an error by showing the welcome screen
        /// </summary>
        public void RecoverToWelcomeScreen()
        {
            try
            {
                _recoverToWelcomeScreen();
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Failed to recover to welcome screen");
                MessageBox.Show(
                    "A critical error occurred. Please restart the application.",
                    "Critical Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}
