using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;

namespace MySqlBackupTool.Client.EmbeddedForms
{
    /// <summary>
    /// Manages the lifecycle and display of embedded forms within FormMain
    /// </summary>
    public class EmbeddedFormHost : IDisposable
    {
        private readonly Panel _contentPanel;
        private Control? _currentControl;
        private IEmbeddedForm? _currentForm;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<EmbeddedFormHost> _logger;
        private readonly EmbeddedFormErrorHandler _errorHandler;
        private readonly Stack<NavigationState> _navigationHistory;
        private readonly FormTransitionManager _transitionManager;
        private bool _disposed = false;

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
            _navigationHistory = new Stack<NavigationState>();
            
            _errorHandler = new EmbeddedFormErrorHandler(
                serviceProvider.GetService(typeof(ILogger<EmbeddedFormErrorHandler>)) as ILogger<EmbeddedFormErrorHandler> 
                    ?? throw new InvalidOperationException("Failed to resolve logger for EmbeddedFormErrorHandler"),
                ShowWelcome);

            // Initialize transition manager
            var transitionLogger = serviceProvider.GetService(typeof(ILogger<FormTransitionManager>)) as ILogger<FormTransitionManager>
                ?? throw new InvalidOperationException("Failed to resolve logger for FormTransitionManager");
            _transitionManager = new FormTransitionManager(contentPanel, transitionLogger);
        }

        /// <summary>
        /// Gets the currently active embedded form
        /// </summary>
        public IEmbeddedForm? CurrentForm => _currentForm;

        /// <summary>
        /// Gets the navigation history stack
        /// </summary>
        public IReadOnlyCollection<NavigationState> NavigationHistory => _navigationHistory.ToArray();

        /// <summary>
        /// Gets the current navigation state
        /// </summary>
        public NavigationState? CurrentNavigationState => _navigationHistory.Count > 0 ? _navigationHistory.Peek() : null;

        /// <summary>
        /// Event raised when the active form changes
        /// </summary>
        public event EventHandler<IEmbeddedForm?>? ActiveFormChanged;

        /// <summary>
        /// Event raised when the active form's title changes
        /// </summary>
        public event EventHandler<string>? FormTitleChanged;

        /// <summary>
        /// Event raised when the active form's status changes
        /// </summary>
        public event EventHandler<string>? FormStatusChanged;

        /// <summary>
        /// Shows an embedded form in the content panel
        /// </summary>
        /// <typeparam name="T">The type of embedded form to show</typeparam>
        public void ShowForm<T>() where T : UserControl, IEmbeddedForm
        {
            ThrowIfDisposed();

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

                // Show control with transition
                _transitionManager.ShowWithTransition(newControl, showLoadingIndicator: true);

                // Update current references
                _currentControl = newControl;
                _currentForm = newForm;

                // Add to navigation history
                var navigationState = new NavigationState
                {
                    FormType = typeof(T).Name,
                    Title = newForm.Title,
                    NavigationPath = newForm.NavigationPath,
                    ActivatedAt = DateTime.Now
                };
                _navigationHistory.Push(navigationState);

                // Activate the form
                try
                {
                    _currentForm.OnActivated();
                    
                    // Apply visual feedback for activation
                    EmbeddedFormStyleManager.ApplyActivationFeedback(newControl);
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
            ThrowIfDisposed();

            try
            {
                _logger.LogInformation("Showing welcome screen");

                // Deactivate current form if any
                if (_currentForm != null)
                {
                    DeactivateCurrentForm();
                }

                // Clear content panel with transition
                _transitionManager.ClearWithTransition();
                
                _currentControl = null;
                _currentForm = null;

                // Clear navigation history
                _navigationHistory.Clear();

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
                _logger.LogDebug("Deactivating current form: {FormType}", _currentForm.GetType().Name);

                // Unwire events to prevent memory leaks
                _currentForm.CloseRequested -= OnFormCloseRequested;
                _currentForm.TitleChanged -= OnFormTitleChanged;
                _currentForm.StatusChanged -= OnFormStatusChanged;

                // Call deactivation lifecycle method
                _currentForm.OnDeactivated();

                // Dispose the control to free resources
                if (_currentControl is IDisposable disposable)
                {
                    _logger.LogDebug("Disposing control: {ControlType}", _currentControl.GetType().Name);
                    disposable.Dispose();
                }

                // Clear references to allow garbage collection
                _currentControl = null;
                _currentForm = null;

                // Notify GC about memory pressure relief if form was large
                GC.Collect(0, GCCollectionMode.Optimized);

                _logger.LogDebug("Form deactivation completed successfully");
            }
            catch (Exception ex)
            {
                _errorHandler.HandleDeactivationError(ex, _currentForm);
                
                // Even if deactivation fails, clear references to prevent memory leaks
                _currentControl = null;
                _currentForm = null;
            }
        }

        /// <summary>
        /// Disposes the EmbeddedFormHost and releases all resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected implementation of Dispose pattern
        /// </summary>
        /// <param name="disposing">True if disposing managed resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                try
                {
                    _logger.LogInformation("Disposing EmbeddedFormHost");

                    // Deactivate and dispose current form
                    if (_currentForm != null)
                    {
                        DeactivateCurrentForm();
                    }

                    // Dispose transition manager
                    _transitionManager?.Dispose();

                    // Clear navigation history
                    _navigationHistory.Clear();

                    // Clear content panel
                    _contentPanel.Controls.Clear();

                    _logger.LogInformation("EmbeddedFormHost disposed successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during EmbeddedFormHost disposal");
                }
            }

            _disposed = true;
        }

        /// <summary>
        /// Throws ObjectDisposedException if the host has been disposed
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(EmbeddedFormHost));
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
            
            // Propagate the title change event
            FormTitleChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Handles the StatusChanged event from embedded forms
        /// </summary>
        private void OnFormStatusChanged(object? sender, string e)
        {
            _logger.LogDebug("Form status changed to: {Status}", e);
            
            // Propagate the status change event
            FormStatusChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Gets the current navigation state for preservation (e.g., when minimizing to tray)
        /// </summary>
        /// <returns>The current navigation state, or null if on welcome screen</returns>
        public NavigationState? GetCurrentStateForPreservation()
        {
            if (_currentForm == null || CurrentNavigationState == null)
            {
                _logger.LogDebug("No active form to preserve");
                return null;
            }

            _logger.LogInformation("Preserving navigation state for form: {FormType}", CurrentNavigationState.FormType);
            return CurrentNavigationState;
        }

        /// <summary>
        /// Restores a previously saved navigation state
        /// </summary>
        /// <param name="state">The navigation state to restore</param>
        /// <returns>True if restoration was successful, false otherwise</returns>
        public bool RestoreNavigationState(NavigationState? state)
        {
            if (state == null)
            {
                _logger.LogDebug("No state to restore, showing welcome screen");
                ShowWelcome();
                return true;
            }

            try
            {
                _logger.LogInformation("Restoring navigation state for form: {FormType}", state.FormType);

                // Map form type name to actual type and show the form
                var formType = GetFormTypeByName(state.FormType);
                if (formType == null)
                {
                    _logger.LogWarning("Could not find form type: {FormType}, showing welcome screen", state.FormType);
                    ShowWelcome();
                    return false;
                }

                // Use reflection to call ShowForm<T>() with the correct type
                var showFormMethod = typeof(EmbeddedFormHost)
                    .GetMethod(nameof(ShowForm))
                    ?.MakeGenericMethod(formType);

                if (showFormMethod == null)
                {
                    _logger.LogError("Could not find ShowForm method");
                    ShowWelcome();
                    return false;
                }

                showFormMethod.Invoke(this, null);
                _logger.LogInformation("Successfully restored navigation state");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring navigation state for form: {FormType}", state.FormType);
                ShowWelcome();
                return false;
            }
        }

        /// <summary>
        /// Gets the form type by its name
        /// </summary>
        /// <param name="formTypeName">The name of the form type</param>
        /// <returns>The Type object, or null if not found</returns>
        private Type? GetFormTypeByName(string formTypeName)
        {
            // Map of known form type names to their actual types
            var formTypeMap = new Dictionary<string, Type>
            {
                { nameof(WelcomeControl), typeof(WelcomeControl) },
                { nameof(ConfigurationListControl), typeof(ConfigurationListControl) },
                { nameof(ScheduleListControl), typeof(ScheduleListControl) },
                { nameof(BackupMonitorControl), typeof(BackupMonitorControl) },
                { nameof(LogBrowserControl), typeof(LogBrowserControl) },
                { nameof(TransferLogViewerControl), typeof(TransferLogViewerControl) }
            };

            return formTypeMap.TryGetValue(formTypeName, out var type) ? type : null;
        }
    }
}
