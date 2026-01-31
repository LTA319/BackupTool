# MySQL Backup Tool

A distributed enterprise-grade backup solution for MySQL databases built on .NET 8. The system provides a client-server architecture for managing large-scale database backups with advanced features like file chunking, resume capability, and comprehensive monitoring.

## Key Features

- **Distributed Architecture**: Separate client and server components for scalable backup operations
- **Large File Support**: Handles 100GB+ databases through intelligent file chunking
- **Resume Capability**: Interrupted transfer recovery with checksum validation
- **Enterprise Monitoring**: Comprehensive logging, progress reporting, and alerting
- **Flexible Configuration**: SQLite-based configuration management with retention policies
- **Multi-threading**: Background operations with real-time progress tracking

## Components

- **Client Application**: Windows Forms GUI for backup management and MySQL instance control
- **Server Application**: Console service for receiving, storing, and organizing backup files
- **Shared Library**: Common interfaces, models, and services used by both client and server

The tool is designed for enterprise environments requiring reliable, automated MySQL backup workflows with detailed audit trails and recovery capabilities.