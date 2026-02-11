# Implementation Plan

- [x] 1. Create core infrastructure for embedded forms





  - Create IEmbeddedForm interface with lifecycle methods and events
  - Create EmbeddedFormHost class to manage form display and lifecycle
  - Create EmbeddedFormFactory for form instantiation
  - Create NavigationState and EmbeddedFormMetadata data models
  - Create EmbeddedFormErrorHandler for error handling
  - _Requirements: 1.1, 1.2, 1.3, 6.1, 6.2, 6.3_

- [ ]* 1.1 Write property test for Single Active Form Invariant
  - **Property 1: Single Active Form Invariant**
  - **Validates: Requirements 1.2, 1.4**

- [ ]* 1.2 Write property test for Service Provider Propagation
  - **Property 3: Service Provider Propagation**
  - **Validates: Requirements 3.5**

- [ ]* 1.3 Write unit tests for EmbeddedFormHost
  - Test showing a form updates CurrentForm property
  - Test showing welcome screen clears CurrentForm
  - Test switching between forms properly deactivates previous form
  - Test ActiveFormChanged event fires correctly
  - _Requirements: 1.2, 1.4_

- [ ]* 1.4 Write unit tests for EmbeddedFormFactory
  - Test factory creates correct control types
  - Test factory passes ServiceProvider correctly
  - Test factory handles creation errors gracefully
  - _Requirements: 6.1, 6.2_

- [x] 2. Create WelcomeControl as default view




  - Create WelcomeControl UserControl implementing IEmbeddedForm
  - Design welcome screen layout with application title and quick actions
  - Implement OnActivated and OnDeactivated lifecycle methods
  - Add welcome message and recent activity display
  - _Requirements: 1.1, 1.5, 2.3_

- [ ]* 2.1 Write unit tests for WelcomeControl
  - Test IEmbeddedForm interface implementation
  - Test lifecycle methods
  - Test Title and NavigationPath properties
  - _Requirements: 1.1, 2.3_

- [x] 3. Update FormMain to support embedded forms





  - Add ContentPanel to FormMain for hosting embedded controls
  - Add NavigationPanel for breadcrumb display
  - Initialize EmbeddedFormHost in FormMain constructor
  - Update FormMain layout to accommodate content panel
  - Wire EmbeddedFormHost events to update title bar and status bar
  - _Requirements: 1.3, 2.1, 2.2, 4.1, 7.1, 7.2_

- [ ]* 3.1 Write property test for Layout Responsiveness
  - **Property 4: Layout Responsiveness**
  - **Validates: Requirements 4.1, 4.2, 4.4**

- [ ]* 3.2 Write unit tests for FormMain embedded form integration
  - Test ContentPanel is created correctly
  - Test EmbeddedFormHost initialization
  - Test title bar updates when form changes
  - Test status bar updates when form status changes
  - _Requirements: 1.3, 2.1, 2.2_

- [x] 4. Convert ConfigurationListForm to ConfigurationListControl





  - Create ConfigurationListControl UserControl
  - Copy all fields, methods, and event handlers from ConfigurationListForm
  - Implement IEmbeddedForm interface
  - Replace DialogResult and Close() calls with CloseRequested event
  - Move Designer code to UserControl designer
  - Update layout to work within content panel
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 4.1, 4.2_

- [ ]* 4.1 Write property test for Form Lifecycle Consistency
  - **Property 2: Form Lifecycle Consistency**
  - **Validates: Requirements 3.1, 3.2**

- [ ]* 4.2 Write unit tests for ConfigurationListControl
  - Test IEmbeddedForm interface implementation
  - Test data loading and display
  - Test button click handlers
  - Test CloseRequested event firing
  - _Requirements: 3.1, 3.2, 3.3, 3.4_

- [x] 5. Convert ScheduleListForm to ScheduleListControl





  - Create ScheduleListControl UserControl
  - Copy all fields, methods, and event handlers from ScheduleListForm
  - Implement IEmbeddedForm interface
  - Replace DialogResult and Close() calls with CloseRequested event
  - Move Designer code to UserControl designer
  - Update layout to work within content panel
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 4.1, 4.2_

- [ ]* 5.1 Write unit tests for ScheduleListControl
  - Test IEmbeddedForm interface implementation
  - Test data loading and display
  - Test schedule management operations
  - Test CloseRequested event firing
  - _Requirements: 3.1, 3.2, 3.3, 3.4_

- [x] 6. Convert BackupMonitorForm to BackupMonitorControl






  - Create BackupMonitorControl UserControl
  - Copy all fields, methods, and event handlers from BackupMonitorForm
  - Implement IEmbeddedForm interface
  - Replace DialogResult and Close() calls with CloseRequested event
  - Move Designer code to UserControl designer
  - Update layout to work within content panel
  - Handle refresh timer lifecycle in OnActivated/OnDeactivated
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 4.1, 4.2_

- [ ]* 6.1 Write unit tests for BackupMonitorControl
  - Test IEmbeddedForm interface implementation
  - Test backup monitoring functionality
  - Test timer lifecycle management
  - Test CloseRequested event firing
  - _Requirements: 3.1, 3.2, 3.3, 3.4_

- [x] 7. Convert LogBrowserForm to LogBrowserControl


  - Create LogBrowserControl UserControl
  - Copy all fields, methods, and event handlers from LogBrowserForm
  - Implement IEmbeddedForm interface
  - Replace DialogResult and Close() calls with CloseRequested event
  - Move Designer code to UserControl designer
  - Update layout to work within content panel
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 4.1, 4.2_

- [ ]* 7.1 Write unit tests for LogBrowserControl
  - Test IEmbeddedForm interface implementation
  - Test log loading and filtering
  - Test CloseRequested event firing
  - _Requirements: 3.1, 3.2, 3.3, 3.4_

- [x] 8. Convert TransferLogViewerForm to TransferLogViewerControl




  - Create TransferLogViewerControl UserControl
  - Copy all fields, methods, and event handlers from TransferLogViewerForm
  - Implement IEmbeddedForm interface
  - Replace DialogResult and Close() calls with CloseRequested event
  - Move Designer code to UserControl designer
  - Update layout to work within content panel
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 4.1, 4.2_

- [ ]* 8.1 Write unit tests for TransferLogViewerControl
  - Test IEmbeddedForm interface implementation
  - Test transfer log display
  - Test CloseRequested event firing
  - _Requirements: 3.1, 3.2, 3.3, 3.4_

- [x] 9. Wire menu items to embedded form navigation




  - Update configurationToolStripMenuItem_Click to show ConfigurationListControl
  - Update scheduleManagementToolStripMenuItem_Click to show ScheduleListControl
  - Update backupMonitorToolStripMenuItem_Click to show BackupMonitorControl
  - Update logBrowserToolStripMenuItem_Click to show LogBrowserControl
  - Update transferLogViewToolStripMenuItem_Click to show TransferLogViewerControl
  - Ensure Test Database Connection continues to show modal dialog
  - _Requirements: 1.2, 5.1, 5.2, 5.3_

- [ ]* 9.1 Write property test for Modal Dialog Independence
  - **Property 10: Modal Dialog Independence**
  - **Validates: Requirements 5.1, 5.2, 5.3**

- [ ]* 9.2 Write integration tests for menu navigation
  - Test each menu item navigates to correct embedded form
  - Test Test Database Connection shows modal dialog
  - Test modal dialog doesn't affect embedded form state
  - _Requirements: 1.2, 5.1, 5.2, 5.3_

- [x] 10. Implement navigation state tracking and breadcrumb display




  - Create NavigationPanel control for breadcrumb display
  - Update EmbeddedFormHost to track navigation history
  - Implement breadcrumb rendering based on NavigationPath
  - Add visual styling for breadcrumb navigation
  - _Requirements: 7.1, 7.2, 7.3, 7.4_

- [ ]* 10.1 Write unit tests for navigation state tracking
  - Test navigation history is maintained correctly
  - Test breadcrumb display updates correctly
  - Test NavigationPath formatting
  - _Requirements: 7.1, 7.2, 7.3_

- [x] 11. Implement title and status synchronization





  - Wire IEmbeddedForm.TitleChanged event to update FormMain title bar
  - Wire IEmbeddedForm.StatusChanged event to update FormMain status bar
  - Implement title format: "MySQL Backup Tool - [Form Title]"
  - Handle title updates during form transitions
  - _Requirements: 2.1, 2.2, 2.3, 2.4_

- [ ]* 11.1 Write property test for Title Synchronization
  - **Property 7: Title Synchronization**
  - **Validates: Requirements 2.1**

- [ ]* 11.2 Write property test for Status Message Propagation
  - **Property 8: Status Message Propagation**
  - **Validates: Requirements 2.2**

- [ ]* 11.3 Write unit tests for title and status synchronization
  - Test title bar updates when form title changes
  - Test status bar updates when form status changes
  - Test title format is correct
  - _Requirements: 2.1, 2.2, 2.3_

- [x] 12. Implement keyboard shortcut handling




  - Add KeyPreview handling in FormMain for global shortcuts
  - Implement Escape key to return to welcome screen
  - Forward keyboard events to active embedded form
  - Ensure menu shortcuts work regardless of active form
  - _Requirements: 8.1, 8.2, 8.3, 8.4_

- [ ]* 12.1 Write property test for Keyboard Shortcut Isolation
  - **Property 9: Keyboard Shortcut Isolation**
  - **Validates: Requirements 8.1, 8.2**

- [ ]* 12.2 Write unit tests for keyboard shortcut handling
  - Test Escape key returns to welcome screen
  - Test menu shortcuts work with embedded forms
  - Test form-specific shortcuts only work when form is active
  - _Requirements: 8.1, 8.2, 8.3, 8.4_

- [x] 13. Implement system tray integration with embedded forms




  - Update HideToSystemTray to preserve current embedded form state
  - Update ShowMainWindow to restore previous embedded form
  - Store navigation state before hiding to tray
  - Restore navigation state when showing from tray
  - _Requirements: 9.1, 9.2, 9.3_

- [ ]* 13.1 Write property test for Navigation State Preservation
  - **Property 5: Navigation State Preservation**
  - **Validates: Requirements 9.1, 9.2**

- [ ]* 13.2 Write unit tests for system tray integration
  - Test form state is preserved when minimizing to tray
  - Test form state is restored when showing from tray
  - Test disposal works correctly when closing from tray
  - _Requirements: 9.1, 9.2, 9.3_

- [ ] 14. Implement resource cleanup and memory management
  - Implement proper disposal in EmbeddedFormHost
  - Unregister event handlers when forms are deactivated
  - Add memory pressure monitoring for form caching
  - Implement form disposal when switching between forms
  - _Requirements: 6.3, 6.4_

- [ ]* 14.1 Write property test for Event Handler Cleanup
  - **Property 6: Event Handler Cleanup**
  - **Validates: Requirements 6.4**

- [ ]* 14.2 Write unit tests for resource cleanup
  - Test event handlers are unregistered on deactivation
  - Test forms are disposed correctly
  - Test no memory leaks from form switching
  - _Requirements: 6.3, 6.4_

- [ ] 15. Add smooth transitions and visual polish
  - Implement fade-in/fade-out transitions between forms
  - Add loading indicator for slow-loading forms
  - Optimize layout performance during resize
  - Add visual feedback for form activation
  - Ensure consistent styling across all embedded forms
  - _Requirements: 2.4, 4.4_

- [ ]* 15.1 Write unit tests for visual transitions
  - Test transitions complete without errors
  - Test loading indicators display correctly
  - Test layout performance is acceptable
  - _Requirements: 2.4, 4.4_

- [ ] 16. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ]* 17. Write integration tests for full workflows
  - Test complete navigation through all tools
  - Test system tray integration with embedded forms
  - Test modal dialog integration
  - Test service integration from embedded forms
  - _Requirements: All requirements_
