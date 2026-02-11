using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using WinFormsTimer = System.Windows.Forms.Timer;

namespace MySqlBackupTool.Client.EmbeddedForms
{
    /// <summary>
    /// Manages smooth transitions between embedded forms with fade effects and loading indicators
    /// </summary>
    public class FormTransitionManager
    {
        private readonly Panel _contentPanel;
        private readonly ILogger<FormTransitionManager> _logger;
        private readonly WinFormsTimer _fadeTimer;
        private Control? _transitioningControl;
        private bool _isFadingIn;
        private double _opacity;
        private const int FADE_STEPS = 10;
        private const int FADE_INTERVAL_MS = 20;
        private readonly Panel _loadingPanel;
        private readonly Label _loadingLabel;
        private readonly ProgressBar _loadingProgressBar;

        /// <summary>
        /// Initializes a new instance of the FormTransitionManager
        /// </summary>
        /// <param name="contentPanel">The panel that hosts embedded forms</param>
        /// <param name="logger">Logger instance</param>
        public FormTransitionManager(Panel contentPanel, ILogger<FormTransitionManager> logger)
        {
            _contentPanel = contentPanel ?? throw new ArgumentNullException(nameof(contentPanel));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _fadeTimer = new WinFormsTimer
            {
                Interval = FADE_INTERVAL_MS
            };
            _fadeTimer.Tick += OnFadeTimerTick;

            // Create loading indicator panel
            _loadingPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = _contentPanel.BackColor,
                Visible = false
            };

            _loadingLabel = new Label
            {
                Text = "Loading...",
                Font = new Font("Segoe UI", 12F, FontStyle.Regular),
                ForeColor = SystemColors.ControlText,
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleCenter
            };

            _loadingProgressBar = new ProgressBar
            {
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 30,
                Width = 300,
                Height = 23
            };

            _loadingPanel.Controls.Add(_loadingLabel);
            _loadingPanel.Controls.Add(_loadingProgressBar);
            _loadingPanel.Resize += OnLoadingPanelResize;
        }

        /// <summary>
        /// Shows a control with a fade-in transition
        /// </summary>
        /// <param name="control">The control to show</param>
        /// <param name="showLoadingIndicator">Whether to show a loading indicator during transition</param>
        public void ShowWithTransition(Control control, bool showLoadingIndicator = false)
        {
            if (control == null)
            {
                throw new ArgumentNullException(nameof(control));
            }

            try
            {
                _logger.LogDebug("Starting transition to show control: {ControlType}", control.GetType().Name);

                // Stop any ongoing transition
                StopTransition();

                // Show loading indicator if requested
                if (showLoadingIndicator)
                {
                    ShowLoadingIndicator();
                }

                // Prepare the control for fade-in
                _transitioningControl = control;
                _opacity = 0.0;
                _isFadingIn = true;

                // Set initial opacity (simulate by adjusting control visibility)
                control.Visible = false;

                // Add control to panel
                _contentPanel.SuspendLayout();
                _contentPanel.Controls.Clear();
                control.Dock = DockStyle.Fill;
                _contentPanel.Controls.Add(control);
                _contentPanel.ResumeLayout();

                // Start fade-in animation
                _fadeTimer.Start();

                _logger.LogDebug("Transition started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting transition for control: {ControlType}", control.GetType().Name);
                
                // Fallback: show control immediately without transition
                ShowImmediately(control);
            }
        }

        /// <summary>
        /// Clears the content panel with a fade-out transition
        /// </summary>
        public void ClearWithTransition()
        {
            try
            {
                _logger.LogDebug("Starting transition to clear content panel");

                // Stop any ongoing transition
                StopTransition();

                // If there's a control to fade out
                if (_contentPanel.Controls.Count > 0)
                {
                    _transitioningControl = _contentPanel.Controls[0];
                    _opacity = 1.0;
                    _isFadingIn = false;

                    // Start fade-out animation
                    _fadeTimer.Start();
                }
                else
                {
                    // Nothing to fade out, just clear
                    _contentPanel.Controls.Clear();
                }

                _logger.LogDebug("Clear transition started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting clear transition");
                
                // Fallback: clear immediately
                _contentPanel.Controls.Clear();
            }
        }

        /// <summary>
        /// Shows a control immediately without transition
        /// </summary>
        /// <param name="control">The control to show</param>
        public void ShowImmediately(Control control)
        {
            if (control == null)
            {
                throw new ArgumentNullException(nameof(control));
            }

            try
            {
                StopTransition();
                HideLoadingIndicator();

                _contentPanel.SuspendLayout();
                _contentPanel.Controls.Clear();
                control.Dock = DockStyle.Fill;
                control.Visible = true;
                _contentPanel.Controls.Add(control);
                _contentPanel.ResumeLayout();

                _logger.LogDebug("Control shown immediately: {ControlType}", control.GetType().Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing control immediately: {ControlType}", control.GetType().Name);
            }
        }

        /// <summary>
        /// Shows the loading indicator
        /// </summary>
        private void ShowLoadingIndicator()
        {
            try
            {
                if (!_contentPanel.Controls.Contains(_loadingPanel))
                {
                    _contentPanel.Controls.Add(_loadingPanel);
                    _loadingPanel.BringToFront();
                }

                CenterLoadingControls();
                _loadingPanel.Visible = true;
                _loadingProgressBar.Visible = true;

                _logger.LogDebug("Loading indicator shown");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing loading indicator");
            }
        }

        /// <summary>
        /// Hides the loading indicator
        /// </summary>
        private void HideLoadingIndicator()
        {
            try
            {
                _loadingPanel.Visible = false;
                _loadingProgressBar.Visible = false;

                if (_contentPanel.Controls.Contains(_loadingPanel))
                {
                    _contentPanel.Controls.Remove(_loadingPanel);
                }

                _logger.LogDebug("Loading indicator hidden");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error hiding loading indicator");
            }
        }

        /// <summary>
        /// Centers the loading controls in the loading panel
        /// </summary>
        private void CenterLoadingControls()
        {
            try
            {
                int centerX = _loadingPanel.Width / 2;
                int centerY = _loadingPanel.Height / 2;

                _loadingProgressBar.Location = new Point(
                    centerX - _loadingProgressBar.Width / 2,
                    centerY - 10);

                _loadingLabel.Location = new Point(
                    centerX - _loadingLabel.Width / 2,
                    centerY - 40);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error centering loading controls");
            }
        }

        /// <summary>
        /// Handles the loading panel resize event
        /// </summary>
        private void OnLoadingPanelResize(object? sender, EventArgs e)
        {
            CenterLoadingControls();
        }

        /// <summary>
        /// Stops any ongoing transition
        /// </summary>
        private void StopTransition()
        {
            _fadeTimer.Stop();
            _transitioningControl = null;
        }

        /// <summary>
        /// Handles the fade timer tick event
        /// </summary>
        private void OnFadeTimerTick(object? sender, EventArgs e)
        {
            try
            {
                if (_transitioningControl == null)
                {
                    _fadeTimer.Stop();
                    return;
                }

                if (_isFadingIn)
                {
                    // Fade in
                    _opacity += 1.0 / FADE_STEPS;

                    if (_opacity >= 1.0)
                    {
                        _opacity = 1.0;
                        _transitioningControl.Visible = true;
                        _fadeTimer.Stop();
                        HideLoadingIndicator();
                        _transitioningControl = null;
                        _logger.LogDebug("Fade-in transition completed");
                    }
                    else
                    {
                        // Show control when opacity reaches threshold
                        if (_opacity >= 0.1 && !_transitioningControl.Visible)
                        {
                            _transitioningControl.Visible = true;
                            HideLoadingIndicator();
                        }
                    }
                }
                else
                {
                    // Fade out
                    _opacity -= 1.0 / FADE_STEPS;

                    if (_opacity <= 0.0)
                    {
                        _opacity = 0.0;
                        _contentPanel.Controls.Clear();
                        _fadeTimer.Stop();
                        _transitioningControl = null;
                        _logger.LogDebug("Fade-out transition completed");
                    }
                    else
                    {
                        // Hide control when opacity reaches threshold
                        if (_opacity <= 0.9 && _transitioningControl.Visible)
                        {
                            _transitioningControl.Visible = false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during fade transition");
                _fadeTimer.Stop();
                
                // Fallback: complete the transition immediately
                if (_transitioningControl != null)
                {
                    if (_isFadingIn)
                    {
                        _transitioningControl.Visible = true;
                        HideLoadingIndicator();
                    }
                    else
                    {
                        _contentPanel.Controls.Clear();
                    }
                }
                
                _transitioningControl = null;
            }
        }

        /// <summary>
        /// Disposes the transition manager and releases resources
        /// </summary>
        public void Dispose()
        {
            try
            {
                _fadeTimer.Stop();
                _fadeTimer.Tick -= OnFadeTimerTick;
                _fadeTimer.Dispose();

                _loadingPanel.Resize -= OnLoadingPanelResize;
                _loadingPanel.Dispose();
                _loadingLabel.Dispose();
                _loadingProgressBar.Dispose();

                _logger.LogDebug("FormTransitionManager disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing FormTransitionManager");
            }
        }
    }
}
