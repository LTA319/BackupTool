# MySQL Backup Tool - API Reference

## Overview

The MySQL Backup Tool provides a comprehensive set of APIs for managing MySQL database backups through a distributed client-server architecture. This document covers all public interfaces, models, and services available in the system.

## Table of Contents

1. [Core Services](#core-services)
2. [Backup Management](#backup-management)
3. [File Operations](#file-operations)
4. [Notification System](#notification-system)
5. [Scheduling](#scheduling)
6. [Security](#security)
7. [Data Models](#data-models)
8. [Error Handling](#error-handling)

---

## Core Services

### IMySQLManager

Manages MySQL instance lifecycle operations including starting, stopping, and verifying database connections.

#### Methods

##### `StopInstanceAsync(string serviceName)`
Stops a MySQL service instance.

**Parameters:**
- `serviceName` (string): Name of the MySQL service to stop

**Returns:** `Task<bool>` - True if the service was successfully stopped

**Example:**
```csharp
var mysqlManager = serviceProvider.GetService<IMySQLManager>();
bool stopped = await mysqlManager.StopInstanceAsync("MySQL80");
```

##### `StartInstanceAsync(string serviceName)`
Starts a MySQL service instance.

**Parameters:**
- `serviceName` (string): Name of the MySQL service to start

**Returns:** `Task<bool>` - True if the service was successfully started

##### `VerifyInstanceAvailabilityAsync(MySQLConnectionInfo connection)`
Verifies that a MySQL instance is available and accepting connections.

**Parameters:**
- `connection` (MySQLConnectionInfo): Connection information for the MySQL instance

**Returns:** `Task<bool>` - True if the instance is available

##### `VerifyInstanceAvailabilityAsync(MySQLConnectionInfo connection, int timeoutSeconds)`
Verifies MySQL instance availability with configurable timeout.

**Parameters:**
- `connection` (MySQLConnectionInfo): Connection information
- `timeoutSeconds` (int): Connection timeout in seconds

**Returns:** `Task<bool>` - True if the instance is available

---

### ICompressionService

Handles file compression operations for backup files.

#### Methods

##### `CompressDirectoryAsync(string sourcePath, string targetPath, IProgress<CompressionProgress>? progress = null)`
Compresses a directory into a zip file.

**Parameters:**
- `sourcePath` (string): Path to the directory to compress
- `targetPath` (string): Path where the compressed file should be created
- `progress` (IProgress<CompressionProgress>?, optional): Progress reporter

**Returns:** `Task<string>` - Path to the created compressed file

**Example:**
```csharp
var compressionService = serviceProvider.GetService<ICompressionService>();
var progress = new Progress<CompressionProgress>(p => 
    Console.WriteLine($"Compression: {p.PercentComplete:F1}%"));

string compressedFile = await compressionService.CompressDirectoryAsync(
    @"C:\MySQL\Data", 
    @"C:\Backups\backup.zip", 
    progress);
```

##### `CleanupAsync(string filePath)`
Cleans up temporary files created during compression.

**Parameters:**
- `filePath` (string): Path to the file to clean up

**Returns:** `Task`

---

### IFileTransferService

Manages file transfer operations between client and server.

#### Methods

##### `TransferFileAsync(string filePath, TransferConfig config, CancellationToken cancellationToken = default)`
Transfers a file to a remote server.

**Parameters:**
- `filePath` (string): Path to the file to transfer
- `config` (TransferConfig): Transfer configuration settings
- `cancellationToken` (CancellationToken, optional): Cancellation token

**Returns:** `Task<TransferResult>` - Result of the transfer operation

##### `ResumeTransferAsync(string resumeToken, CancellationToken cancellationToken = default)`
Resumes an interrupted file transfer.

**Parameters:**
- `resumeToken` (string): Token identifying the interrupted transfer
- `cancellationToken` (CancellationToken, optional): Cancellation token

**Returns:** `Task<TransferResult>` - Result of the resumed transfer

##### `ResumeTransferAsync(string resumeToken, string filePath, TransferConfig config, CancellationToken cancellationToken = default)`
Resumes an interrupted file transfer with full context.

**Parameters:**
- `resumeToken` (string): Token identifying the interrupted transfer
- `filePath` (string): Path to the file to transfer
- `config` (TransferConfig): Transfer configuration settings
- `cancellationToken` (CancellationToken, optional): Cancellation token

**Returns:** `Task<TransferResult>` - Result of the resumed transfer

---

## Backup Management

### IBackupScheduler

Manages backup scheduling with cron expression support.

#### Methods

##### `StartAsync(CancellationToken cancellationToken = default)`
Starts the backup scheduler service.

##### `StopAsync(CancellationToken cancellationToken = default)`
Stops the backup scheduler service.

##### `AddOrUpdateScheduleAsync(ScheduleConfiguration scheduleConfig)`
Adds or updates a schedule configuration.

**Parameters:**
- `scheduleConfig` (ScheduleConfiguration): Schedule configuration to add or update

**Returns:** `Task<ScheduleConfiguration>` - The saved schedule configuration

##### `RemoveScheduleAsync(int scheduleId)`
Removes a schedule configuration.

**Parameters:**
- `scheduleId` (int): ID of the schedule to remove

##### `GetAllSchedulesAsync()`
Gets all schedule configurations.

**Returns:** `Task<IEnumerable<ScheduleConfiguration>>` - List of all schedules

##### `GetSchedulesForBackupConfigAsync(int backupConfigId)`
Gets schedule configurations for a specific backup configuration.

**Parameters:**
- `backupConfigId` (int): Backup configuration ID

**Returns:** `Task<List<ScheduleConfiguration>>` - List of schedule configurations

##### `SetScheduleEnabledAsync(int scheduleId, bool enabled)`
Enables or disables a schedule.

**Parameters:**
- `scheduleId` (int): Schedule ID
- `enabled` (bool): Whether to enable or disable the schedule

##### `GetNextScheduledTimeAsync()`
Gets the next scheduled backup time across all schedules.

**Returns:** `Task<DateTime?>` - The next scheduled backup time, or null if no schedules are enabled

##### `TriggerScheduledBackupAsync(int scheduleId)`
Manually triggers a scheduled backup.

**Parameters:**
- `scheduleId` (int): Schedule ID to trigger

##### `ValidateScheduleAsync(ScheduleConfiguration scheduleConfig)`
Validates a schedule configuration.

**Parameters:**
- `scheduleConfig` (ScheduleConfiguration): Schedule configuration to validate

**Returns:** `Task<(bool IsValid, List<string> Errors)>` - Validation result

---

### IRetentionPolicyService

Manages backup retention policies and automated cleanup.

#### Methods

##### `ExecuteRetentionPoliciesAsync()`
Executes all enabled retention policies.

**Returns:** `Task<RetentionExecutionResult>` - Execution result

##### `ApplyRetentionPolicyAsync(RetentionPolicy policy)`
Applies a specific retention policy.

**Parameters:**
- `policy` (RetentionPolicy): The retention policy to apply

**Returns:** `Task<RetentionExecutionResult>` - Execution result

##### `CreateRetentionPolicyAsync(RetentionPolicy policy)`
Creates a new retention policy with validation.

**Parameters:**
- `policy` (RetentionPolicy): The retention policy to create

**Returns:** `Task<RetentionPolicy>` - The created policy

##### `UpdateRetentionPolicyAsync(RetentionPolicy policy)`
Updates an existing retention policy.

**Parameters:**
- `policy` (RetentionPolicy): The retention policy to update

**Returns:** `Task<RetentionPolicy>` - The updated policy

##### `DeleteRetentionPolicyAsync(int policyId)`
Deletes a retention policy.

**Parameters:**
- `policyId` (int): ID of the policy to delete

**Returns:** `Task<bool>` - True if deletion was successful

##### `GetAllRetentionPoliciesAsync()`
Gets all retention policies.

**Returns:** `Task<IEnumerable<RetentionPolicy>>` - All retention policies

##### `GetEnabledRetentionPoliciesAsync()`
Gets all enabled retention policies.

**Returns:** `Task<IEnumerable<RetentionPolicy>>` - Enabled retention policies

##### `GetRetentionPolicyByIdAsync(int policyId)`
Gets a retention policy by ID.

**Parameters:**
- `policyId` (int): Policy ID

**Returns:** `Task<RetentionPolicy?>` - The policy, or null if not found

##### `GetRetentionPolicyByNameAsync(string name)`
Gets a retention policy by name.

**Parameters:**
- `name` (string): Policy name

**Returns:** `Task<RetentionPolicy?>` - The policy, or null if not found

##### `EnableRetentionPolicyAsync(int policyId)`
Enables a retention policy.

**Parameters:**
- `policyId` (int): Policy ID

**Returns:** `Task<bool>` - True if successful

##### `DisableRetentionPolicyAsync(int policyId)`
Disables a retention policy.

**Parameters:**
- `policyId` (int): Policy ID

**Returns:** `Task<bool>` - True if successful

##### `GetRetentionPolicyRecommendationsAsync()`
Gets retention policy recommendations based on current backup patterns.

**Returns:** `Task<List<RetentionPolicy>>` - Recommended policies

##### `ValidateRetentionPolicyAsync(RetentionPolicy policy)`
Validates a retention policy configuration.

**Parameters:**
- `policy` (RetentionPolicy): Policy to validate

**Returns:** `Task<(bool IsValid, List<string> Errors)>` - Validation result

##### `EstimateRetentionImpactAsync(RetentionPolicy policy)`
Estimates the impact of applying a retention policy.

**Parameters:**
- `policy` (RetentionPolicy): Policy to analyze

**Returns:** `Task<RetentionImpactEstimate>` - Impact estimate

---

## File Operations

### IEncryptionService

Provides file encryption and decryption capabilities using AES-256.

#### Methods

##### `EncryptAsync(string inputPath, string outputPath, string password, CancellationToken cancellationToken = default)`
Encrypts a file using AES-256 encryption.

**Parameters:**
- `inputPath` (string): Path to the file to encrypt
- `outputPath` (string): Path where the encrypted file will be saved
- `password` (string): Password for encryption
- `cancellationToken` (CancellationToken, optional): Cancellation token

**Returns:** `Task<EncryptionMetadata>` - Encryption metadata

**Example:**
```csharp
var encryptionService = serviceProvider.GetService<IEncryptionService>();
var metadata = await encryptionService.EncryptAsync(
    @"C:\Backups\backup.zip",
    @"C:\Backups\backup.zip.enc",
    "SecurePassword123!");
```

##### `DecryptAsync(string inputPath, string outputPath, string password, CancellationToken cancellationToken = default)`
Decrypts a file that was encrypted with AES-256.

**Parameters:**
- `inputPath` (string): Path to the encrypted file
- `outputPath` (string): Path where the decrypted file will be saved
- `password` (string): Password for decryption
- `cancellationToken` (CancellationToken, optional): Cancellation token

**Returns:** `Task` - Async operation

##### `ValidatePasswordAsync(string encryptedFilePath, string password)`
Validates if the provided password can decrypt the encrypted file.

**Parameters:**
- `encryptedFilePath` (string): Path to the encrypted file
- `password` (string): Password to validate

**Returns:** `Task<bool>` - True if password is correct

##### `GetMetadataAsync(string encryptedFilePath)`
Gets metadata from an encrypted file.

**Parameters:**
- `encryptedFilePath` (string): Path to the encrypted file

**Returns:** `Task<EncryptionMetadata>` - Encryption metadata

##### `GenerateSecurePassword(int length = 32)`
Generates a secure random password.

**Parameters:**
- `length` (int, optional): Length of the password (default: 32)

**Returns:** `string` - Secure random password

---

### IValidationService

Provides backup file validation and integrity checking.

#### Methods

##### `ValidateBackupAsync(string filePath, CancellationToken cancellationToken = default)`
Validates a backup file for integrity and completeness.

**Parameters:**
- `filePath` (string): Path to the backup file to validate
- `cancellationToken` (CancellationToken, optional): Cancellation token

**Returns:** `Task<FileValidationResult>` - Validation result with details

##### `ValidateIntegrityAsync(string filePath, string expectedChecksum, ChecksumAlgorithm algorithm = ChecksumAlgorithm.SHA256)`
Validates file integrity using checksum comparison.

**Parameters:**
- `filePath` (string): Path to the file to validate
- `expectedChecksum` (string): Expected checksum value
- `algorithm` (ChecksumAlgorithm, optional): Checksum algorithm to use

**Returns:** `Task<bool>` - True if checksums match

##### `CalculateChecksumAsync(string filePath, ChecksumAlgorithm algorithm = ChecksumAlgorithm.SHA256, CancellationToken cancellationToken = default)`
Calculates checksum for a file.

**Parameters:**
- `filePath` (string): Path to the file
- `algorithm` (ChecksumAlgorithm, optional): Checksum algorithm to use
- `cancellationToken` (CancellationToken, optional): Cancellation token

**Returns:** `Task<string>` - Calculated checksum as hex string

##### `GenerateReportAsync(string filePath, CancellationToken cancellationToken = default)`
Generates a comprehensive validation report for a backup file.

**Parameters:**
- `filePath` (string): Path to the backup file
- `cancellationToken` (CancellationToken, optional): Cancellation token

**Returns:** `Task<ValidationReport>` - Detailed validation report

##### `ValidateCompressionAsync(string filePath, CancellationToken cancellationToken = default)`
Validates that a backup file can be successfully decompressed.

**Parameters:**
- `filePath` (string): Path to the compressed backup file
- `cancellationToken` (CancellationToken, optional): Cancellation token

**Returns:** `Task<bool>` - True if file can be decompressed

##### `ValidateEncryptionAsync(string filePath, string password, CancellationToken cancellationToken = default)`
Validates that an encrypted backup file can be decrypted with the provided password.

**Parameters:**
- `filePath` (string): Path to the encrypted backup file
- `password` (string): Password for decryption
- `cancellationToken` (CancellationToken, optional): Cancellation token

**Returns:** `Task<bool>` - True if file can be decrypted

---

## Notification System

### INotificationService

Provides email notification capabilities with SMTP support.

#### Properties

##### `Configuration`
Gets the current SMTP configuration.

**Type:** `SmtpConfig`

#### Methods

##### `SendEmailAsync(EmailMessage message, CancellationToken cancellationToken = default)`
Sends a single email message asynchronously.

**Parameters:**
- `message` (EmailMessage): The email message to send
- `cancellationToken` (CancellationToken, optional): Cancellation token

**Returns:** `Task<bool>` - True if the email was sent successfully

**Exceptions:**
- `ArgumentNullException`: Thrown when message is null
- `InvalidOperationException`: Thrown when SMTP configuration is invalid

**Example:**
```csharp
var notificationService = serviceProvider.GetService<INotificationService>();
var message = new EmailMessage
{
    To = "admin@example.com",
    Subject = "Backup Completed Successfully",
    Body = "The MySQL backup completed successfully at " + DateTime.Now,
    IsHtml = false
};

bool sent = await notificationService.SendEmailAsync(message);
```

##### `SendBulkEmailAsync(IEnumerable<EmailMessage> messages, CancellationToken cancellationToken = default)`
Sends multiple email messages in bulk.

**Parameters:**
- `messages` (IEnumerable<EmailMessage>): Collection of email messages to send
- `cancellationToken` (CancellationToken, optional): Cancellation token

**Returns:** `Task<Dictionary<string, bool>>` - Dictionary mapping message IDs to their send success status

##### `GetStatusAsync(string notificationId, CancellationToken cancellationToken = default)`
Gets the delivery status of a specific notification.

**Parameters:**
- `notificationId` (string): Unique identifier of the notification
- `cancellationToken` (CancellationToken, optional): Cancellation token

**Returns:** `Task<NotificationStatus?>` - The current status, or null if not found

##### `GetTemplatesAsync(CancellationToken cancellationToken = default)`
Retrieves all available email templates.

**Returns:** `Task<IEnumerable<EmailTemplate>>` - Collection of available email templates

##### `GetTemplatesByCategoryAsync(string category, CancellationToken cancellationToken = default)`
Retrieves email templates by category.

**Parameters:**
- `category` (string): Template category to filter by

**Returns:** `Task<IEnumerable<EmailTemplate>>` - Collection of email templates in the specified category

##### `GetTemplateAsync(string templateName, CancellationToken cancellationToken = default)`
Gets a specific email template by name.

**Parameters:**
- `templateName` (string): Name of the template to retrieve

**Returns:** `Task<EmailTemplate?>` - The email template, or null if not found

##### `TestSmtpConnectionAsync(SmtpConfig config, CancellationToken cancellationToken = default)`
Tests the SMTP connection with the provided configuration.

**Parameters:**
- `config` (SmtpConfig): SMTP configuration to test

**Returns:** `Task<bool>` - True if the connection test succeeds

##### `CreateEmailFromTemplateAsync(string templateName, string recipient, Dictionary<string, object> variables, CancellationToken cancellationToken = default)`
Creates an email message from a template with variable substitution.

**Parameters:**
- `templateName` (string): Name of the template to use
- `recipient` (string): Recipient email address
- `variables` (Dictionary<string, object>): Dictionary of variables to substitute in the template

**Returns:** `Task<EmailMessage?>` - The created email message, or null if template not found

##### `UpdateConfiguration(SmtpConfig config)`
Updates the SMTP configuration.

**Parameters:**
- `config` (SmtpConfig): New SMTP configuration

##### `GetStatisticsAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)`
Gets statistics about notification delivery.

**Parameters:**
- `fromDate` (DateTime?, optional): Start date for statistics
- `toDate` (DateTime?, optional): End date for statistics
- `cancellationToken` (CancellationToken, optional): Cancellation token

**Returns:** `Task<NotificationStatistics>` - Notification delivery statistics

---

## Scheduling

### Schedule Configuration

Backup schedules use cron expressions for flexible timing configuration.

#### Cron Expression Format

The system supports standard cron expressions with the following format:
```
* * * * * *
│ │ │ │ │ │
│ │ │ │ │ └─── Day of Week (0-7, Sunday = 0 or 7)
│ │ │ │ └───── Month (1-12)
│ │ │ └─────── Day of Month (1-31)
│ │ └───────── Hour (0-23)
│ └─────────── Minute (0-59)
└───────────── Second (0-59)
```

#### Common Cron Examples

- `0 0 2 * * *` - Daily at 2:00 AM
- `0 0 2 * * 0` - Weekly on Sunday at 2:00 AM
- `0 0 2 1 * *` - Monthly on the 1st at 2:00 AM
- `0 */30 * * * *` - Every 30 minutes
- `0 0 */6 * * *` - Every 6 hours

---

## Security

### Authentication and Authorization

The system provides several security-related interfaces:

#### IAuthenticationService
Handles user authentication and session management.

#### IEncryptionService
Provides AES-256 encryption for backup files with the following security features:

- **Key Derivation:** PBKDF2 with configurable iterations (default: 100,000)
- **Salt Generation:** Cryptographically secure random salt for each encryption
- **IV Generation:** Unique initialization vector for each encryption
- **Secure Memory:** Proper cleanup of sensitive data in memory

#### Security Best Practices

1. **Password Strength:** Use strong passwords for encryption (minimum 12 characters)
2. **Key Storage:** Store encryption passwords securely, never in plain text
3. **Network Security:** Use TLS for all network communications
4. **File Permissions:** Ensure backup files have appropriate access restrictions
5. **Audit Logging:** Enable comprehensive logging for security events

---

## Data Models

### Core Configuration Models

#### BackupConfiguration
Represents a complete backup job configuration.

**Key Properties:**
- `Id` (int): Unique identifier
- `Name` (string): Configuration name (1-100 characters, alphanumeric + spaces/hyphens/underscores)
- `MySQLConnection` (MySQLConnectionInfo): Database connection details
- `DataDirectoryPath` (string): Path to MySQL data directory
- `ServiceName` (string): MySQL service name
- `TargetServer` (ServerEndpoint): Destination server information
- `TargetDirectory` (string): Backup destination directory
- `NamingStrategy` (FileNamingStrategy): File naming configuration
- `IsActive` (bool): Whether the configuration is active
- `CreatedAt` (DateTime): Creation timestamp

**Validation:**
- Implements `IValidatableObject` for comprehensive validation
- Validates directory paths and accessibility
- Checks connection parameters
- Ensures naming strategy validity

#### MySQLConnectionInfo
Database connection configuration.

**Key Properties:**
- `Server` (string): MySQL server hostname/IP
- `Port` (int): MySQL server port (default: 3306)
- `Username` (string): Database username
- `Password` (string): Database password (encrypted storage recommended)
- `Database` (string): Target database name
- `ConnectionTimeout` (int): Connection timeout in seconds

#### ServerEndpoint
Target server configuration for backup storage.

**Key Properties:**
- `IPAddress` (string): Server IP address
- `Port` (int): Server port
- `Protocol` (string): Transfer protocol (TCP, HTTP, etc.)
- `AuthenticationRequired` (bool): Whether authentication is required
- `Credentials` (NetworkCredential): Authentication credentials

### Notification Models

#### EmailMessage
Represents an email notification.

**Key Properties:**
- `Id` (string): Unique message identifier
- `To` (string): Recipient email address
- `Subject` (string): Email subject (max 200 characters)
- `Body` (string): Email body content (max 10,000 characters)
- `IsHtml` (bool): Whether body is HTML formatted
- `Attachments` (List<string>): File attachment paths
- `Headers` (Dictionary<string, string>): Custom email headers
- `Priority` (EmailPriority): Email priority level
- `CreatedAt` (DateTime): Creation timestamp

#### SmtpConfig
SMTP server configuration.

**Key Properties:**
- `Host` (string): SMTP server hostname
- `Port` (int): SMTP server port (default: 587)
- `EnableSsl` (bool): Whether to use SSL/TLS
- `Username` (string): SMTP authentication username
- `Password` (string): SMTP authentication password
- `FromAddress` (string): Sender email address
- `FromName` (string): Sender display name
- `TimeoutSeconds` (int): Connection timeout (5-300 seconds)

#### EmailTemplate
Email template for notifications.

**Key Properties:**
- `Name` (string): Template identifier
- `Description` (string): Template description
- `Subject` (string): Email subject template
- `HtmlBody` (string): HTML body template
- `TextBody` (string): Plain text body template
- `RequiredVariables` (List<string>): Required template variables
- `Category` (string): Template category
- `IsActive` (bool): Whether template is active

### Encryption Models

#### EncryptionMetadata
Metadata for encrypted files.

**Key Properties:**
- `Algorithm` (string): Encryption algorithm (default: "AES-256-CBC")
- `KeyDerivation` (string): Key derivation function (default: "PBKDF2")
- `Iterations` (int): PBKDF2 iterations (default: 100,000)
- `Salt` (string): Base64-encoded salt
- `IV` (string): Base64-encoded initialization vector
- `EncryptedAt` (DateTime): Encryption timestamp
- `OriginalSize` (long): Original file size
- `OriginalChecksum` (string): SHA256 checksum of original file
- `Version` (int): Encryption format version

#### EncryptionConfig
Configuration for encryption operations.

**Key Properties:**
- `Password` (string): Encryption password
- `KeySize` (int): Key size in bits (default: 256)
- `Iterations` (int): PBKDF2 iterations (default: 100,000)
- `SecureDelete` (bool): Whether to securely delete original file
- `BufferSize` (int): Streaming buffer size (default: 64KB)
- `CompressBeforeEncryption` (bool): Whether to compress before encrypting

### Progress and Status Models

#### CompressionProgress
Progress information for compression operations.

**Key Properties:**
- `PercentComplete` (double): Completion percentage (0-100)
- `BytesProcessed` (long): Bytes processed so far
- `TotalBytes` (long): Total bytes to process
- `CurrentFile` (string): Currently processing file
- `EstimatedTimeRemaining` (TimeSpan?): Estimated time remaining

#### TransferResult
Result of a file transfer operation.

**Key Properties:**
- `Success` (bool): Whether transfer was successful
- `BytesTransferred` (long): Number of bytes transferred
- `Duration` (TimeSpan): Transfer duration
- `AverageSpeed` (double): Average transfer speed (bytes/second)
- `ResumeToken` (string): Token for resuming interrupted transfers
- `ErrorMessage` (string): Error message if transfer failed

---

## Error Handling

### Exception Hierarchy

The system uses a structured exception hierarchy for comprehensive error handling:

#### BackupException
Base exception class for all backup-related errors.

**Properties:**
- `OperationId` (string): Unique identifier for the operation
- `Timestamp` (DateTime): When the error occurred
- `Context` (Dictionary<string, object>): Additional error context

#### NetworkRetryException
Thrown when network retry attempts are exhausted.

**Properties:**
- `OperationName` (string): Name of the failed operation
- `AttemptsExhausted` (int): Number of retry attempts made
- `TotalDuration` (TimeSpan): Total time spent retrying

#### ValidationException
Thrown when validation fails.

**Properties:**
- `ValidationErrors` (List<string>): List of validation error messages
- `PropertyName` (string): Name of the property that failed validation

### Error Codes

The system uses standardized error codes for consistent error handling:

- `MYSQL_001`: MySQL service start/stop failure
- `MYSQL_002`: MySQL connection failure
- `COMPRESS_001`: Compression operation failure
- `TRANSFER_001`: File transfer failure
- `ENCRYPT_001`: Encryption operation failure
- `VALIDATE_001`: File validation failure
- `NOTIFY_001`: Notification delivery failure
- `SCHEDULE_001`: Schedule configuration error
- `RETENTION_001`: Retention policy execution error

### Logging

All operations are logged with structured logging using Serilog:

```csharp
// Example log entry structure
{
  "Timestamp": "2024-01-25T10:30:00.000Z",
  "Level": "Information",
  "MessageTemplate": "Backup operation {OperationId} completed successfully",
  "Properties": {
    "OperationId": "backup-20240125-103000",
    "ConfigurationId": 1,
    "Duration": "00:05:30",
    "BytesProcessed": 1073741824,
    "CompressionRatio": 0.65
  }
}
```

---

## Usage Examples

### Complete Backup Workflow

```csharp
// Configure services
var services = new ServiceCollection();
services.AddMySqlBackupTool();
var serviceProvider = services.BuildServiceProvider();

// Get required services
var mysqlManager = serviceProvider.GetService<IMySQLManager>();
var compressionService = serviceProvider.GetService<ICompressionService>();
var encryptionService = serviceProvider.GetService<IEncryptionService>();
var transferService = serviceProvider.GetService<IFileTransferService>();
var notificationService = serviceProvider.GetService<INotificationService>();

try
{
    // 1. Stop MySQL service
    await mysqlManager.StopInstanceAsync("MySQL80");
    
    // 2. Compress data directory
    var compressedFile = await compressionService.CompressDirectoryAsync(
        @"C:\MySQL\Data", 
        @"C:\Temp\backup.zip");
    
    // 3. Encrypt backup file
    var encryptedFile = @"C:\Temp\backup.zip.enc";
    await encryptionService.EncryptAsync(compressedFile, encryptedFile, "SecurePassword123!");
    
    // 4. Transfer to server
    var transferConfig = new TransferConfig
    {
        TargetServer = new ServerEndpoint { IPAddress = "192.168.1.100", Port = 8080 },
        TargetDirectory = "/backups",
        EnableResume = true
    };
    
    var transferResult = await transferService.TransferFileAsync(encryptedFile, transferConfig);
    
    // 5. Send notification
    var notification = new EmailMessage
    {
        To = "admin@example.com",
        Subject = "Backup Completed Successfully",
        Body = $"Backup completed. {transferResult.BytesTransferred} bytes transferred.",
        IsHtml = false
    };
    
    await notificationService.SendEmailAsync(notification);
    
    // 6. Cleanup temporary files
    await compressionService.CleanupAsync(compressedFile);
    File.Delete(encryptedFile);
}
finally
{
    // 7. Restart MySQL service
    await mysqlManager.StartInstanceAsync("MySQL80");
}
```

### Schedule Configuration

```csharp
var scheduler = serviceProvider.GetService<IBackupScheduler>();

// Create daily backup schedule
var schedule = new ScheduleConfiguration
{
    Name = "Daily Backup",
    CronExpression = "0 0 2 * * *", // Daily at 2:00 AM
    BackupConfigurationId = 1,
    IsEnabled = true,
    Description = "Daily backup at 2 AM"
};

await scheduler.AddOrUpdateScheduleAsync(schedule);
await scheduler.StartAsync();
```

### Retention Policy Setup

```csharp
var retentionService = serviceProvider.GetService<IRetentionPolicyService>();

// Create retention policy: keep daily backups for 30 days
var policy = new RetentionPolicy
{
    Name = "30-Day Retention",
    MaxAge = TimeSpan.FromDays(30),
    MaxCount = 30,
    BackupDirectory = @"C:\Backups",
    IsEnabled = true
};

await retentionService.CreateRetentionPolicyAsync(policy);

// Execute retention policies
var result = await retentionService.ExecuteRetentionPoliciesAsync();
Console.WriteLine($"Deleted {result.FilesDeleted} old backup files");
```

---

## Configuration

### Dependency Injection Setup

```csharp
// Program.cs or Startup.cs
services.AddMySqlBackupTool(options =>
{
    options.ConnectionString = "Data Source=backup.db";
    options.EnableEncryption = true;
    options.EnableNotifications = true;
    options.DefaultCompressionLevel = CompressionLevel.Optimal;
});

// Configure SMTP for notifications
services.Configure<SmtpConfig>(options =>
{
    options.Host = "smtp.gmail.com";
    options.Port = 587;
    options.EnableSsl = true;
    options.Username = "backup@example.com";
    options.Password = "app-password";
    options.FromAddress = "backup@example.com";
    options.FromName = "MySQL Backup Tool";
});
```

### Configuration File (appsettings.json)

```json
{
  "MySqlBackupTool": {
    "ConnectionString": "Data Source=backup.db",
    "EnableEncryption": true,
    "EnableNotifications": true,
    "DefaultCompressionLevel": "Optimal",
    "MaxConcurrentBackups": 3,
    "DefaultRetentionDays": 30
  },
  "SmtpConfig": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "EnableSsl": true,
    "Username": "backup@example.com",
    "Password": "app-password",
    "FromAddress": "backup@example.com",
    "FromName": "MySQL Backup Tool",
    "TimeoutSeconds": 30
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "MySqlBackupTool": "Debug"
    }
  }
}
```

---

This API reference provides comprehensive documentation for all public interfaces and models in the MySQL Backup Tool. For implementation examples and advanced usage scenarios, refer to the integration tests and sample applications included with the project.