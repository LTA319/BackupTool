using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;

namespace MySqlBackupTool.Client.EmbeddedForms
{
    /// <summary>
    /// Manages consistent styling and visual feedback for embedded forms
    /// </summary>
    public static class EmbeddedFormStyleManager
    {
        // Standard colors for consistent styling
        private static readonly Color BackgroundColor = SystemColors.Control;
        private static readonly Color ActiveBorderColor = Color.FromArgb(0, 122, 204); // Blue accent
        private static readonly Color InactiveBorderColor = SystemColors.ControlDark;
        private static readonly Color HeaderBackgroundColor = Color.FromArgb(240, 240, 240);
        private static readonly Color HeaderTextColor = Color.FromArgb(51, 51, 51);

        // Standard fonts
        private static readonly Font HeaderFont = new Font("Segoe UI", 12F, FontStyle.Bold);
        private static readonly Font SubHeaderFont = new Font("Segoe UI", 10F, FontStyle.Regular);
        private static readonly Font BodyFont = new Font("Segoe UI", 9F, FontStyle.Regular);

        // Standard spacing
        private const int StandardPadding = 10;
        private const int StandardMargin = 5;
        private const int HeaderHeight = 40;

        /// <summary>
        /// Applies standard styling to an embedded form control
        /// </summary>
        /// <param name="control">The control to style</param>
        public static void ApplyStandardStyling(Control control)
        {
            if (control == null)
            {
                throw new ArgumentNullException(nameof(control));
            }

            try
            {
                control.BackColor = BackgroundColor;
                control.Font = BodyFont;
                control.Padding = new Padding(StandardPadding);
            }
            catch (Exception)
            {
                // Silently fail if styling cannot be applied
            }
        }

        /// <summary>
        /// Applies activation visual feedback to a control
        /// </summary>
        /// <param name="control">The control to apply feedback to</param>
        public static void ApplyActivationFeedback(Control control)
        {
            if (control == null)
            {
                return;
            }

            try
            {
                // Add a subtle border to indicate activation
                if (control is Panel panel)
                {
                    panel.BorderStyle = BorderStyle.FixedSingle;
                }

                // Optionally add a visual pulse effect (simplified version)
                var originalBackColor = control.BackColor;
                var highlightColor = ControlPaint.Light(originalBackColor, 0.1f);

                // Quick flash to indicate activation
                control.BackColor = highlightColor;
                
                var timer = new System.Windows.Forms.Timer { Interval = 100 };
                timer.Tick += (s, e) =>
                {
                    control.BackColor = originalBackColor;
                    timer.Stop();
                    timer.Dispose();
                };
                timer.Start();
            }
            catch (Exception)
            {
                // Silently fail if feedback cannot be applied
            }
        }

        /// <summary>
        /// Removes activation visual feedback from a control
        /// </summary>
        /// <param name="control">The control to remove feedback from</param>
        public static void RemoveActivationFeedback(Control control)
        {
            if (control == null)
            {
                return;
            }

            try
            {
                // Remove border
                if (control is Panel panel)
                {
                    panel.BorderStyle = BorderStyle.None;
                }
            }
            catch (Exception)
            {
                // Silently fail if feedback cannot be removed
            }
        }

        /// <summary>
        /// Creates a styled header panel for an embedded form
        /// </summary>
        /// <param name="title">The title text</param>
        /// <param name="subtitle">Optional subtitle text</param>
        /// <returns>A styled header panel</returns>
        public static Panel CreateStyledHeader(string title, string? subtitle = null)
        {
            var headerPanel = new Panel
            {
                Height = HeaderHeight,
                Dock = DockStyle.Top,
                BackColor = HeaderBackgroundColor,
                Padding = new Padding(StandardPadding)
            };

            var titleLabel = new Label
            {
                Text = title,
                Font = HeaderFont,
                ForeColor = HeaderTextColor,
                AutoSize = true,
                Location = new Point(StandardPadding, StandardPadding)
            };

            headerPanel.Controls.Add(titleLabel);

            if (!string.IsNullOrEmpty(subtitle))
            {
                var subtitleLabel = new Label
                {
                    Text = subtitle,
                    Font = SubHeaderFont,
                    ForeColor = Color.FromArgb(102, 102, 102),
                    AutoSize = true,
                    Location = new Point(StandardPadding, StandardPadding + 20)
                };

                headerPanel.Controls.Add(subtitleLabel);
                headerPanel.Height = HeaderHeight + 20;
            }

            return headerPanel;
        }

        /// <summary>
        /// Applies consistent button styling
        /// </summary>
        /// <param name="button">The button to style</param>
        /// <param name="isPrimary">Whether this is a primary action button</param>
        public static void ApplyButtonStyling(Button button, bool isPrimary = false)
        {
            if (button == null)
            {
                return;
            }

            try
            {
                button.Font = BodyFont;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderSize = 1;
                button.Height = 30;
                button.Padding = new Padding(10, 5, 10, 5);

                if (isPrimary)
                {
                    button.BackColor = ActiveBorderColor;
                    button.ForeColor = Color.White;
                    button.FlatAppearance.BorderColor = ActiveBorderColor;
                }
                else
                {
                    button.BackColor = SystemColors.Control;
                    button.ForeColor = SystemColors.ControlText;
                    button.FlatAppearance.BorderColor = InactiveBorderColor;
                }
            }
            catch (Exception)
            {
                // Silently fail if styling cannot be applied
            }
        }

        /// <summary>
        /// Applies consistent DataGridView styling
        /// </summary>
        /// <param name="dataGridView">The DataGridView to style</param>
        public static void ApplyDataGridViewStyling(DataGridView dataGridView)
        {
            if (dataGridView == null)
            {
                return;
            }

            try
            {
                dataGridView.BackgroundColor = BackgroundColor;
                dataGridView.BorderStyle = BorderStyle.None;
                dataGridView.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
                dataGridView.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
                dataGridView.EnableHeadersVisualStyles = false;
                dataGridView.GridColor = Color.FromArgb(224, 224, 224);
                dataGridView.RowHeadersVisible = false;
                dataGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
                dataGridView.AllowUserToAddRows = false;
                dataGridView.AllowUserToDeleteRows = false;
                dataGridView.AllowUserToResizeRows = false;
                dataGridView.MultiSelect = false;
                dataGridView.ReadOnly = true;

                // Column header styling
                dataGridView.ColumnHeadersDefaultCellStyle.BackColor = HeaderBackgroundColor;
                dataGridView.ColumnHeadersDefaultCellStyle.ForeColor = HeaderTextColor;
                dataGridView.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                dataGridView.ColumnHeadersDefaultCellStyle.SelectionBackColor = HeaderBackgroundColor;
                dataGridView.ColumnHeadersDefaultCellStyle.SelectionForeColor = HeaderTextColor;
                dataGridView.ColumnHeadersHeight = 35;

                // Cell styling
                dataGridView.DefaultCellStyle.BackColor = Color.White;
                dataGridView.DefaultCellStyle.ForeColor = SystemColors.ControlText;
                dataGridView.DefaultCellStyle.Font = BodyFont;
                dataGridView.DefaultCellStyle.SelectionBackColor = ActiveBorderColor;
                dataGridView.DefaultCellStyle.SelectionForeColor = Color.White;
                dataGridView.DefaultCellStyle.Padding = new Padding(5);

                // Alternating row styling
                dataGridView.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 250, 250);
            }
            catch (Exception)
            {
                // Silently fail if styling cannot be applied
            }
        }

        /// <summary>
        /// Optimizes layout performance for a control during resize operations
        /// </summary>
        /// <param name="control">The control to optimize</param>
        public static void OptimizeLayoutPerformance(Control control)
        {
            if (control == null)
            {
                return;
            }

            try
            {
                // Suspend layout during bulk operations
                control.SuspendLayout();

                // Set double buffering for smoother rendering
                if (control is Form || control is UserControl || control is Panel)
                {
                    var type = control.GetType();
                    var property = type.GetProperty("DoubleBuffered",
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic);

                    if (property != null)
                    {
                        property.SetValue(control, true, null);
                    }
                }

                // Resume layout
                control.ResumeLayout(performLayout: true);
            }
            catch (Exception)
            {
                // Silently fail if optimization cannot be applied
            }
        }

        /// <summary>
        /// Gets the standard padding value
        /// </summary>
        public static int GetStandardPadding() => StandardPadding;

        /// <summary>
        /// Gets the standard margin value
        /// </summary>
        public static int GetStandardMargin() => StandardMargin;

        /// <summary>
        /// Gets the standard header height
        /// </summary>
        public static int GetHeaderHeight() => HeaderHeight;

        /// <summary>
        /// Gets the standard body font
        /// </summary>
        public static Font GetBodyFont() => BodyFont;

        /// <summary>
        /// Gets the standard header font
        /// </summary>
        public static Font GetHeaderFont() => HeaderFont;
    }
}
