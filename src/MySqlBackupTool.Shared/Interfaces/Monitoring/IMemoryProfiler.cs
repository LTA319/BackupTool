using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// 备份操作期间内存分析的接口 / Interface for memory profiling during backup operations
/// 提供内存使用情况监控、快照记录和性能分析功能，帮助优化备份操作的内存使用
/// Provides memory usage monitoring, snapshot recording and performance analysis to help optimize memory usage during backup operations
/// </summary>
public interface IMemoryProfiler
{
    /// <summary>
    /// 开始对备份操作进行内存分析 / Starts memory profiling for a backup operation
    /// 初始化内存监控会话，记录操作开始时的内存基线
    /// Initializes memory monitoring session and records memory baseline at operation start
    /// </summary>
    /// <param name="operationId">操作的唯一标识符 / Unique identifier for the operation</param>
    /// <param name="operationType">被分析的操作类型 / Type of operation being profiled</param>
    void StartProfiling(string operationId, string operationType);

    /// <summary>
    /// 在操作期间记录内存快照 / Records a memory snapshot during the operation
    /// 捕获当前内存状态，包括堆内存使用、GC统计和进程内存信息
    /// Captures current memory state including heap memory usage, GC statistics and process memory information
    /// </summary>
    /// <param name="operationId">操作标识符 / Operation identifier</param>
    /// <param name="phase">操作的当前阶段 / Current phase of the operation</param>
    /// <param name="additionalInfo">额外的上下文信息，可选 / Additional context information, optional</param>
    void RecordSnapshot(string operationId, string phase, string? additionalInfo = null);

    /// <summary>
    /// 停止分析并返回完整的内存分析报告 / Stops profiling and returns the complete memory profile
    /// 结束内存监控会话，计算内存使用统计并生成完整的分析报告
    /// Ends memory monitoring session, calculates memory usage statistics and generates complete analysis report
    /// </summary>
    /// <param name="operationId">操作标识符 / Operation identifier</param>
    /// <returns>操作的完整内存分析报告 / Complete memory profile for the operation</returns>
    MemoryProfile StopProfiling(string operationId);

    /// <summary>
    /// 获取正在进行的操作的当前内存分析报告 / Gets the current memory profile for an ongoing operation
    /// 返回当前正在进行的内存监控会话的实时分析数据
    /// Returns real-time analysis data for currently ongoing memory monitoring session
    /// </summary>
    /// <param name="operationId">操作标识符 / Operation identifier</param>
    /// <returns>当前内存分析报告，如果操作未找到则返回null / Current memory profile or null if operation not found</returns>
    MemoryProfile? GetCurrentProfile(string operationId);

    /// <summary>
    /// 强制执行垃圾回收并记录影响 / Forces garbage collection and records the impact
    /// 触发垃圾回收操作，记录回收前后的内存变化和性能影响
    /// Triggers garbage collection operation and records memory changes and performance impact before and after
    /// </summary>
    /// <param name="operationId">操作标识符 / Operation identifier</param>
    /// <param name="generation">要回收的GC代数，可选 / GC generation to collect, optional</param>
    void ForceGarbageCollection(string operationId, int? generation = null);

    /// <summary>
    /// 基于分析数据获取内存使用建议 / Gets memory usage recommendations based on profiling data
    /// 分析内存使用模式，提供内存优化建议和最佳实践指导
    /// Analyzes memory usage patterns and provides memory optimization recommendations and best practice guidance
    /// </summary>
    /// <param name="profile">要分析的内存分析报告 / Memory profile to analyze</param>
    /// <returns>内存优化建议列表 / List of recommendations for memory optimization</returns>
    List<MemoryRecommendation> GetRecommendations(MemoryProfile profile);
}