using System.Linq.Expressions;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// 通用仓储接口，定义CRUD操作
/// </summary>
/// <typeparam name="T">实体类型</typeparam>
public interface IRepository<T> where T : class
{
    /// <summary>
    /// 获取所有实体
    /// </summary>
    /// <returns>所有实体的异步任务</returns>
    Task<IEnumerable<T>> GetAllAsync();

    /// <summary>
    /// 根据指定条件获取实体
    /// </summary>
    /// <param name="predicate">查询条件表达式</param>
    /// <returns>符合条件的实体集合的异步任务</returns>
    Task<IEnumerable<T>> GetAsync(Expression<Func<T, bool>> predicate);

    /// <summary>
    /// 根据ID获取单个实体
    /// </summary>
    /// <param name="id">实体ID</param>
    /// <returns>实体对象的异步任务，如果不存在则返回null</returns>
    Task<T?> GetByIdAsync(int id);

    /// <summary>
    /// 获取符合条件的第一个实体，如果没有找到则返回null
    /// </summary>
    /// <param name="predicate">查询条件表达式</param>
    /// <returns>第一个符合条件的实体的异步任务，如果不存在则返回null</returns>
    Task<T?> GetFirstOrDefaultAsync(Expression<Func<T, bool>> predicate);

    /// <summary>
    /// 添加新实体
    /// </summary>
    /// <param name="entity">要添加的实体</param>
    /// <returns>添加后的实体的异步任务</returns>
    Task<T> AddAsync(T entity);

    /// <summary>
    /// 更新现有实体
    /// </summary>
    /// <param name="entity">要更新的实体</param>
    /// <returns>更新后的实体的异步任务</returns>
    Task<T> UpdateAsync(T entity);

    /// <summary>
    /// 根据ID删除实体
    /// </summary>
    /// <param name="id">要删除的实体ID</param>
    /// <returns>如果删除成功返回true的异步任务，否则返回false</returns>
    Task<bool> DeleteAsync(int id);

    /// <summary>
    /// 删除实体
    /// </summary>
    /// <param name="entity">要删除的实体</param>
    /// <returns>如果删除成功返回true的异步任务，否则返回false</returns>
    Task<bool> DeleteAsync(T entity);

    /// <summary>
    /// 检查是否存在符合条件的实体
    /// </summary>
    /// <param name="predicate">查询条件表达式</param>
    /// <returns>如果存在符合条件的实体返回true的异步任务，否则返回false</returns>
    Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate);

    /// <summary>
    /// 获取符合条件的实体数量
    /// </summary>
    /// <param name="predicate">查询条件表达式，如果为null则统计所有实体</param>
    /// <returns>实体数量的异步任务</returns>
    Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null);

    /// <summary>
    /// 保存所有待处理的更改
    /// </summary>
    /// <returns>受影响的实体数量的异步任务</returns>
    Task<int> SaveChangesAsync();
}