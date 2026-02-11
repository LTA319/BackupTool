using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace MySqlBackupTool.Client.EmbeddedForms
{
    /// <summary>
    /// Custom control for displaying breadcrumb navigation
    /// </summary>
    public class NavigationPanel : Panel
    {
        private readonly List<Label> _breadcrumbLabels = new List<Label>();
        private readonly List<Label> _separatorLabels = new List<Label>();
        private string _currentPath = string.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="NavigationPanel"/> class
        /// </summary>
        public NavigationPanel()
        {
            InitializePanel();
        }

        /// <summary>
        /// Gets or sets the current navigation path
        /// </summary>
        public string NavigationPath
        {
            get => _currentPath;
            set
            {
                if (_currentPath != value)
                {
                    _currentPath = value;
                    UpdateBreadcrumbs();
                }
            }
        }

        /// <summary>
        /// Initializes the panel properties
        /// </summary>
        private void InitializePanel()
        {
            this.BackColor = Color.FromArgb(240, 240, 240);
            this.BorderStyle = BorderStyle.None;
            this.Dock = DockStyle.Fill;
            this.Padding = new Padding(10, 8, 10, 8);
        }

        /// <summary>
        /// Updates the breadcrumb display based on the current path
        /// </summary>
        private void UpdateBreadcrumbs()
        {
            // Clear existing controls
            ClearBreadcrumbs();

            if (string.IsNullOrWhiteSpace(_currentPath))
            {
                return;
            }

            // Split the path into segments
            var segments = _currentPath.Split(new[] { " > ", ">" }, StringSplitOptions.RemoveEmptyEntries)
                                       .Select(s => s.Trim())
                                       .Where(s => !string.IsNullOrEmpty(s))
                                       .ToArray();

            if (segments.Length == 0)
            {
                return;
            }

            // Suspend layout for better performance
            this.SuspendLayout();

            int xPosition = 10;
            const int yPosition = 10;
            const int separatorWidth = 30;

            for (int i = 0; i < segments.Length; i++)
            {
                // Create breadcrumb label
                var breadcrumbLabel = CreateBreadcrumbLabel(segments[i], xPosition, yPosition, i == segments.Length - 1);
                _breadcrumbLabels.Add(breadcrumbLabel);
                this.Controls.Add(breadcrumbLabel);

                xPosition += breadcrumbLabel.Width;

                // Add separator if not the last segment
                if (i < segments.Length - 1)
                {
                    var separatorLabel = CreateSeparatorLabel(xPosition, yPosition);
                    _separatorLabels.Add(separatorLabel);
                    this.Controls.Add(separatorLabel);

                    xPosition += separatorWidth;
                }
            }

            this.ResumeLayout();
        }

        /// <summary>
        /// Creates a breadcrumb label
        /// </summary>
        private Label CreateBreadcrumbLabel(string text, int x, int y, bool isLast)
        {
            var label = new Label
            {
                Text = text,
                AutoSize = true,
                Location = new Point(x, y),
                Font = new Font("Segoe UI", 10F, isLast ? FontStyle.Bold : FontStyle.Regular),
                ForeColor = isLast ? Color.FromArgb(51, 51, 51) : Color.FromArgb(102, 102, 102),
                Cursor = isLast ? Cursors.Default : Cursors.Hand
            };

            // Add hover effect for non-last items
            if (!isLast)
            {
                label.MouseEnter += (s, e) =>
                {
                    label.ForeColor = Color.FromArgb(0, 122, 204);
                    label.Font = new Font(label.Font, FontStyle.Underline);
                };

                label.MouseLeave += (s, e) =>
                {
                    label.ForeColor = Color.FromArgb(102, 102, 102);
                    label.Font = new Font("Segoe UI", 10F, FontStyle.Regular);
                };
            }

            return label;
        }

        /// <summary>
        /// Creates a separator label
        /// </summary>
        private Label CreateSeparatorLabel(int x, int y)
        {
            var label = new Label
            {
                Text = ">",
                AutoSize = true,
                Location = new Point(x, y),
                Font = new Font("Segoe UI", 10F, FontStyle.Regular),
                ForeColor = Color.FromArgb(153, 153, 153)
            };

            return label;
        }

        /// <summary>
        /// Clears all breadcrumb controls
        /// </summary>
        private void ClearBreadcrumbs()
        {
            foreach (var label in _breadcrumbLabels)
            {
                this.Controls.Remove(label);
                label.Dispose();
            }
            _breadcrumbLabels.Clear();

            foreach (var label in _separatorLabels)
            {
                this.Controls.Remove(label);
                label.Dispose();
            }
            _separatorLabels.Clear();
        }

        /// <summary>
        /// Cleans up resources
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ClearBreadcrumbs();
            }
            base.Dispose(disposing);
        }
    }
}
