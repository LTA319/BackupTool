# MySQL 备份工具 - 配置示例

## 概述

本文档为 MySQL 备份工具的常见部署场景提供实用的配置示例。每个示例都包含完整的配置文件和设置说明。

## 目录

1. [基本单服务器设置](#基本单服务器设置)
2. [企业多服务器环境](#企业多服务器环境)
3. [高可用性配置](#高可用性配置)
4. [云部署示例](#云部署示例)
5. [开发环境设置](#开发环境设置)
6. [专用配置](#专用配置)

---

## 基本单服务器设置

### 场景
小型企业，单个 MySQL 服务器，需要每日备份和邮件通知。

### 客户端配置 (appsettings.json)

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
    "FromName": "小型企业备份系统",
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

### 备份配置

```json
{
  "Name": "每日生产备份",
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

### 调度配置

```json
{
  "Name": "每日凌晨2点备份",
  "CronExpression": "0 0 2 * * *",
  "BackupConfigurationId": 1,
  "IsEnabled": true,
  "Description": "每日凌晨 2:00 备份",
  "TimeZone": "China Standard Time"
}
```

### 保留策略

```json
{
  "Name": "30天保留",
  "MaxAge": "30.00:00:00",
  "MaxCount": 30,
  "BackupDirectory": "/backups/production",
  "IsEnabled": true,
  "Description": "保留每日备份30天",
  "DeleteEmptyDirectories": true
}
```

---

## 企业多服务器环境

### 场景
大型企业，多个 MySQL 服务器，集中备份存储，综合监控。

### 中央备份服务器配置

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
    "FromName": "企业备份系统"
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
    "MaxAlertsPerHour": 100,
    "NotificationTimeout": "00:01:00",
    "Email": {
      "Enabled": true,
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
      "Headers": {
        "X-API-Key": "webhook_api_key_here"
      }
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
      "Source": "企业备份系统"
    }
  }
}
```

### 生产数据库服务器配置

```json
{
  "Name": "生产数据库服务器1",
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

### 开发数据库服务器配置

```json
{
  "Name": "开发数据库服务器",
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

### 企业调度配置

```json
{
  "Schedules": [
    {
      "Name": "生产每日备份",
      "CronExpression": "0 0 2 * * *",
      "BackupConfigurationId": 1,
      "IsEnabled": true,
      "Description": "每日凌晨 2:00 生产备份",
      "Priority": "Critical",
      "MaxExecutionTime": "02:00:00",
      "NotifyOnFailure": true,
      "NotifyOnSuccess": false
    },
    {
      "Name": "生产每周完整备份",
      "CronExpression": "0 0 1 * * 0",
      "BackupConfigurationId": 2,
      "IsEnabled": true,
      "Description": "每周日凌晨 1:00 完整备份",
      "Priority": "Critical",
      "MaxExecutionTime": "04:00:00",
      "NotifyOnFailure": true,
      "NotifyOnSuccess": true
    },
    {
      "Name": "开发每周备份",
      "CronExpression": "0 0 3 * * 6",
      "BackupConfigurationId": 3,
      "IsEnabled": true,
      "Description": "每周六凌晨 3:00 开发备份",
      "Priority": "Low",
      "MaxExecutionTime": "01:00:00"
    }
  ]
}
```

### 企业保留策略

```json
{
  "RetentionPolicies": [
    {
      "Name": "生产每日保留",
      "MaxAge": "90.00:00:00",
      "MaxCount": 90,
      "BackupDirectory": "/enterprise/backups/production",
      "FilePattern": "*daily*",
      "IsEnabled": true,
      "Description": "保留生产每日备份90天",
      "Priority": 1
    },
    {
      "Name": "生产每周长期",
      "MaxAge": "365.00:00:00",
      "MaxCount": 52,
      "BackupDirectory": "/enterprise/backups/production",
      "FilePattern": "*weekly*",
      "IsEnabled": true,
      "Description": "保留生产每周备份1年",
      "Priority": 2
    },
    {
      "Name": "开发保留",
      "MaxAge": "30.00:00:00",
      "MaxCount": 10,
      "BackupDirectory": "/enterprise/backups/development",
      "IsEnabled": true,
      "Description": "保留开发备份30天",
      "Priority": 3
    },
    {
      "Name": "存储配额管理",
      "MinFreeSpace": 107374182400,
      "BackupDirectory": "/enterprise/backups",
      "IsEnabled": true,
      "Description": "维护100GB可用空间",
      "Priority": 0
    }
  ]
}
```

---

## 高可用性配置

### 场景
关键任务环境，需要冗余备份系统和故障转移功能。

### 主备份服务器配置

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

### 辅助备份服务器配置

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

### 客户端高可用配置

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

## 云部署示例

### AWS 配置

#### EC2 实例配置

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

#### RDS MySQL 配置

```json
{
  "Name": "AWS RDS 生产",
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

### Azure 配置

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

### Google Cloud 配置

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

## 开发环境设置

### 本地开发配置

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

### Docker 开发配置

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

#### 开发客户端配置

```json
{
  "Name": "Docker MySQL 开发",
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

## 专用配置

### 大型数据库配置（>100GB）

```json
{
  "Name": "大型数据库备份",
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

### 高频备份配置

```json
{
  "Name": "高频事务日志备份",
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

### 合规和审计配置

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

### 多租户配置

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
      "Name": "客户 A",
      "DatabasePrefix": "customer_a_",
      "StorageQuota": 107374182400,
      "BackupSchedule": "0 0 3 * * *",
      "RetentionDays": 90,
      "EncryptionKey": "tenant_001_encryption_key",
      "NotificationEmails": ["admin@customer-a.com"]
    },
    {
      "TenantId": "tenant-002", 
      "Name": "客户 B",
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

## AlertingConfig 配置参考

AlertingConfig 部分为警报系统提供全面的配置选项，包括 HTTP 客户端设置、重试策略和多种通知渠道。

### 完整的 AlertingConfig 示例

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

### 配置属性

#### 核心设置

| 属性 | 类型 | 默认值 | 描述 |
|------|------|--------|------|
| `EnableAlerting` | boolean | `true` | 启用或禁用整个警报系统 |
| `BaseUrl` | string | `null` | HTTP 请求的基础 URL（可选） |
| `TimeoutSeconds` | integer | `30` | HTTP 客户端超时时间（秒，1-300） |
| `MaxRetryAttempts` | integer | `3` | 失败 HTTP 请求的最大重试次数（0-10） |
| `EnableCircuitBreaker` | boolean | `false` | 为 HTTP 请求启用断路器模式 |
| `MinimumSeverity` | string | `"Error"` | 最小警报严重级别（`Debug`、`Info`、`Warning`、`Error`、`Critical`） |
| `MaxAlertsPerHour` | integer | `50` | 每小时最大警报数量以防止垃圾信息（1-1000） |
| `NotificationTimeout` | string | `"00:00:30"` | 单个通知尝试的超时时间 |

#### HTTP 客户端设置

| 属性 | 类型 | 默认值 | 描述 |
|------|------|--------|------|
| `DefaultHeaders` | object | `{}` | 所有请求中包含的默认 HTTP 头 |

**DefaultHeaders 示例：**
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

#### 邮件配置

| 属性 | 类型 | 默认值 | 描述 |
|------|------|--------|------|
| `Email.Enabled` | boolean | `false` | 启用邮件通知 |
| `Email.SmtpServer` | string | `""` | SMTP 服务器主机名 |
| `Email.SmtpPort` | integer | `587` | SMTP 服务器端口（1-65535） |
| `Email.UseSsl` | boolean | `true` | 为 SMTP 连接使用 SSL/TLS |
| `Email.FromAddress` | string | `""` | 发件人邮箱地址 |
| `Email.Recipients` | array | `[]` | 收件人邮箱地址列表 |

#### Webhook 配置

| 属性 | 类型 | 默认值 | 描述 |
|------|------|--------|------|
| `Webhook.Enabled` | boolean | `false` | 启用 webhook 通知 |
| `Webhook.Url` | string | `""` | Webhook URL 端点 |
| `Webhook.HttpMethod` | string | `"POST"` | HTTP 方法（`POST`、`PUT`、`PATCH`） |
| `Webhook.ContentType` | string | `"application/json"` | webhook 请求的 Content-Type 头 |
| `Webhook.AuthToken` | string | `""` | 可选的身份验证令牌 |

#### 文件日志配置

| 属性 | 类型 | 默认值 | 描述 |
|------|------|--------|------|
| `FileLog.Enabled` | boolean | `true` | 启用警报文件日志 |
| `FileLog.LogDirectory` | string | `"logs/alerts"` | 警报日志文件目录 |
| `FileLog.FileNamePattern` | string | `"alerts_{yyyy-MM-dd}.log"` | 带日期占位符的文件名模式 |
| `FileLog.MaxFileSizeMB` | integer | `10` | 轮转前的最大文件大小（MB） |
| `FileLog.MaxFileCount` | integer | `30` | 保留的最大日志文件数量 |

### 按场景的配置示例

#### 开发环境
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

#### 仅邮件的生产环境
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

#### 带 Webhook 集成的高容量环境
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

### 配置验证

系统自动验证所有配置值并提供回退：

- **无效的超时值**：重置为 30 秒并发出警告
- **无效的重试次数**：限制为最多 10 次并发出警告
- **无效的 URL**：清除并发出警告，HTTP 操作被禁用
- **无效的邮箱地址**：从收件人列表中移除并发出警告
- **缺少必需字段**：记录为警告，功能可能受限

### 环境变量覆盖

可以使用环境变量覆盖配置值：

```bash
# 覆盖超时时间
export Alerting__TimeoutSeconds=60

# 覆盖邮件设置
export Alerting__Email__SmtpServer=smtp.newserver.com
export Alerting__Email__SmtpPort=465

# 覆盖 webhook URL
export Alerting__Webhook__Url=https://new-webhook-url.com
```

### 故障排除

#### 常见问题

1. **AlertingService 无法解析**
   - 确保 HttpClient 已在 DI 容器中注册
   - 检查 AlertingConfig 是否正确绑定

2. **HTTP 请求超时**
   - 增加 `TimeoutSeconds` 值
   - 检查到 `BaseUrl` 的网络连接
   - 验证重试策略设置

3. **邮件通知不工作**
   - 验证 SMTP 服务器设置
   - 检查 SMTP 端口的防火墙规则
   - 验证收件人列表中的邮箱地址

4. **Webhook 通知失败**
   - 验证 webhook URL 是否可访问
   - 如果需要，检查身份验证令牌
   - 查看 webhook 端点日志

#### 日志记录

启用详细日志记录以排除问题：

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

## 配置验证

### 验证脚本 (PowerShell)

```powershell
# 验证 MySQL 备份工具配置
param(
    [Parameter(Mandatory=$true)]
    [string]$ConfigPath
)

function Test-BackupConfiguration {
    param([string]$Path)
    
    Write-Host "验证配置位置：$Path" -ForegroundColor Green
    
    # 测试 JSON 有效性
    try {
        $config = Get-Content $Path | ConvertFrom-Json
        Write-Host "✓ JSON 格式有效" -ForegroundColor Green
    }
    catch {
        Write-Host "✗ 无效的 JSON 格式：$($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
    
    # 测试 MySQL 连接
    if ($config.MySQLConnection) {
        $connectionString = "Server=$($config.MySQLConnection.Server);Port=$($config.MySQLConnection.Port);Uid=$($config.MySQLConnection.Username);Pwd=$($config.MySQLConnection.Password);"
        try {
            # 在此处添加连接测试逻辑
            Write-Host "✓ MySQL 连接参数有效" -ForegroundColor Green
        }
        catch {
            Write-Host "✗ MySQL 连接失败：$($_.Exception.Message)" -ForegroundColor Red
        }
    }
    
    # 测试目录路径
    if ($config.DataDirectoryPath -and (Test-Path $config.DataDirectoryPath)) {
        Write-Host "✓ 数据目录路径存在" -ForegroundColor Green
    }
    else {
        Write-Host "✗ 数据目录路径不存在：$($config.DataDirectoryPath)" -ForegroundColor Red
    }
    
    # 测试目标服务器连接
    if ($config.TargetServer) {
        try {
            $tcpClient = New-Object System.Net.Sockets.TcpClient
            $tcpClient.Connect($config.TargetServer.IPAddress, $config.TargetServer.Port)
            $tcpClient.Close()
            Write-Host "✓ 目标服务器可达" -ForegroundColor Green
        }
        catch {
            Write-Host "✗ 无法连接到目标服务器：$($_.Exception.Message)" -ForegroundColor Red
        }
    }
    
    Write-Host "配置验证完成" -ForegroundColor Green
}

Test-BackupConfiguration -Path $ConfigPath
```

### 配置模板生成器

```bash
#!/bin/bash
# 生成 MySQL 备份工具配置模板

echo "MySQL 备份工具配置生成器"
echo "=========================="

read -p "环境 (development/staging/production): " environment
read -p "MySQL 服务器主机: " mysql_host
read -p "MySQL 服务器端口 [3306]: " mysql_port
mysql_port=${mysql_port:-3306}
read -p "MySQL 用户名: " mysql_user
read -s -p "MySQL 密码: " mysql_password
echo
read -p "数据目录路径: " data_path
read -p "备份服务器 IP: " backup_server_ip
read -p "备份服务器端口 [8080]: " backup_server_port
backup_server_port=${backup_server_port:-8080}
read -p "启用加密 (y/n): " enable_encryption
read -p "启用邮件通知 (y/n): " enable_notifications

# 生成配置文件
cat > "backup-config-${environment}.json" << EOF
{
  "Name": "${environment^} MySQL 备份",
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

echo "配置文件已生成：backup-config-${environment}.json"
```

---

此配置示例文档为各种部署场景提供了综合模板。每个示例都可以根据具体要求和环境约束进行自定义。