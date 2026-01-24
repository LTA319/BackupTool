using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// Interface for memory profiling during backup operations
/// </summary>
public interface IMemoryProfiler
{
    /// <summary>
    /// Starts memory profiling for a backup operation
    /// </summary>
    /// <param name="operationId">Unique identifier for the operation</param>
    /// <param name="operationType">Type of operation being profiled</param>
    void StartProfiling(string operationId, string operationType);

    /// <summary>
    /// Records a memory snapshot during the operation
    /// </summary>
    /// <param name="operationId">Operation identifier</param>
    /// <param name="phase">Current phase of the operation</param>
    /// <param name="additionalInfo">Additional context information</param>
    void RecordSnapshot(string operationId, string phase, string? additionalInfo = null);

    /// <summary>
    /// Stops profiling and returns the complete memory profile
    /// </summary>
    /// <param name="operationId">Operation identifier</param>
    /// <returns>Complete memory profile for the operation</returns>
    MemoryProfile StopProfiling(string operationId);

    /// <summary>
    /// Gets the current memory profile for an ongoing operation
    /// </summary>
    /// <param name="operationId">Operation identifier</param>
    /// <returns>Current memory profile or null if operation not found</returns>
    MemoryProfile? GetCurrentProfile(string operationId);

    /// <summary>
    /// Forces garbage collection and records the impact
    /// </summary>
    /// <param name="operationId">Operation identifier</param>
    /// <param name="generation">GC generation to collect (optional)</param>
    void ForceGarbageCollection(string operationId, int? generation = null);

    /// <summary>
    /// Gets memory usage recommendations based on profiling data
    /// </summary>
    /// <param name="profile">Memory profile to analyze</param>
    /// <returns>List of recommendations for memory optimization</returns>
    List<MemoryRecommendation> GetRecommendations(MemoryProfile profile);
}