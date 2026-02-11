# Design Document

## Overview

This design transforms the MySqlBackupTool.Client application from a traditional multi-window MDI (Multiple Document Interface) approach to a modern single-window interface with embedded content panels. The design follows a container-based pattern where FormMain acts as the host window, and tool forms are embedded as user controls within a central content panel. This approach provides a cohesive user experience similar to modern IDEs like Visual Studio, while maintaining all existing functionality.

The key architectural change is converting existing Form-based tools into embeddable user controls that can be dynamically loaded and displayed within the main window's content area. This requires minimal changes to existing form logic while providing a flexible framework for future extensions.

## Architecture

### High-Level Architecture

```
FormMain (Host Window)
├── MenuStrip (Navigation)
├── ContentPanel (Dynamic Content Area)
│   ├── WelcomeControl (Default View)
│   └── [Embedded Form Controls]
│       ├── ConfigurationListControl
│       ├── ScheduleListControl
│       ├── BackupMonitorControl
│       ├── LogBrowserControl
│       └── TransferLogViewerControl
├── NavigationPanel (Breadcrumb/Title Bar)
└── StatusStrip (Status Information)
```

### Component Layers

1. **Host Layer**: FormMain manages the overall window lifecycle, menu system, and system tray integration
2. **Navigation Layer**: Handles menu clicks and routes to appropriate embedded controls
3. **Content Layer**: Dynamic panel that hosts embedded user controls
4. **Embedded Control Layer**: Individual tool implementations as UserControl derivatives
5. **Service Layer**: Existing dependency injection and service infrastructure (unchanged)

### Design Patterns

- **Container Pattern**: FormMain acts as a container for embedded controls
- **Factory Pattern**: EmbeddedFormFactory creates and configures embedded controls
- **Strategy Pattern**: Different embedded controls implement common IEmbeddedForm interface
- **Observer Pattern**: Embedded controls notify host of state changes via events
- **Dependency Injection**: ServiceProvider passed to embedded controls for service resolution

## Components and Interfaces

### Core Interfaces

#### IEmbeddedForm Interface

```csharp
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
    event EventHandler CloseRequested;
    
    /// <summary>
    /// Event raised when the form's title changes
    /// </summary>
    event EventHandler<string> TitleChanged;
    
    /// <summary>
    /// Event raised when the form's status message changes
    /// </summary>
    event EventHandler<string> StatusChanged;
}
```

### Core Components

#### 1. EmbeddedFormHost

```csharp
/// <summary>
/// Manages the lifecycle and display of embedded forms within FormMain
/// </summary>
public class EmbeddedFormHost
{
    private Panel _contentPanel;
    private Control? _currentControl;
    private IEmbeddedForm? _currentForm;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EmbeddedFormHost> _logger;
    
    public EmbeddedFormHost(Panel contentPanel, IServiceProvider serviceProvider);
    
    /// <summary>
    /// Shows an embedded form in the content panel
    /// </summary>
    public void ShowForm<T>() where T : UserControl, IEmbeddedForm;
    
    /// <summary>
    /// Shows the welcome screen
    /// </summary>
    public void ShowWelcome();
    
    /// <summary>
    /// Gets the currently active embedded form
    /// </summary>
    public IEmbeddedForm? CurrentForm { get; }
    
    /// <summary>
    /// Event raised when the active form changes
    /// </summary>
    public event EventHandler<IEmbeddedForm?> ActiveFormChanged;
}
```

#### 2. EmbeddedFormFactory

```csharp
/// <summary>
/// Factory for creating embedded form instances
/// </summary>
public static class EmbeddedFormFactory
{
    /// <summary>
    /// Creates an embedded form instance
    /// </summary>
    public static T CreateForm<T>(IServiceProvider serviceProvider) 
        where T : UserControl, IEmbeddedForm;
}
```

#### 3. WelcomeControl

```csharp
/// <summary>
/// Default welcome screen displayed when no tool is active
/// </summary>
public class WelcomeControl : UserControl, IEmbeddedForm
{
    // Displays welcome message, quick actions, recent activity
}
```

#### 4. Embedded Form Controls

Each existing form will be converted to a UserControl that implements IEmbeddedForm:

- **ConfigurationListControl**: Backup configuration management
- **ScheduleListControl**: Schedule configuration management
- **BackupMonitorControl**: Backup monitoring and control
- **LogBrowserControl**: Log viewing and filtering
- **TransferLogViewerControl**: Transfer log viewing

### Conversion Strategy

Existing forms will be converted using this approach:

1. Create new UserControl class (e.g., ConfigurationListControl)
2. Copy all private fields, methods, and event handlers from existing Form
3. Implement IEmbeddedForm interface
4. Replace Form-specific code (DialogResult, ShowDialog, Close) with IEmbeddedForm patterns
5. Move Designer code to UserControl designer
6. Keep existing Form classes for backward compatibility (if needed)

## Data Models

### NavigationState

```csharp
/// <summary>
/// Represents the current navigation state
/// </summary>
public class NavigationState
{
    public string FormType { get; set; }
    public string Title { get; set; }
    public string NavigationPath { get; set; }
    public DateTime ActivatedAt { get; set; }
}
```

### EmbeddedFormMetadata

```csharp
/// <summary>
/// Metadata about an embedded form type
/// </summary>
public class EmbeddedFormMetadata
{
    public Type FormType { get; set; }
    public string MenuItemName { get; set; }
    public string DefaultTitle { get; set; }
    public string NavigationPath { get; set; }
    public bool RequiresConfirmationToClose { get; set; }
}
```

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system-essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Single Active Form Invariant

*For any* state of the application, at most one embedded form SHALL be active and displayed in the Content Panel at any given time.

**Validates: Requirements 1.2, 1.4**

### Property 2: Form Lifecycle Consistency

*For any* embedded form, when it is activated then deactivated then activated again, the form SHALL maintain its state and data consistency across activation cycles.

**Validates: Requirements 3.1, 3.2**

### Property 3: Service Provider Propagation

*For any* embedded form that requires dependency injection, the ServiceProvider passed to the form SHALL be the same instance used by FormMain, ensuring consistent service resolution.

**Validates: Requirements 3.5**

### Property 4: Layout Responsiveness

*For any* window resize operation, the embedded form SHALL resize to fill the available Content Panel area while maintaining its minimum size constraints.

**Validates: Requirements 4.1, 4.2, 4.4**

### Property 5: Navigation State Preservation

*For any* embedded form, when the application is minimized to tray and then restored, the previously active form SHALL be displayed in the same state.

**Validates: Requirements 9.1, 9.2**

### Property 6: Event Handler Cleanup

*For any* embedded form that is deactivated, all event handlers registered by that form SHALL be properly unregistered to prevent memory leaks.

**Validates: Requirements 6.4**

### Property 7: Title Synchronization

*For any* embedded form that is active, changes to the form's Title property SHALL be reflected in the FormMain title bar within one UI update cycle.

**Validates: Requirements 2.1**

### Property 8: Status Message Propagation

*For any* embedded form that updates its status, the status message SHALL be displayed in the FormMain status bar.

**Validates: Requirements 2.2**

### Property 9: Keyboard Shortcut Isolation

*For any* embedded form with keyboard shortcuts, those shortcuts SHALL only be active when that form is the active embedded form.

**Validates: Requirements 8.1, 8.2**

### Property 10: Modal Dialog Independence

*For any* modal dialog (like Test Database Connection), opening the dialog SHALL NOT change the current embedded form state, and closing the dialog SHALL return focus to the previously active embedded form.

**Validates: Requirements 5.1, 5.2, 5.3**

## Error Handling

### Error Categories

1. **Form Creation Errors**: Failures during embedded form instantiation
2. **Service Resolution Errors**: Dependency injection failures
3. **Layout Errors**: Issues with form sizing or positioning
4. **State Transition Errors**: Problems during form activation/deactivation
5. **Resource Cleanup Errors**: Failures during form disposal

### Error Handling Strategy

```csharp
public class EmbeddedFormErrorHandler
{
    /// <summary>
    /// Handles errors during form creation
    /// </summary>
    public void HandleCreationError(Exception ex, Type formType);
    
    /// <summary>
    /// Handles errors during form activation
    /// </summary>
    public void HandleActivationError(Exception ex, IEmbeddedForm form);
    
    /// <summary>
    /// Handles errors during form deactivation
    /// </summary>
    public void HandleDeactivationError(Exception ex, IEmbeddedForm form);
    
    /// <summary>
    /// Attempts to recover from an error by showing the welcome screen
    /// </summary>
    public void RecoverToWelcomeScreen();
}
```

### Error Recovery

- **Creation Failure**: Log error, show message box, display welcome screen
- **Activation Failure**: Log error, attempt to show previous form, fallback to welcome screen
- **Deactivation Failure**: Log error, force cleanup, continue with new form activation
- **Layout Failure**: Log error, apply default layout, notify user if critical
- **Cleanup Failure**: Log error, mark resources for garbage collection, continue operation

## Testing Strategy

### Unit Testing

Unit tests will verify specific behaviors and edge cases:

1. **EmbeddedFormHost Tests**
   - Test showing a form updates CurrentForm property
   - Test showing welcome screen clears CurrentForm
   - Test switching between forms properly deactivates previous form
   - Test ActiveFormChanged event fires correctly

2. **IEmbeddedForm Implementation Tests**
   - Test each embedded control implements interface correctly
   - Test OnActivated/OnDeactivated lifecycle methods
   - Test CanClose returns appropriate values
   - Test event raising (CloseRequested, TitleChanged, StatusChanged)

3. **EmbeddedFormFactory Tests**
   - Test factory creates correct control types
   - Test factory passes ServiceProvider correctly
   - Test factory handles creation errors gracefully

4. **Navigation Tests**
   - Test menu clicks navigate to correct forms
   - Test navigation state is tracked correctly
   - Test breadcrumb updates match active form

5. **Layout Tests**
   - Test forms fill content panel on creation
   - Test forms resize with window
   - Test minimum size constraints are enforced

### Property-Based Testing

Property-based tests will use FsCheck to verify universal properties across many inputs:

1. **Property Test for Single Active Form Invariant (Property 1)**
   - Generate random sequences of form navigation commands
   - Verify only one form is active after each command
   - **Validates: Requirements 1.2, 1.4**

2. **Property Test for Form Lifecycle Consistency (Property 2)**
   - Generate random activation/deactivation sequences
   - Verify form state remains consistent
   - **Validates: Requirements 3.1, 3.2**

3. **Property Test for Service Provider Propagation (Property 3)**
   - Generate random form types
   - Verify ServiceProvider instance is consistent
   - **Validates: Requirements 3.5**

4. **Property Test for Layout Responsiveness (Property 4)**
   - Generate random window sizes
   - Verify embedded forms resize correctly
   - **Validates: Requirements 4.1, 4.2, 4.4**

5. **Property Test for Event Handler Cleanup (Property 6)**
   - Generate random form activation/deactivation cycles
   - Verify no memory leaks from event handlers
   - **Validates: Requirements 6.4**

### Integration Testing

Integration tests will verify end-to-end workflows:

1. **Full Navigation Workflow**
   - Start application
   - Navigate through all tools
   - Verify each tool displays correctly
   - Return to welcome screen

2. **System Tray Integration**
   - Minimize to tray with active form
   - Restore from tray
   - Verify form state preserved

3. **Modal Dialog Integration**
   - Open embedded form
   - Open Test Database Connection dialog
   - Close dialog
   - Verify embedded form still active

4. **Service Integration**
   - Verify embedded forms can access all required services
   - Verify data operations work correctly
   - Verify logging works from embedded forms

### Testing Configuration

- **Unit Test Framework**: xUnit 2.9.3
- **Property-Based Testing**: FsCheck 2.16.6 with FsCheck.Xunit 2.16.6
- **Mocking**: Moq 4.20.70 for service mocking
- **Test Iterations**: Minimum 100 iterations per property-based test
- **Coverage Target**: 80% code coverage for new components

## Implementation Notes

### Phase 1: Infrastructure

1. Create IEmbeddedForm interface
2. Implement EmbeddedFormHost class
3. Implement EmbeddedFormFactory class
4. Create WelcomeControl
5. Update FormMain to include content panel and host

### Phase 2: Form Conversion

1. Convert ConfigurationListForm to ConfigurationListControl
2. Convert ScheduleListForm to ScheduleListControl
3. Convert BackupMonitorForm to BackupMonitorControl
4. Convert LogBrowserForm to LogBrowserControl
5. Convert TransferLogViewerForm to TransferLogViewerControl

### Phase 3: Integration

1. Wire menu items to EmbeddedFormHost
2. Implement navigation state tracking
3. Implement title and status synchronization
4. Add keyboard shortcut handling
5. Test system tray integration

### Phase 4: Polish

1. Add smooth transitions between forms
2. Implement breadcrumb navigation
3. Add form-specific toolbar buttons (if needed)
4. Optimize layout performance
5. Add accessibility features

### Backward Compatibility

- Keep existing Form classes for potential future use
- Maintain existing service interfaces unchanged
- Ensure existing tests continue to pass
- Document migration path for any external dependencies

### Performance Considerations

- Lazy load embedded forms (create on first access)
- Cache created forms for quick switching (optional)
- Dispose forms when memory pressure is high
- Use async/await for form initialization
- Minimize layout recalculations during resize

### Accessibility

- Ensure keyboard navigation works correctly
- Maintain proper tab order within embedded forms
- Support screen readers with appropriate labels
- Provide keyboard shortcuts for common actions
- Ensure sufficient color contrast in navigation elements
