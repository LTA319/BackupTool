using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using MySqlBackupTool.Shared.Data;
using MySqlBackupTool.Shared.Data.Repositories;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MySqlBackupTool.Tests.Properties;

/// <summary>
/// Property-based tests for backup logging functionality
/// **Validates: Requirements 7.1, 7.2, 7.4**
/// </summary>
public class BackupLoggingPropertyTests : IDisposable
{
    private readonly BackupDbContext _context;
    private readonly IBackupLogRepository _repository;
    private readonly IBackupLogService _service;

    public BackupLoggingPropertyTests()
    {
        var options = new DbContextOptionsBuilder<BackupDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new BackupDbContext(options);
        _repository = new BackupLogRepository(_context);
        
        var mockLogger = new Mock<ILogger<BackupLogService>>();
        _service = new BackupLogService(_repository, mockLogger.Object);
    }

    /// <summary>
    /// **Property 15: Comprehensive Backup Logging**
    /// For any backup operation (successful or failed), the system should log all required information 
    /// including start time, end time, file sizes, completion status, and any error details.
    /// **Validates: Requirements 7.1, 7.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public void ComprehensiveBackupLogging_ShouldLogAllRequiredInformation()
    {
        Prop.ForAll(
            GenerateBackupOperationData(),
            (backupData) =>
            {
                // Arrange - Start a backup operation
                var backupLog = _service.StartBackupAsync(backupData.ConfigurationId).Result;
                
                // Act - Simulate backup operation with various status updates
                _service.UpdateBackupStatusAsync(backupLog.Id, BackupStatus.StoppingMySQL).Wait();
                _service.UpdateBackupStatusAsync(backupLog.Id, BackupStatus.Compressing).Wait();
                _service.UpdateBackupStatusAsync(backupLog.Id, BackupStatus.Transferring).Wait();
                
                // Log transfer chunks if applicable
                if (backupData.TransferChunks != null)
                {
                    for (int i = 0; i < backupData.TransferChunks.Length; i++)
                    {
                        var chunk = backupData.TransferChunks[i];
                        _service.LogTransferChunkAsync(backupLog.Id, i, chunk.Size, chunk.Status, chunk.ErrorMessage).Wait();
                    }
                }
                
                _service.UpdateBackupStatusAsync(backupLog.Id, BackupStatus.StartingMySQL).Wait();
                
                // Complete the backup
                _service.CompleteBackupAsync(
                    backupLog.Id, 
                    backupData.FinalStatus, 
                    backupData.FilePath, 
                    backupData.FileSize, 
                    backupData.ErrorMessage).Wait();

                // Assert - Verify all required information is logged
                var retrievedLog = _service.GetBackupLogDetailsAsync(backupLog.Id).Result;
                
                return retrievedLog != null &&
                       retrievedLog.BackupConfigId == backupData.ConfigurationId &&
                       retrievedLog.StartTime > DateTime.MinValue &&
                       retrievedLog.EndTime.HasValue &&
                       retrievedLog.Status == backupData.FinalStatus &&
                       (backupData.FilePath == null || retrievedLog.FilePath == backupData.FilePath) &&
                       (backupData.FileSize == null || retrievedLog.FileSize == backupData.FileSize) &&
                       (backupData.ErrorMessage == null || retrievedLog.ErrorMessage == backupData.ErrorMessage) &&
                       (backupData.TransferChunks == null || retrievedLog.TransferLogs.Count == backupData.TransferChunks.Length);
            }).QuickCheckThrowOnFailure();
    }

    /// <summary>
    /// **Property 16: Log Storage and Retrieval**
    /// For any backup log entry, storing the log and then searching/retrieving it should return 
    /// the same log data with all metadata intact.
    /// **Validates: Requirements 7.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public void LogStorageAndRetrieval_ShouldPreserveAllData()
    {
        Prop.ForAll(
            GenerateBackupLogData(),
            (logData) =>
            {
                // Arrange & Act - Create and store backup log
                var originalLog = _service.StartBackupAsync(logData.ConfigurationId).Result;
                
                // Update with test data
                _service.UpdateBackupStatusAsync(originalLog.Id, logData.Status, logData.CurrentOperation).Wait();
                
                if (logData.IsCompleted)
                {
                    _service.CompleteBackupAsync(
                        originalLog.Id, 
                        logData.Status, 
                        logData.FilePath, 
                        logData.FileSize, 
                        logData.ErrorMessage).Wait();
                }

                // Test various retrieval methods
                var retrievedById = _service.GetBackupLogDetailsAsync(originalLog.Id).Result;
                
                var filter = new BackupLogFilter 
                { 
                    ConfigurationId = logData.ConfigurationId,
                    Status = logData.Status
                };
                var retrievedByFilter = _service.GetBackupLogsAsync(filter).Result;
                
                var searchCriteria = new BackupLogSearchCriteria
                {
                    ConfigurationId = logData.ConfigurationId,
                    Status = logData.Status,
                    PageSize = 10
                };
                var searchResult = _service.SearchBackupLogsAsync(searchCriteria).Result;

                // Assert - Verify data integrity across all retrieval methods
                var foundInFilter = retrievedByFilter.Any(log => log.Id == originalLog.Id);
                var foundInSearch = searchResult.Logs.Any(log => log.Id == originalLog.Id);
                
                return retrievedById != null &&
                       retrievedById.Id == originalLog.Id &&
                       retrievedById.BackupConfigId == logData.ConfigurationId &&
                       retrievedById.Status == logData.Status &&
                       foundInFilter &&
                       foundInSearch &&
                       searchResult.TotalCount > 0;
            }).QuickCheckThrowOnFailure();
    }

    /// <summary>
    /// Property: Log Search Functionality
    /// For any search criteria, the search should return consistent results and proper pagination.
    /// </summary>
    [Property(MaxTest = 50)]
    public void LogSearch_ShouldReturnConsistentResults()
    {
        Prop.ForAll(
            GenerateSearchTestData(),
            (testData) =>
            {
                // Arrange - Create multiple backup logs
                var createdLogs = new List<BackupLog>();
                
                foreach (var logData in testData.LogsToCreate)
                {
                    var log = _service.StartBackupAsync(logData.ConfigurationId).Result;
                    _service.UpdateBackupStatusAsync(log.Id, logData.Status).Wait();
                    
                    if (logData.IsCompleted)
                    {
                        _service.CompleteBackupAsync(log.Id, logData.Status, logData.FilePath, logData.FileSize).Wait();
                    }
                    
                    createdLogs.Add(log);
                }

                // Act - Search with criteria
                var searchResult = _service.SearchBackupLogsAsync(testData.SearchCriteria).Result;

                // Assert - Verify search results
                var expectedCount = createdLogs.Count(log => 
                    (testData.SearchCriteria.ConfigurationId == null || log.BackupConfigId == testData.SearchCriteria.ConfigurationId) &&
                    (testData.SearchCriteria.Status == null || log.Status == testData.SearchCriteria.Status));

                return searchResult.TotalCount >= 0 &&
                       searchResult.Logs.Count() <= testData.SearchCriteria.PageSize &&
                       searchResult.PageNumber == testData.SearchCriteria.PageNumber &&
                       (expectedCount == 0 || searchResult.TotalCount > 0);
            }).QuickCheckThrowOnFailure();
    }

    #region Test Data Generators

    private static Arbitrary<BackupOperationData> GenerateBackupOperationData()
    {
        return Arb.From(
            from configId in Gen.Choose(1, 100)
            from finalStatus in Gen.Elements(BackupStatus.Completed, BackupStatus.Failed, BackupStatus.Cancelled)
            from filePath in Gen.Elements<string?>(null, "/path/to/backup.zip", "C:\\Backups\\test.zip")
            from fileSize in Gen.Elements<long?>(null, 1024L, 1048576L, 1073741824L)
            from errorMessage in Gen.Elements<string?>(null, "Test error", "Network timeout", "Disk full")
            from hasChunks in Gen.Elements(true, false)
            from chunkCount in Gen.Choose(0, 5)
            select new BackupOperationData
            {
                ConfigurationId = configId,
                FinalStatus = finalStatus,
                FilePath = filePath,
                FileSize = fileSize,
                ErrorMessage = finalStatus == BackupStatus.Failed ? errorMessage : null,
                TransferChunks = hasChunks ? GenerateTransferChunks(chunkCount) : null
            });
    }

    private static TransferChunkData[] GenerateTransferChunks(int count)
    {
        var chunks = new TransferChunkData[count];
        for (int i = 0; i < count; i++)
        {
            chunks[i] = new TransferChunkData
            {
                Size = 1024L * (i + 1),
                Status = i % 10 == 0 ? "Failed" : "Completed",
                ErrorMessage = i % 10 == 0 ? "Chunk transfer failed" : null
            };
        }
        return chunks;
    }

    private static Arbitrary<BackupLogTestData> GenerateBackupLogData()
    {
        return Arb.From(
            from configId in Gen.Choose(1, 50)
            from status in Gen.Elements(BackupStatus.Completed, BackupStatus.Failed, BackupStatus.Transferring, BackupStatus.Cancelled)
            from isCompleted in Gen.Elements(true, false)
            from filePath in Gen.Elements<string?>(null, "/backup/test.zip", "C:\\Backups\\db.zip")
            from fileSize in Gen.Elements<long?>(null, 2048L, 5242880L)
            from errorMessage in Gen.Elements<string?>(null, "Test error message")
            from operation in Gen.Elements<string?>(null, "Compressing data", "Transferring file")
            select new BackupLogTestData
            {
                ConfigurationId = configId,
                Status = status,
                IsCompleted = isCompleted && (status == BackupStatus.Completed || status == BackupStatus.Failed || status == BackupStatus.Cancelled),
                FilePath = filePath,
                FileSize = fileSize,
                ErrorMessage = status == BackupStatus.Failed ? errorMessage : null,
                CurrentOperation = operation
            });
    }

    private static Arbitrary<SearchTestData> GenerateSearchTestData()
    {
        return Arb.From(
            from logCount in Gen.Choose(1, 5)
            from searchConfigId in Gen.Elements<int?>(null, 1, 2, 3)
            from searchStatus in Gen.Elements<BackupStatus?>(null, BackupStatus.Completed, BackupStatus.Failed)
            from pageSize in Gen.Choose(1, 10)
            from pageNumber in Gen.Choose(1, 3)
            select new SearchTestData
            {
                LogsToCreate = GenerateLogDataArray(logCount),
                SearchCriteria = new BackupLogSearchCriteria
                {
                    ConfigurationId = searchConfigId,
                    Status = searchStatus,
                    PageSize = pageSize,
                    PageNumber = pageNumber
                }
            });
    }

    private static BackupLogTestData[] GenerateLogDataArray(int count)
    {
        var logs = new BackupLogTestData[count];
        var random = new System.Random();
        
        for (int i = 0; i < count; i++)
        {
            logs[i] = new BackupLogTestData
            {
                ConfigurationId = random.Next(1, 4),
                Status = (BackupStatus)random.Next(0, 9),
                IsCompleted = random.Next(0, 2) == 1,
                FilePath = random.Next(0, 2) == 1 ? $"/backup/test{i}.zip" : null,
                FileSize = random.Next(0, 2) == 1 ? random.Next(1024, 1048576) : null
            };
        }
        
        return logs;
    }

    #endregion

    #region Test Data Classes

    public class BackupOperationData
    {
        public int ConfigurationId { get; set; }
        public BackupStatus FinalStatus { get; set; }
        public string? FilePath { get; set; }
        public long? FileSize { get; set; }
        public string? ErrorMessage { get; set; }
        public TransferChunkData[]? TransferChunks { get; set; }
    }

    public class TransferChunkData
    {
        public long Size { get; set; }
        public string Status { get; set; } = "Completed";
        public string? ErrorMessage { get; set; }
    }

    public class BackupLogTestData
    {
        public int ConfigurationId { get; set; }
        public BackupStatus Status { get; set; }
        public bool IsCompleted { get; set; }
        public string? FilePath { get; set; }
        public long? FileSize { get; set; }
        public string? ErrorMessage { get; set; }
        public string? CurrentOperation { get; set; }
    }

    public class SearchTestData
    {
        public BackupLogTestData[] LogsToCreate { get; set; } = Array.Empty<BackupLogTestData>();
        public BackupLogSearchCriteria SearchCriteria { get; set; } = new();
    }

    #endregion

    public void Dispose()
    {
        _context.Dispose();
    }
}