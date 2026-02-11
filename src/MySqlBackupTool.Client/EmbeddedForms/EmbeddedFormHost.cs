using System;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;

namespace MySqlBackupTool.Client.EmbeddedForms
{
    /// <summary>
    /// Manages the lifecycle and display of embedded forms within FormMain
    /// </summary>
    public class EmbeddedFormHost
    {
        private readonly Panel _contentPanel;
        private Control? _currentControl;
        private IEmbeddedForm? _currentForm;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<EmbeddedFormHost> _logger;
        private readonly EmbeddedFormErrorHandler _errorHandler;

        /// <summary>
        /// Initializes a new instance of the <see cref="EmbeddedFormHost"/> class
        /// </summary>
        /// <param name="contentPanel">The panel that will host embedded forms</param>
        /// <param name="serviceProvider">Service provider for dependency injection</param>
        /// <param name="logger">Logger instance</param>
        public EmbeddedFormHost(
            Panel contentPanel, 
            IServiceProvider serviceProvider,
            ILogger<EmbeddedFormHost> logger)
        {
            _contentPanel = contentPanel ?? throw new ArgumentNullException(nameof(contentPanel));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            _errorHandler = new EmbeddedFormErrorHandler(
                serviceProvider.GetService(typeof(ILogger<EmbeddedFormErrorHandler>)) as ILogger<EmbeddedFormErrorHandler> 
                    ?? throw new InvalidOperationException("Failed to resolve logger for EmbeddedFormErrorHandler"),
                ShowWelcome);
        }

        /// <summary>
        /// Gets the currently active embedded form
        /// </summary>
        public IEmbeddedForm? CurrentForm => _currentForm;

        /// <summary>
        /// Event raised when the active form changes
        /// </summary>
        public event EventHandler<IEmbeddedForm?>? ActiveFormChanged;

        /// <summary>
        /// Shows an embedded form in the content panel
        /// </summary>
        /// <typeparam name="T">The type of embedded form to show</typeparam>
        public void ShowForm<T>() where T : UserControl, IEmbeddedForm
        {
            try
            {
                _logger.LogInformation("Showing embedded form of type {FormType}", typeof(T).Name);

                // Deactivate current form if any
                if (_currentForm != null)
                {
                    DeactivateCurrentForm();
                }

                // Create new form instance
                var newControl = EmbeddedFormFactory.CreateForm<T>(_serviceProvider);
                var newForm = newControl as IEmbeddedForm;

                if (newForm == null)
                {
                    throw new InvalidOperationException($"Form {typeof(T).Name} does not implement IEmbeddedForm");
                }

                // Wire up events
                newForm.CloseRequested += OnFormCloseRequested;
                newForm.TitleChanged += OnFormTitleChanged;
                newForm.StatusChanged += OnFormStatusChanged;

                // Clear content panel and add new control
                _contentPanel.SuspendLayout();
                _contentPanel.Controls.Clear();
                
                newControl.Dock = DockStyle.Fill;
                _contentPanel.Controls.Add(newControl);
                _contentPanel.ResumeLayout();

                // Update current references
                _currentControl = newControl;
                _currentForm = newForm;

                // Activate the form
                try
                {
                    _currentForm.OnActivated();
                }
                catch (Exception ex)
                {
                    _errorHandler.HandleActivationError(ex, _currentForm);
                    return;
                }

                // Raise event
                ActiveFormChanged?.Invoke(this, _currentForm);

                _logger.LogInformation("Successfully showed embedded form {FormTitle}", _currentForm.Title);
            }
            catch (Exception ex)
            {
                _errorHandler.HandleCreationError(ex, typeof(T));
            }
        }

        /// <summary>
        /// Shows the welcome screen
        /// </summary>
        public void ShowWelcome()
        {
            try
            {
                _logger.LogInformation("Showing welcome screen");

                // Deactivate current form if any
                if (_currentForm != null)
                {
                    DeactivateCurrentForm();
                }

                // Clear content panel
                _contentPanel.Controls.Clear();
                _currentControl = null;
                _currentForm = null;

                // Raise event
                ActiveFormChanged?.Invoke(this, null);

                _logger.LogInformation("Successfully showed welcome screen");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing welcome screen");
            }
        }

        /// <summary>
        /// Deactivates the current form
        /// </summary>
        private void DeactivateCurrentForm()
        {
            if (_currentForm == null)
            {
                return;
            }

            try
            {
                // Unwire events
                _currentForm.CloseRequested -= OnFormCloseRequested;
                _currentForm.TitleChanged -= OnFormTitleChanged;
                _currentForm.StatusChanged -= OnFormStatusChanged;

                // Deactivate
                _currentForm.OnDeactivated();

                // Dispose if the control is disposable
                if (_currentControl is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            catch (Exception ex)
            {
                _errorHandler.HandleDeactivationError(ex, _currentForm);
            }
        }

        /// <summary>
        /// Handles the CloseRequested event from embedded forms
        /// </summary>
        private void OnFormCloseRequested(object? sender, EventArgs e)
        {
            if (_currentForm != null && _currentForm.CanClose())
            {
                ShowWelcome();
            }
        }

        /// <summary>
        /// Handles the TitleChanged event from embedded forms
        /// </summary>
        private void OnFormTitleChanged(object? sender, string e)
        {
            _logger.LogDebug("Form title changed to: {Title}", e);
        }

        /// <summary>
        /// Handles the StatusChanged event from embedded forms
        /// </summary>
        private void OnFormStatusChanged(object? sender, string e)
        {
            _logger.LogDebug("Form status changed to: {Status}", e);
        }
    }
}
