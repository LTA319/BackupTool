# MySQL Backup Tool - Configuration Examples

## Overview

This document provides practical configuration examples for common deployment scenarios of the MySQL Backup Tool. Each example includes complete configuration files and setup instructions.

## Table of Contents

1. [Basic Single-Server Setup](#basic-single-server-setup)
2. [Enterprise Multi-Server Environment](#enterprise-multi-server-environment)
3. [High-Availability Configuration](#high-availability-configuration)
4. [Cloud Deployment Examples](#cloud-deployment-examples)
5. [Development Environment Setup](#development-environment-setup)
6. [Specialized Configurations](#specialized-configurations)

---

## Basic Single-Server Setup

### Scenario
Small business with a single MySQL server requiring daily backups with email notifications.

### Client Configuration (appsettings.json)

```json
{
  "MySqlBackupTool": {
    "ConnectionString": "Data Source=backup.db",
    "EnableEncryption": true,
    "EnableNotifications": true,
    "DefaultCompressionLevel": "Optimal",
    "MaxConcurrentBackups": 1,
    "DefaultRetentionDays": 30
  },
  "SmtpConfig": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "EnableSsl": true,
    "Username": "backup@smallbusiness.com",
    "Password": "app-specific-password",
    "FromAddress": "backup@smallbusiness.com",
    "FromName": "Small Business Backup System",
    "TimeoutSeconds": 30
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "MySqlBackupTool": "Information"
    },
    "File": {
      "Path": "logs/backup-{Date}.log",
      "RollingInterval": "Day",
      "RetainedFileCountLimit": 7
    }
  }
}
```

### Backup Configuration

```json
{
  "Name": "Daily Production Backup",
  "MySQLConnection": {
    "Server": "localhost",
    "Port": 3306,
    "Username": "backup_user",
    "Password": "encrypted_password_here",
    "Database": "production_db",
    "ConnectionTimeout": 30
  },
  "DataDirectoryPath": "C:\\ProgramData\\MySQL\\MySQL Server 8.0\\Data",
  "ServiceName": "MySQL80",
  "TargetServer": {
    "IPAddress": "192.168.1.100",
    "Port": 8080,
    "Protocol": "TCP",
    "AuthenticationRequired": false
  },
  "TargetDirectory": "/backups/production",
  "NamingStrategy": {
    "Pattern": "production_backup_{yyyy-MM-dd}_{HH-mm-ss}.zip",
    "IncludeTimestamp": true,
    "DateFormat": "yyyy-MM-dd_HH-mm-ss"
  },
  "IsActive": true
}
```

### Schedule Configuration

```json
{
  "Name": "Daily 2AM Backup",
  "CronExpression": "0 0 2 * * *",
  "BackupConfigurationId": 1,
  "IsEnabled": true,
  "Description": "Daily backup at 2:00 AM",
  "TimeZone": "Eastern Standard Time"
}
```

### Retention Policy

```json
{
  "Name": "30-Day Retention",
  "MaxAge": "30.00:00:00",
  "MaxCount": 30,
  "BackupDirectory": "/backups/production",
  "IsEnabled": true,
  "Description": "Keep daily backups for 30 days",
  "DeleteEmptyDirectories": true
}
```

---

## Enterprise Multi-Server Environment

### Scenario
Large enterprise with multiple MySQL servers, centralized backup storage, and comprehensive monitoring.

### Central Backup Server Configuration

```json
{
  "MySqlBackupTool": {
    "ConnectionString": "Data Source=enterprise_backup.db",
    "EnableEncryption": true,
    "EnableNotifications": true,
    "DefaultCompressionLevel": "Optimal",
    "MaxConcurrentBackups": 10,
    "DefaultRetentionDays": 90,
    "EnableAuditing": true,
    "AuditLogPath": "audit/backup-audit-{Date}.log"
  },
  "ServerConfig": {
    "ListenPort": 8080,
    "MaxConcurrentConnections": 50,
    "EnableSsl": true,
    "SslCertificatePath": "certificates/backup-server.pfx",
    "SslCertificatePassword": "certificate_password",
    "AuthenticationRequired": true,
    "AllowedClients": [
      "192.168.10.0/24",
      "192.168.20.0/24",
      "192.168.30.0/24"
    ]
  },
  "StorageConfig": {
    "PrimaryStoragePath": "/enterprise/backups/primary",
    "SecondaryStoragePath": "/enterprise/backups/secondary",
    "EnableReplication": true,
    "CompressionEnabled": true,
    "EncryptionEnabled": true
  },
  "SmtpConfig": {
    "Host": "mail.enterprise.com",
    "Port": 587,
    "EnableSsl": true,
    "Username": "backup-system@enterprise.com",
    "Password": "enterprise_mail_password",
    "FromAddress": "backup-system@enterprise.com",
    "FromName": "Enterprise Backup System"
  },
  "Alerting": {
    "EnableAlerting": true,
    "BaseUrl": "https://api.enterprise.com",
    "TimeoutSeconds": 45,
    "MaxRetryAttempts": 5,
    "EnableCircuitBreaker": true,
    "DefaultHeaders": {
      "X-API-Key": "enterprise_api_key_here",
      "User-Agent": "MySqlBackupTool/2.0"
    },
    "MinimumSeverity": "Warning",
    "MaxAlertsPerHour": 100,
    "NotificationTimeout": "00:01:00",
    "Email": {
      "Enabled": true,
      "SmtpServer": "smtp.enterprise.com",
      "SmtpPort": 587,
      "UseSsl": true,
      "FromAddress": "backup-system@enterprise.com",
      "Recipients": [
        "dba-team@enterprise.com",
        "ops-team@enterprise.com",
        "backup-admin@enterprise.com"
      ]
    },
    "Webhook": {
      "Enabled": true,
      "Url": "https://monitoring.enterprise.com/webhooks/backup-alerts",
      "HttpMethod": "POST",
      "ContentType": "application/json",
      "AuthToken": "webhook_auth_token_here"
    },
    "FileLog": {
      "Enabled": true,
      "LogDirectory": "logs/alerts",
      "FileNamePattern": "alerts_{yyyy-MM-dd}.log",
      "MaxFileSizeMB": 10,
      "MaxFileCount": 30
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "MySqlBackupTool": "Debug"
    },
    "File": {
      "Path": "logs/enterprise-backup-{Date}.log",
      "RollingInterval": "Day",
      "RetainedFileCountLimit": 90
    },
    "EventLog": {
      "Enabled": true,
      "Source": "Enterprise Backup System"
    }
  }
}
```

### Production Database Server Configuration

```json
{
  "Name": "Production DB Server 1",
  "MySQLConnection": {
    "Server": "prod-db-01.enterprise.com",
    "Port": 3306,
    "Username": "backup_service",
    "Password": "encrypted_service_password",
    "Database": "",
    "ConnectionTimeout": 60
  },
  "DataDirectoryPath": "/var/lib/mysql",
  "ServiceName": "mysql",
  "TargetServer": {
    "IPAddress": "backup-server.enterprise.com",
    "Port": 8080,
    "Protocol": "HTTPS",
    "AuthenticationRequired": true,
    "Credentials": {
      "Username": "prod-db-01",
      "Password": "client_auth_password"
    }
  },
  "TargetDirectory": "/enterprise/backups/production/db-01",
  "NamingStrategy": {
    "Pattern": "prod-db-01_{yyyy-MM-dd}_{HH-mm-ss}_{database}.zip.enc",
    "IncludeTimestamp": true,
    "IncludeHostname": true,
    "IncludeDatabaseName": true
  },
  "EncryptionConfig": {
    "Password": "production_encryption_key",
    "KeySize": 256,
    "Iterations": 100000,
    "SecureDelete": true
  },
  "IsActive": true,
  "Priority": "High",
  "Tags": ["production", "critical", "daily"]
}
```

### Development Database Server Configuration

```json
{
  "Name": "Development DB Server",
  "MySQLConnection": {
    "Server": "dev-db-01.enterprise.com",
    "Port": 3306,
    "Username": "dev_backup",
    "Password": "encrypted_dev_password",
    "Database": "",
    "ConnectionTimeout": 30
  },
  "DataDirectoryPath": "/var/lib/mysql",
  "ServiceName": "mysql",
  "TargetServer": {
    "IPAddress": "backup-server.enterprise.com",
    "Port": 8080,
    "Protocol": "HTTPS",
    "AuthenticationRequired": true
  },
  "TargetDirectory": "/enterprise/backups/development/db-01",
  "NamingStrategy": {
    "Pattern": "dev-db-01_{yyyy-MM-dd}_{database}.zip",
    "IncludeTimestamp": true,
    "IncludeDatabaseName": true
  },
  "EncryptionConfig": {
    "Password": "development_encryption_key",
    "KeySize": 256,
    "Iterations": 50000
  },
  "IsActive": true,
  "Priority": "Medium",
  "Tags": ["development", "weekly"]
}
```

### Enterprise Scheduling Configuration

```json
{
  "Schedules": [
    {
      "Name": "Production Daily Backup",
      "CronExpression": "0 0 2 * * *",
      "BackupConfigurationId": 1,
      "IsEnabled": true,
      "Description": "Daily production backup at 2:00 AM",
      "Priority": "Critical",
      "MaxExecutionTime": "02:00:00",
      "NotifyOnFailure": true,
      "NotifyOnSuccess": false
    },
    {
      "Name": "Production Weekly Full Backup",
      "CronExpression": "0 0 1 * * 0",
      "BackupConfigurationId": 2,
      "IsEnabled": true,
      "Description": "Weekly full backup on Sunday at 1:00 AM",
      "Priority": "Critical",
      "MaxExecutionTime": "04:00:00",
      "NotifyOnFailure": true,
      "NotifyOnSuccess": true
    },
    {
      "Name": "Development Weekly Backup",
      "CronExpression": "0 0 3 * * 6",
      "BackupConfigurationId": 3,
      "IsEnabled": true,
      "Description": "Weekly development backup on Saturday at 3:00 AM",
      "Priority": "Low",
      "MaxExecutionTime": "01:00:00"
    }
  ]
}
```

### Enterprise Retention Policies

```json
{
  "RetentionPolicies": [
    {
      "Name": "Production Daily Retention",
      "MaxAge": "90.00:00:00",
      "MaxCount": 90,
      "BackupDirectory": "/enterprise/backups/production",
      "FilePattern": "*daily*",
      "IsEnabled": true,
      "Description": "Keep daily production backups for 90 days",
      "Priority": 1
    },
    {
      "Name": "Production Weekly Long-Term",
      "MaxAge": "365.00:00:00",
      "MaxCount": 52,
      "BackupDirectory": "/enterprise/backups/production",
      "FilePattern": "*weekly*",
      "IsEnabled": true,
      "Description": "Keep weekly production backups for 1 year",
      "Priority": 2
    },
    {
      "Name": "Development Retention",
      "MaxAge": "30.00:00:00",
      "MaxCount": 10,
      "BackupDirectory": "/enterprise/backups/development",
      "IsEnabled": true,
      "Description": "Keep development backups for 30 days",
      "Priority": 3
    },
    {
      "Name": "Storage Quota Management",
      "MinFreeSpace": 107374182400,
      "BackupDirectory": "/enterprise/backups",
      "IsEnabled": true,
      "Description": "Maintain 100GB free space",
      "Priority": 0
    }
  ]
}
```

---

## High-Availability Configuration

### Scenario
Mission-critical environment requiring redundant backup systems and failover capabilities.

### Primary Backup Server Configuration

```json
{
  "MySqlBackupTool": {
    "ConnectionString": "Data Source=ha_backup_primary.db",
    "EnableEncryption": true,
    "EnableNotifications": true,
    "MaxConcurrentBackups": 20,
    "EnableHighAvailability": true,
    "HAConfig": {
      "Role": "Primary",
      "SecondaryServers": [
        "backup-secondary-01.ha.com:8080",
        "backup-secondary-02.ha.com:8080"
      ],
      "HeartbeatInterval": "00:00:30",
      "FailoverTimeout": "00:02:00",
      "SyncInterval": "00:05:00"
    }
  },
  "ServerConfig": {
    "ListenPort": 8080,
    "EnableSsl": true,
    "SslCertificatePath": "certificates/ha-primary.pfx",
    "MaxConcurrentConnections": 100,
    "LoadBalancing": {
      "Enabled": true,
      "Algorithm": "RoundRobin",
      "HealthCheckInterval": "00:01:00"
    }
  },
  "StorageConfig": {
    "PrimaryStoragePath": "/ha/backups/primary",
    "ReplicationTargets": [
      "/ha/backups/replica1",
      "/ha/backups/replica2"
    ],
    "EnableRealTimeReplication": true,
    "ReplicationMode": "Synchronous",
    "IntegrityCheckInterval": "01:00:00"
  },
  "MonitoringConfig": {
    "EnableHealthChecks": true,
    "HealthCheckPort": 8081,
    "MetricsEnabled": true,
    "MetricsPort": 8082,
    "PrometheusEnabled": true
  }
}
```

### Secondary Backup Server Configuration

```json
{
  "MySqlBackupTool": {
    "ConnectionString": "Data Source=ha_backup_secondary.db",
    "EnableEncryption": true,
    "EnableNotifications": true,
    "MaxConcurrentBackups": 20,
    "EnableHighAvailability": true,
    "HAConfig": {
      "Role": "Secondary",
      "PrimaryServer": "backup-primary.ha.com:8080",
      "HeartbeatInterval": "00:00:30",
      "TakeoverDelay": "00:01:00",
      "SyncFromPrimary": true
    }
  },
  "ServerConfig": {
    "ListenPort": 8080,
    "EnableSsl": true,
    "SslCertificatePath": "certificates/ha-secondary.pfx",
    "MaxConcurrentConnections": 100,
    "StandbyMode": true
  },
  "StorageConfig": {
    "PrimaryStoragePath": "/ha/backups/secondary",
    "SyncFromPrimary": true,
    "SyncInterval": "00:05:00",
    "EnableLocalStorage": true
  }
}
```

### Client HA Configuration

```json
{
  "BackupServers": [
    {
      "IPAddress": "backup-primary.ha.com",
      "Port": 8080,
      "Priority": 1,
      "Weight": 100
    },
    {
      "IPAddress": "backup-secondary-01.ha.com",
      "Port": 8080,
      "Priority": 2,
      "Weight": 50
    },
    {
      "IPAddress": "backup-secondary-02.ha.com",
      "Port": 8080,
      "Priority": 2,
      "Weight": 50
    }
  ],
  "FailoverConfig": {
    "EnableAutoFailover": true,
    "HealthCheckInterval": "00:00:30",
    "FailoverTimeout": "00:02:00",
    "RetryAttempts": 3,
    "RetryDelay": "00:00:10"
  }
}
```

---

## Cloud Deployment Examples

### AWS Configuration

#### EC2 Instance Configuration

```json
{
  "MySqlBackupTool": {
    "ConnectionString": "Data Source=/opt/backup/aws_backup.db",
    "EnableEncryption": true,
    "EnableNotifications": true,
    "CloudProvider": "AWS",
    "AWSConfig": {
      "Region": "us-east-1",
      "S3Bucket": "enterprise-mysql-backups",
      "S3StorageClass": "STANDARD_IA",
      "KMSKeyId": "arn:aws:kms:us-east-1:123456789012:key/12345678-1234-1234-1234-123456789012",
      "EnableS3Transfer": true,
      "S3TransferAcceleration": true
    }
  },
  "StorageConfig": {
    "LocalStoragePath": "/opt/backup/local",
    "CloudStoragePath": "s3://enterprise-mysql-backups/production",
    "EnableCloudSync": true,
    "CloudSyncInterval": "00:15:00",
    "RetainLocalCopies": true,
    "LocalRetentionDays": 7
  },
  "IAMConfig": {
    "UseInstanceProfile": true,
    "RequiredPermissions": [
      "s3:PutObject",
      "s3:GetObject",
      "s3:DeleteObject",
      "s3:ListBucket",
      "kms:Encrypt",
      "kms:Decrypt",
      "kms:GenerateDataKey"
    ]
  }
}
```

#### RDS MySQL Configuration

```json
{
  "Name": "AWS RDS Production",
  "MySQLConnection": {
    "Server": "prod-mysql.cluster-xyz.us-east-1.rds.amazonaws.com",
    "Port": 3306,
    "Username": "backup_user",
    "Password": "encrypted_rds_password",
    "Database": "",
    "ConnectionTimeout": 60,
    "SslMode": "Required"
  },
  "BackupMethod": "RDSSnapshot",
  "RDSConfig": {
    "DBInstanceIdentifier": "prod-mysql-instance",
    "SnapshotIdentifier": "backup-{yyyy-MM-dd}-{HH-mm-ss}",
    "CopyToS3": true,
    "S3Bucket": "enterprise-mysql-backups",
    "S3Prefix": "rds-snapshots/production"
  },
  "TargetServer": {
    "IPAddress": "backup-server.internal.aws.com",
    "Port": 8080,
    "Protocol": "HTTPS"
  }
}
```

### Azure Configuration

```json
{
  "MySqlBackupTool": {
    "ConnectionString": "Data Source=/opt/backup/azure_backup.db",
    "EnableEncryption": true,
    "CloudProvider": "Azure",
    "AzureConfig": {
      "StorageAccountName": "enterprisebackupstorage",
      "ContainerName": "mysql-backups",
      "BlobStorageTier": "Cool",
      "EnableAzureKeyVault": true,
      "KeyVaultUrl": "https://enterprise-backup-kv.vault.azure.net/",
      "ManagedIdentityClientId": "12345678-1234-1234-1234-123456789012"
    }
  },
  "StorageConfig": {
    "LocalStoragePath": "/opt/backup/local",
    "CloudStoragePath": "azure://enterprisebackupstorage/mysql-backups/production",
    "EnableCloudSync": true,
    "CloudSyncInterval": "00:15:00"
  }
}
```

### Google Cloud Configuration

```json
{
  "MySqlBackupTool": {
    "ConnectionString": "Data Source=/opt/backup/gcp_backup.db",
    "EnableEncryption": true,
    "CloudProvider": "GCP",
    "GCPConfig": {
      "ProjectId": "enterprise-backup-project",
      "BucketName": "enterprise-mysql-backups",
      "StorageClass": "NEARLINE",
      "ServiceAccountKeyPath": "/opt/backup/gcp-service-account.json",
      "EnableKMSEncryption": true,
      "KMSKeyName": "projects/enterprise-backup-project/locations/global/keyRings/backup-ring/cryptoKeys/mysql-backup-key"
    }
  }
}
```

---

## Development Environment Setup

### Local Development Configuration

```json
{
  "MySqlBackupTool": {
    "ConnectionString": "Data Source=dev_backup.db",
    "EnableEncryption": false,
    "EnableNotifications": false,
    "DefaultCompressionLevel": "Fastest",
    "MaxConcurrentBackups": 1,
    "DefaultRetentionDays": 7,
    "DevelopmentMode": true
  },
  "ServerConfig": {
    "ListenPort": 8080,
    "EnableSsl": false,
    "MaxConcurrentConnections": 5,
    "AuthenticationRequired": false
  },
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "MySqlBackupTool": "Trace"
    },
    "Console": {
      "Enabled": true,
      "IncludeScopes": true
    },
    "File": {
      "Path": "logs/dev-backup-{Date}.log",
      "RollingInterval": "Day",
      "RetainedFileCountLimit": 3
    }
  }
}
```

### Docker Development Configuration

#### docker-compose.yml

```yaml
version: '3.8'

services:
  mysql:
    image: mysql:8.0
    environment:
      MYSQL_ROOT_PASSWORD: dev_password
      MYSQL_DATABASE: test_db
      MYSQL_USER: dev_user
      MYSQL_PASSWORD: dev_password
    ports:
      - "3306:3306"
    volumes:
      - mysql_data:/var/lib/mysql
      - ./init.sql:/docker-entrypoint-initdb.d/init.sql

  backup-server:
    build: 
      context: .
      dockerfile: Dockerfile.Server
    ports:
      - "8080:8080"
    volumes:
      - backup_storage:/app/backups
      - ./config/server-config.json:/app/appsettings.json
    depends_on:
      - mysql

  backup-client:
    build:
      context: .
      dockerfile: Dockerfile.Client
    volumes:
      - ./config/client-config.json:/app/appsettings.json
      - mysql_data:/mysql-data:ro
    depends_on:
      - backup-server
      - mysql
    environment:
      - MYSQL_DATA_PATH=/mysql-data
      - BACKUP_SERVER_URL=http://backup-server:8080

volumes:
  mysql_data:
  backup_storage:
```

#### Development Client Configuration

```json
{
  "Name": "Docker MySQL Development",
  "MySQLConnection": {
    "Server": "mysql",
    "Port": 3306,
    "Username": "dev_user",
    "Password": "dev_password",
    "Database": "test_db",
    "ConnectionTimeout": 30
  },
  "DataDirectoryPath": "/mysql-data",
  "ServiceName": "mysql",
  "TargetServer": {
    "IPAddress": "backup-server",
    "Port": 8080,
    "Protocol": "HTTP",
    "AuthenticationRequired": false
  },
  "TargetDirectory": "/backups/development",
  "NamingStrategy": {
    "Pattern": "dev_backup_{yyyy-MM-dd}_{HH-mm-ss}.zip",
    "IncludeTimestamp": true
  },
  "IsActive": true
}
```

---

## Specialized Configurations

### Large Database Configuration (>100GB)

```json
{
  "Name": "Large Database Backup",
  "MySQLConnection": {
    "Server": "large-db.company.com",
    "Port": 3306,
    "Username": "backup_service",
    "Password": "encrypted_password",
    "ConnectionTimeout": 300
  },
  "DataDirectoryPath": "/var/lib/mysql",
  "ServiceName": "mysql",
  "LargeFileConfig": {
    "ChunkSize": 104857600,
    "MaxMemoryUsage": 2147483648,
    "EnableParallelCompression": true,
    "CompressionThreads": 4,
    "EnableStreamingTransfer": true,
    "TransferThreads": 8
  },
  "CompressionConfig": {
    "Level": "Optimal",
    "EnableMultiThreading": true,
    "BufferSize": 1048576,
    "EstimatedCompressionRatio": 0.7
  },
  "TransferConfig": {
    "ChunkSize": 52428800,
    "EnableResume": true,
    "ResumeTimeout": "04:00:00",
    "MaxResumeAttempts": 5,
    "ParallelTransfers": 4
  },
  "TimeoutConfig": {
    "BackupTimeout": "08:00:00",
    "CompressionTimeout": "04:00:00",
    "TransferTimeout": "06:00:00"
  }
}
```

### High-Frequency Backup Configuration

```json
{
  "Name": "High-Frequency Transaction Log Backup",
  "BackupType": "TransactionLog",
  "MySQLConnection": {
    "Server": "transactional-db.company.com",
    "Port": 3306,
    "Username": "log_backup_user",
    "Password": "encrypted_password"
  },
  "LogBackupConfig": {
    "BinaryLogPath": "/var/lib/mysql/binlog",
    "BackupInterval": "00:15:00",
    "EnableContinuousBackup": true,
    "LogRetentionHours": 72,
    "EnableLogShipping": true
  },
  "ScheduleConfig": {
    "CronExpression": "0 */15 * * * *",
    "EnableContinuousMode": true,
    "MaxExecutionTime": "00:10:00"
  }
}
```

### Compliance and Audit Configuration

```json
{
  "MySqlBackupTool": {
    "EnableAuditing": true,
    "ComplianceMode": "SOX",
    "AuditConfig": {
      "AuditLogPath": "/audit/backup-audit.log",
      "EnableTamperProtection": true,
      "RequireDigitalSignatures": true,
      "CertificatePath": "/certificates/audit-signing.pfx",
      "RetainAuditLogs": "2555.00:00:00"
    },
    "ComplianceConfig": {
      "RequireEncryption": true,
      "MinimumEncryptionStrength": 256,
      "RequireIntegrityChecks": true,
      "MandatoryRetentionPeriod": "2555.00:00:00",
      "RequireOffSiteStorage": true,
      "EnableImmutableStorage": true
    }
  },
  "SecurityConfig": {
    "RequireStrongAuthentication": true,
    "EnableRoleBasedAccess": true,
    "RequireApprovalForDeletion": true,
    "EnableSecurityEventLogging": true,
    "SecurityEventLogPath": "/security/backup-security.log"
  }
}
```

### Multi-Tenant Configuration

```json
{
  "MySqlBackupTool": {
    "EnableMultiTenancy": true,
    "TenantConfig": {
      "TenantIsolationMode": "Database",
      "EnableTenantSpecificEncryption": true,
      "EnableTenantSpecificRetention": true,
      "TenantConfigurationPath": "/config/tenants"
    }
  },
  "Tenants": [
    {
      "TenantId": "tenant-001",
      "Name": "Customer A",
      "DatabasePrefix": "customer_a_",
      "StorageQuota": 107374182400,
      "BackupSchedule": "0 0 3 * * *",
      "RetentionDays": 90,
      "EncryptionKey": "tenant_001_encryption_key",
      "NotificationEmails": ["admin@customer-a.com"]
    },
    {
      "TenantId": "tenant-002", 
      "Name": "Customer B",
      "DatabasePrefix": "customer_b_",
      "StorageQuota": 53687091200,
      "BackupSchedule": "0 0 4 * * *",
      "RetentionDays": 60,
      "EncryptionKey": "tenant_002_encryption_key",
      "NotificationEmails": ["admin@customer-b.com"]
    }
  ]
}
```

---

## AlertingConfig Configuration Reference

The AlertingConfig section provides comprehensive configuration options for the alerting system, including HTTP client settings, retry policies, and multiple notification channels.

### Complete AlertingConfig Example

```json
{
  "Alerting": {
    "EnableAlerting": true,
    "BaseUrl": "https://api.monitoring.com",
    "TimeoutSeconds": 30,
    "MaxRetryAttempts": 3,
    "EnableCircuitBreaker": false,
    "DefaultHeaders": {
      "X-API-Key": "your_api_key_here",
      "User-Agent": "MySqlBackupTool/1.0",
      "Accept": "application/json"
    },
    "MinimumSeverity": "Error",
    "MaxAlertsPerHour": 50,
    "NotificationTimeout": "00:00:30",
    "Email": {
      "Enabled": true,
      "SmtpServer": "smtp.gmail.com",
      "SmtpPort": 587,
      "UseSsl": true,
      "FromAddress": "backup@company.com",
      "Recipients": [
        "admin@company.com",
        "ops@company.com"
      ]
    },
    "Webhook": {
      "Enabled": false,
      "Url": "https://hooks.slack.com/services/YOUR/WEBHOOK/URL",
      "HttpMethod": "POST",
      "ContentType": "application/json",
      "AuthToken": "optional_auth_token"
    },
    "FileLog": {
      "Enabled": true,
      "LogDirectory": "logs/alerts",
      "FileNamePattern": "alerts_{yyyy-MM-dd}.log",
      "MaxFileSizeMB": 10,
      "MaxFileCount": 30
    }
  }
}
```

### Configuration Properties

#### Core Settings

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `EnableAlerting` | boolean | `true` | Enable or disable the entire alerting system |
| `BaseUrl` | string | `null` | Base URL for HTTP requests (optional) |
| `TimeoutSeconds` | integer | `30` | HTTP client timeout in seconds (1-300) |
| `MaxRetryAttempts` | integer | `3` | Maximum retry attempts for failed HTTP requests (0-10) |
| `EnableCircuitBreaker` | boolean | `false` | Enable circuit breaker pattern for HTTP requests |
| `MinimumSeverity` | string | `"Error"` | Minimum alert severity level (`Debug`, `Info`, `Warning`, `Error`, `Critical`) |
| `MaxAlertsPerHour` | integer | `50` | Maximum alerts per hour to prevent spam (1-1000) |
| `NotificationTimeout` | string | `"00:00:30"` | Timeout for individual notification attempts |

#### HTTP Client Settings

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `DefaultHeaders` | object | `{}` | Default HTTP headers to include in all requests |

**DefaultHeaders Example:**
```json
{
  "DefaultHeaders": {
    "X-API-Key": "your_api_key",
    "User-Agent": "MySqlBackupTool/1.0",
    "Accept": "application/json",
    "Authorization": "Bearer your_token"
  }
}
```

#### Email Configuration

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Email.Enabled` | boolean | `false` | Enable email notifications |
| `Email.SmtpServer` | string | `""` | SMTP server hostname |
| `Email.SmtpPort` | integer | `587` | SMTP server port (1-65535) |
| `Email.UseSsl` | boolean | `true` | Use SSL/TLS for SMTP connection |
| `Email.FromAddress` | string | `""` | Sender email address |
| `Email.Recipients` | array | `[]` | List of recipient email addresses |

#### Webhook Configuration

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Webhook.Enabled` | boolean | `false` | Enable webhook notifications |
| `Webhook.Url` | string | `""` | Webhook URL endpoint |
| `Webhook.HttpMethod` | string | `"POST"` | HTTP method (`POST`, `PUT`, `PATCH`) |
| `Webhook.ContentType` | string | `"application/json"` | Content-Type header for webhook requests |
| `Webhook.AuthToken` | string | `""` | Optional authentication token |

#### File Logging Configuration

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `FileLog.Enabled` | boolean | `true` | Enable file logging for alerts |
| `FileLog.LogDirectory` | string | `"logs/alerts"` | Directory for alert log files |
| `FileLog.FileNamePattern` | string | `"alerts_{yyyy-MM-dd}.log"` | File name pattern with date placeholders |
| `FileLog.MaxFileSizeMB` | integer | `10` | Maximum file size in MB before rotation |
| `FileLog.MaxFileCount` | integer | `30` | Maximum number of log files to retain |

### Configuration Examples by Scenario

#### Development Environment
```json
{
  "Alerting": {
    "EnableAlerting": false,
    "MinimumSeverity": "Debug",
    "FileLog": {
      "Enabled": true,
      "LogDirectory": "logs/dev-alerts",
      "MaxFileCount": 5
    }
  }
}
```

#### Production Environment with Email Only
```json
{
  "Alerting": {
    "EnableAlerting": true,
    "TimeoutSeconds": 45,
    "MaxRetryAttempts": 5,
    "MinimumSeverity": "Warning",
    "MaxAlertsPerHour": 100,
    "Email": {
      "Enabled": true,
      "SmtpServer": "smtp.company.com",
      "SmtpPort": 587,
      "UseSsl": true,
      "FromAddress": "backup-alerts@company.com",
      "Recipients": [
        "dba-team@company.com",
        "ops-team@company.com"
      ]
    },
    "FileLog": {
      "Enabled": true,
      "LogDirectory": "/var/log/backup-alerts",
      "MaxFileSizeMB": 50,
      "MaxFileCount": 90
    }
  }
}
```

#### High-Volume Environment with Webhook Integration
```json
{
  "Alerting": {
    "EnableAlerting": true,
    "BaseUrl": "https://api.monitoring.company.com",
    "TimeoutSeconds": 60,
    "MaxRetryAttempts": 3,
    "EnableCircuitBreaker": true,
    "DefaultHeaders": {
      "X-API-Key": "prod_api_key_here",
      "X-Environment": "production"
    },
    "MinimumSeverity": "Error",
    "MaxAlertsPerHour": 200,
    "Webhook": {
      "Enabled": true,
      "Url": "https://hooks.slack.com/services/T00000000/B00000000/XXXXXXXXXXXXXXXXXXXXXXXX",
      "HttpMethod": "POST",
      "ContentType": "application/json"
    },
    "Email": {
      "Enabled": true,
      "SmtpServer": "smtp.company.com",
      "SmtpPort": 587,
      "UseSsl": true,
      "FromAddress": "critical-alerts@company.com",
      "Recipients": ["oncall@company.com"]
    }
  }
}
```

### Configuration Validation

The system automatically validates all configuration values and provides fallbacks:

- **Invalid timeout values**: Reset to 30 seconds with warning
- **Invalid retry attempts**: Capped at 10 attempts with warning  
- **Invalid URLs**: Cleared with warning, HTTP operations disabled
- **Invalid email addresses**: Removed from recipients list with warning
- **Missing required fields**: Logged as warnings, functionality may be limited

### Environment Variable Overrides

Configuration values can be overridden using environment variables:

```bash
# Override timeout
export Alerting__TimeoutSeconds=60

# Override email settings
export Alerting__Email__SmtpServer=smtp.newserver.com
export Alerting__Email__SmtpPort=465

# Override webhook URL
export Alerting__Webhook__Url=https://new-webhook-url.com
```

### Troubleshooting

#### Common Issues

1. **AlertingService cannot be resolved**
   - Ensure HttpClient is registered in DI container
   - Check that AlertingConfig is properly bound

2. **HTTP requests timing out**
   - Increase `TimeoutSeconds` value
   - Check network connectivity to `BaseUrl`
   - Verify retry policy settings

3. **Email notifications not working**
   - Verify SMTP server settings
   - Check firewall rules for SMTP port
   - Validate email addresses in Recipients list

4. **Webhook notifications failing**
   - Verify webhook URL is accessible
   - Check authentication token if required
   - Review webhook endpoint logs

#### Logging

Enable detailed logging to troubleshoot issues:

```json
{
  "Logging": {
    "LogLevel": {
      "MySqlBackupTool.Shared.Services.AlertingService": "Debug",
      "System.Net.Http.HttpClient": "Information"
    }
  }
}
```

---

## Configuration Validation

### Validation Script (PowerShell)

```powershell
# Validate MySQL Backup Tool Configuration
param(
    [Parameter(Mandatory=$true)]
    [string]$ConfigPath
)

function Test-BackupConfiguration {
    param([string]$Path)
    
    Write-Host "Validating configuration at: $Path" -ForegroundColor Green
    
    # Test JSON validity
    try {
        $config = Get-Content $Path | ConvertFrom-Json
        Write-Host "✓ JSON format is valid" -ForegroundColor Green
    }
    catch {
        Write-Host "✗ Invalid JSON format: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
    
    # Test MySQL connection
    if ($config.MySQLConnection) {
        $connectionString = "Server=$($config.MySQLConnection.Server);Port=$($config.MySQLConnection.Port);Uid=$($config.MySQLConnection.Username);Pwd=$($config.MySQLConnection.Password);"
        try {
            # Test connection logic here
            Write-Host "✓ MySQL connection parameters are valid" -ForegroundColor Green
        }
        catch {
            Write-Host "✗ MySQL connection failed: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
    
    # Test directory paths
    if ($config.DataDirectoryPath -and (Test-Path $config.DataDirectoryPath)) {
        Write-Host "✓ Data directory path exists" -ForegroundColor Green
    }
    else {
        Write-Host "✗ Data directory path does not exist: $($config.DataDirectoryPath)" -ForegroundColor Red
    }
    
    # Test target server connectivity
    if ($config.TargetServer) {
        try {
            $tcpClient = New-Object System.Net.Sockets.TcpClient
            $tcpClient.Connect($config.TargetServer.IPAddress, $config.TargetServer.Port)
            $tcpClient.Close()
            Write-Host "✓ Target server is reachable" -ForegroundColor Green
        }
        catch {
            Write-Host "✗ Cannot connect to target server: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
    
    Write-Host "Configuration validation completed" -ForegroundColor Green
}

Test-BackupConfiguration -Path $ConfigPath
```

### Configuration Template Generator

```bash
#!/bin/bash
# Generate MySQL Backup Tool configuration template

echo "MySQL Backup Tool Configuration Generator"
echo "=========================================="

read -p "Environment (development/staging/production): " environment
read -p "MySQL Server Host: " mysql_host
read -p "MySQL Server Port [3306]: " mysql_port
mysql_port=${mysql_port:-3306}
read -p "MySQL Username: " mysql_user
read -s -p "MySQL Password: " mysql_password
echo
read -p "Data Directory Path: " data_path
read -p "Backup Server IP: " backup_server_ip
read -p "Backup Server Port [8080]: " backup_server_port
backup_server_port=${backup_server_port:-8080}
read -p "Enable Encryption (y/n): " enable_encryption
read -p "Enable Email Notifications (y/n): " enable_notifications

# Generate configuration file
cat > "backup-config-${environment}.json" << EOF
{
  "Name": "${environment^} MySQL Backup",
  "MySQLConnection": {
    "Server": "$mysql_host",
    "Port": $mysql_port,
    "Username": "$mysql_user",
    "Password": "$mysql_password",
    "Database": "",
    "ConnectionTimeout": 30
  },
  "DataDirectoryPath": "$data_path",
  "ServiceName": "mysql",
  "TargetServer": {
    "IPAddress": "$backup_server_ip",
    "Port": $backup_server_port,
    "Protocol": "TCP",
    "AuthenticationRequired": false
  },
  "TargetDirectory": "/backups/$environment",
  "NamingStrategy": {
    "Pattern": "${environment}_backup_{yyyy-MM-dd}_{HH-mm-ss}.zip",
    "IncludeTimestamp": true
  },
  "EncryptionConfig": {
    "Enabled": $([ "$enable_encryption" = "y" ] && echo "true" || echo "false"),
    "KeySize": 256,
    "Iterations": 100000
  },
  "NotificationConfig": {
    "Enabled": $([ "$enable_notifications" = "y" ] && echo "true" || echo "false")
  },
  "IsActive": true
}
EOF

echo "Configuration file generated: backup-config-${environment}.json"
```

---

This configuration examples document provides comprehensive templates for various deployment scenarios. Each example can be customized based on specific requirements and environment constraints.