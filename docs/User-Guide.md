# MySQL Backup Tool - User Guide

## Overview

The MySQL Backup Tool is a comprehensive backup solution designed for enterprise MySQL environments. It provides automated backup scheduling, encryption, compression, and distributed storage capabilities through a client-server architecture.

## Table of Contents

1. [Getting Started](#getting-started)
2. [Configuration](#configuration)
3. [Basic Operations](#basic-operations)
4. [Advanced Features](#advanced-features)
5. [Monitoring and Troubleshooting](#monitoring-and-troubleshooting)
6. [Best Practices](#best-practices)

---

## Getting Started

### System Requirements

**Client Requirements:**
- Windows 10 or later
- .NET 8.0 Runtime
- MySQL Server (any supported version)
- Minimum 4GB RAM
- Sufficient disk space for temporary backup files

**Server Requirements:**
- Windows Server 2019 or later / Linux (Ubuntu 20.04+)
- .NET 8.0 Runtime
- Network connectivity to client machines
- Adequate storage space for backup files

### Installation

#### Client Installation

1. Download the latest release from the releases page
2. Extract the files to your desired installation directory
3. Run `MySqlBackupTool.Client.exe` as Administrator (required for MySQL service management)

#### Server Installation

1. Download the server package
2. Extract to your server directory
3. Configure the server settings in `appsettings.json`
4. Run `MySqlBackupTool.Server.exe` or install as a Windows Service

### Initial Setup

1. **Start the Server**: Launch the server application on your backup storage machine
2. **Configure Client**: Open the client application and configure your first backup job
3. **Test Connection**: Verify connectivity between client and server
4. **Run Test Backup**: Execute a test backup to ensure everything works correctly

---

## Configuration

### Backup Configuration

#### Creating a New Backup Configuration

1. Open the MySQL Backup Tool Client
2. Click "New Configuration"
3. Fill in the required fields:

**Basic Settings:**
- **Configuration Name**: Unique identifier for this backup job
- **MySQL Service Name**: Windows service name (e.g., "MySQL80")
- **Data Directory**: Path to MySQL data directory (e.g., `C:\ProgramData\MySQL\MySQL Server 8.0\Data`)

**MySQL Connection:**
- **Server**: MySQL server hostname or IP
- **Port**: MySQL port (default: 3306)
- **Username**: MySQL username with backup privileges
- **Password**: MySQL password (stored encrypted)
- **Database**: Target database name (optional, leave blank for all databases)

**Target Server:**
- **IP Address**: Backup server IP address
- **Port**: Backup server port (default: 8080)
- **Target Directory**: Directory on server for storing backups

**File Naming:**
- **Naming Strategy**: Choose from predefined patterns or create custom
- **Include Timestamp**: Whether to include timestamp in filename
- **Date Format**: Format for timestamp (e.g., `yyyy-MM-dd_HH-mm-ss`)

#### Configuration Validation

The system automatically validates:
- MySQL connection parameters
- Directory accessibility
- Server connectivity
- Naming strategy validity

### Email Notifications

#### SMTP Configuration

Configure email notifications for backup status alerts:

```json
{
  "SmtpConfig": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "EnableSsl": true,
    "Username": "your-email@gmail.com",
    "Password": "your-app-password",
    "FromAddress": "backup-system@yourcompany.com",
    "FromName": "MySQL Backup System"
  }
}
```

#### Email Templates

The system includes predefined templates for:
- **Backup Success**: Sent when backup completes successfully
- **Backup Failure**: Sent when backup fails
- **Schedule Reminder**: Sent before scheduled backups
- **Storage Warning**: Sent when storage space is low

### Encryption Settings

#### Password-Based Encryption

Configure AES-256 encryption for backup files:

1. Enable encryption in backup configuration
2. Set a strong encryption password (minimum 12 characters)
3. Configure key derivation settings:
   - **Iterations**: Number of PBKDF2 iterations (default: 100,000)
   - **Key Size**: Encryption key size (default: 256 bits)

#### Security Recommendations

- Use unique, strong passwords for each backup configuration
- Store encryption passwords in a secure password manager
- Regularly rotate encryption passwords
- Test decryption process periodically

---

## Basic Operations

### Manual Backup

#### Running a One-Time Backup

1. Select your backup configuration
2. Click "Run Backup Now"
3. Monitor progress in the status window
4. Review completion status and logs

#### Backup Process Steps

The system follows this process for each backup:

1. **Pre-Backup Validation**
   - Verify MySQL service status
   - Check disk space availability
   - Validate server connectivity

2. **MySQL Service Management**
   - Stop MySQL service safely
   - Wait for complete shutdown
   - Verify service stopped

3. **Data Compression**
   - Compress data directory to ZIP file
   - Report compression progress
   - Validate compressed file integrity

4. **Encryption (if enabled)**
   - Encrypt compressed file with AES-256
   - Generate encryption metadata
   - Validate encrypted file

5. **File Transfer**
   - Transfer file to backup server
   - Support resume for interrupted transfers
   - Verify transfer completion

6. **Post-Backup Tasks**
   - Restart MySQL service
   - Verify MySQL availability
   - Clean up temporary files
   - Send notification emails
   - Update backup logs

### Backup Scheduling

#### Creating Schedules

1. Go to "Schedules" tab
2. Click "New Schedule"
3. Configure schedule settings:

**Schedule Information:**
- **Name**: Descriptive name for the schedule
- **Backup Configuration**: Select which backup to run
- **Description**: Optional description

**Timing Configuration:**
- **Schedule Type**: Choose from presets or custom cron
- **Cron Expression**: For advanced scheduling
- **Time Zone**: Schedule time zone
- **Enabled**: Whether schedule is active

#### Common Schedule Examples

**Daily Backups:**
- Expression: `0 0 2 * * *`
- Description: Every day at 2:00 AM

**Weekly Backups:**
- Expression: `0 0 2 * * 0`
- Description: Every Sunday at 2:00 AM

**Monthly Backups:**
- Expression: `0 0 2 1 * *`
- Description: First day of each month at 2:00 AM

**Hourly Backups:**
- Expression: `0 0 * * * *`
- Description: Every hour on the hour

### Retention Management

#### Setting Up Retention Policies

1. Navigate to "Retention Policies"
2. Click "New Policy"
3. Configure retention rules:

**Policy Settings:**
- **Policy Name**: Unique identifier
- **Backup Directory**: Directory to manage
- **Enabled**: Whether policy is active

**Retention Rules:**
- **Max Age**: Maximum age of backup files (e.g., 30 days)
- **Max Count**: Maximum number of backup files to keep
- **Min Free Space**: Minimum free space to maintain

**Advanced Options:**
- **Dry Run**: Test policy without deleting files
- **Exclude Patterns**: Files to exclude from cleanup
- **Priority**: Policy execution priority

#### Retention Policy Examples

**30-Day Retention:**
```
Max Age: 30 days
Max Count: 30 files
Description: Keep daily backups for 30 days
```

**Weekly Long-Term:**
```
Max Age: 365 days
Max Count: 52 files
File Pattern: *weekly*
Description: Keep weekly backups for 1 year
```

**Storage-Based:**
```
Min Free Space: 100 GB
Max Count: 100 files
Description: Maintain 100GB free space
```

---

## Advanced Features

### Resume Capability

#### How Resume Works

The system supports resuming interrupted transfers:

1. **Chunked Transfer**: Large files are split into chunks
2. **Progress Tracking**: Each chunk transfer is tracked
3. **Resume Token**: Generated for interrupted transfers
4. **Automatic Resume**: System attempts automatic resume
5. **Manual Resume**: Users can manually resume transfers

#### Configuring Resume Settings

```json
{
  "TransferConfig": {
    "ChunkSize": 10485760,  // 10MB chunks
    "EnableResume": true,
    "ResumeTimeout": "01:00:00",  // 1 hour
    "MaxResumeAttempts": 3
  }
}
```

### Large File Handling

#### Optimization for Large Databases

For databases larger than 10GB:

1. **Chunked Processing**: Files split into manageable chunks
2. **Memory Management**: Optimized memory usage during operations
3. **Progress Reporting**: Detailed progress for long operations
4. **Streaming Operations**: Avoid loading entire files into memory

#### Performance Tuning

**Compression Settings:**
- Use `CompressionLevel.Optimal` for best compression
- Use `CompressionLevel.Fastest` for speed priority
- Adjust buffer sizes based on available memory

**Transfer Settings:**
- Increase chunk size for faster networks
- Decrease chunk size for unreliable connections
- Enable parallel transfers for multiple files

### Multi-Threading

#### Background Operations

All long-running operations run in background threads:

- **Backup Operations**: Run without blocking UI
- **File Transfers**: Parallel chunk transfers
- **Compression**: Multi-threaded compression
- **Encryption**: Parallel processing for large files

#### Concurrency Settings

```json
{
  "BackupConfig": {
    "MaxConcurrentBackups": 3,
    "MaxTransferThreads": 4,
    "MaxCompressionThreads": 2
  }
}
```

### Network Resilience

#### Retry Mechanisms

The system includes comprehensive retry logic:

1. **Exponential Backoff**: Increasing delays between retries
2. **Jitter**: Random delays to prevent thundering herd
3. **Circuit Breaker**: Temporary failure protection
4. **Health Checks**: Continuous connectivity monitoring

#### Network Configuration

```json
{
  "NetworkConfig": {
    "MaxRetryAttempts": 5,
    "BaseRetryDelay": "00:00:02",
    "MaxRetryDelay": "00:02:00",
    "EnableJitter": true,
    "ConnectionTimeout": "00:00:30"
  }
}
```

---

## Monitoring and Troubleshooting

### Logging

#### Log Levels

The system uses structured logging with these levels:

- **Debug**: Detailed diagnostic information
- **Information**: General operational messages
- **Warning**: Potentially harmful situations
- **Error**: Error events that don't stop operation
- **Critical**: Serious errors that may cause termination

#### Log Configuration

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "MySqlBackupTool": "Debug"
    },
    "File": {
      "Path": "logs/backup-{Date}.log",
      "RollingInterval": "Day",
      "RetainedFileCountLimit": 30
    }
  }
}
```

#### Log Analysis

**Common Log Patterns:**

```
// Successful backup
[INF] Backup operation {OperationId} completed successfully in {Duration}

// MySQL service issue
[ERR] Failed to stop MySQL service {ServiceName}: {ErrorMessage}

// Network connectivity problem
[WRN] Network connectivity test failed for {Host}:{Port}

// Encryption operation
[INF] File encrypted successfully: {InputFile} -> {OutputFile}
```

### Performance Monitoring

#### Key Metrics

Monitor these metrics for optimal performance:

**Backup Metrics:**
- Backup duration
- Compression ratio
- Transfer speed
- Success rate

**System Metrics:**
- CPU usage during operations
- Memory consumption
- Disk I/O rates
- Network throughput

**Error Metrics:**
- Failure rates by operation type
- Retry attempt counts
- Network timeout frequency

#### Performance Dashboard

The system provides a built-in dashboard showing:

- Recent backup history
- Success/failure rates
- Average backup times
- Storage utilization
- Schedule adherence

### Troubleshooting Common Issues

#### MySQL Service Issues

**Problem**: MySQL service won't stop
**Solution**:
1. Check for active connections
2. Verify service permissions
3. Use force stop if necessary
4. Check MySQL error logs

**Problem**: MySQL service won't start after backup
**Solution**:
1. Verify data directory integrity
2. Check MySQL configuration files
3. Review MySQL error logs
4. Manually start service if needed

#### Network Connectivity Issues

**Problem**: Cannot connect to backup server
**Solution**:
1. Verify server is running
2. Check firewall settings
3. Test network connectivity
4. Verify port configuration

**Problem**: Transfer timeouts
**Solution**:
1. Increase timeout values
2. Check network stability
3. Reduce chunk sizes
4. Enable resume capability

#### Storage Issues

**Problem**: Insufficient disk space
**Solution**:
1. Enable retention policies
2. Increase storage capacity
3. Compress older backups
4. Move backups to external storage

**Problem**: Backup file corruption
**Solution**:
1. Enable file validation
2. Use checksums for verification
3. Test backup restoration
4. Check storage hardware

---

## Best Practices

### Security Best Practices

#### Password Management
- Use unique, strong passwords for each configuration
- Store passwords in secure password managers
- Regularly rotate encryption passwords
- Never store passwords in plain text

#### Network Security
- Use VPN for remote backup operations
- Enable TLS for all network communications
- Implement proper firewall rules
- Monitor network access logs

#### File Security
- Enable encryption for all backup files
- Set appropriate file permissions
- Use secure storage locations
- Implement access auditing

### Operational Best Practices

#### Backup Strategy
- Implement 3-2-1 backup rule (3 copies, 2 different media, 1 offsite)
- Test backup restoration regularly
- Document backup procedures
- Train staff on backup operations

#### Scheduling
- Schedule backups during low-usage periods
- Stagger multiple backup schedules
- Allow sufficient time for completion
- Monitor schedule adherence

#### Monitoring
- Set up automated alerts for failures
- Monitor backup completion times
- Track storage utilization trends
- Review logs regularly

### Performance Best Practices

#### Resource Management
- Allocate sufficient CPU and memory
- Use fast storage for temporary files
- Optimize network bandwidth usage
- Monitor system resource usage

#### Configuration Optimization
- Tune compression settings for your data
- Adjust chunk sizes for your network
- Configure appropriate timeout values
- Enable parallel processing where beneficial

#### Maintenance
- Regularly update the software
- Clean up old log files
- Optimize database indexes
- Review and update configurations

### Disaster Recovery

#### Backup Validation
- Regularly test backup restoration
- Verify backup file integrity
- Test encryption/decryption process
- Document restoration procedures

#### Recovery Planning
- Create detailed recovery procedures
- Test recovery scenarios regularly
- Maintain offline backup copies
- Document contact information for emergencies

#### Business Continuity
- Plan for extended outages
- Identify critical vs. non-critical systems
- Establish recovery time objectives (RTO)
- Define recovery point objectives (RPO)

---

## Support and Resources

### Getting Help

- **Documentation**: Comprehensive guides and API reference
- **Log Analysis**: Use structured logs for troubleshooting
- **Community Forums**: Connect with other users
- **Professional Support**: Available for enterprise customers

### Additional Resources

- **Sample Configurations**: Example configurations for common scenarios
- **PowerShell Scripts**: Automation scripts for advanced users
- **Integration Examples**: Code samples for custom integrations
- **Performance Tuning Guide**: Advanced optimization techniques

### Reporting Issues

When reporting issues, please include:
- System configuration details
- Relevant log entries
- Steps to reproduce the problem
- Expected vs. actual behavior
- Screenshots if applicable

---

This user guide provides comprehensive information for effectively using the MySQL Backup Tool. For technical implementation details, refer to the API Reference documentation.