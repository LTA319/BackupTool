using Microsoft.EntityFrameworkCore;
using MySqlBackupTool.Shared.Interfaces;
using System.Linq.Expressions;

namespace MySqlBackupTool.Shared.Data.Repositories;

/// <summary>
/// 通用仓储实现，提供CRUD操作
/// </summary>
/// <typeparam name="T">实体类型</typeparam>
public class Repository<T> : IRepository<T> where T : class
{
    /// <summary>
    /// 数据库上下文
    /// </summary>
    protected readonly BackupDbContext _context;
    
    /// <summary>
    /// 实体的DbSet
    /// </summary>
    protected readonly DbSet<T> _dbSet;

    /// <summary>
    /// 构造函数，初始化仓储
    /// </summary>
    /// <param name="context">数据库上下文</param>
    public Repository(BackupDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _dbSet = _context.Set<T>();
    }

    /// <summary>
    /// 获取所有实体
    /// </summary>
    /// <returns>所有实体的集合</returns>
    public virtual async Task<IEnumerable<T>> GetAllAsync()
    {
        return await _dbSet.ToListAsync();
    }

    /// <summary>
    /// 根据指定条件获取实体
    /// </summary>
    /// <param name="predicate">查询条件</param>
    /// <returns>符合条件的实体集合</returns>
    public virtual async Task<IEnumerable<T>> GetAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.Where(predicate).ToListAsync();
    }

    /// <summary>
    /// 根据ID获取单个实体
    /// </summary>
    /// <param name="id">实体ID</param>
    /// <returns>实体对象，如果不存在则返回null</returns>
    public virtual async Task<T?> GetByIdAsync(int id)
    {
        return await _dbSet.FindAsync(id);
    }

    /// <summary>
    /// 获取符合条件的第一个实体，如果没有找到则返回null
    /// </summary>
    /// <param name="predicate">查询条件</param>
    /// <returns>第一个符合条件的实体，如果不存在则返回null</returns>
    public virtual async Task<T?> GetFirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.FirstOrDefaultAsync(predicate);
    }

    /// <summary>
    /// 添加新实体
    /// </summary>
    /// <param name="entity">要添加的实体</param>
    /// <returns>添加后的实体</returns>
    public virtual async Task<T> AddAsync(T entity)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        var entry = await _dbSet.AddAsync(entity);
        return entry.Entity;
    }

    /// <summary>
    /// 更新现有实体
    /// </summary>
    /// <param name="entity">要更新的实体</param>
    /// <returns>更新后的实体</returns>
    public virtual async Task<T> UpdateAsync(T entity)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        _dbSet.Update(entity);
        return await Task.FromResult(entity);
    }

    /// <summary>
    /// 根据ID删除实体
    /// </summary>
    /// <param name="id">要删除的实体ID</param>
    /// <returns>如果删除成功返回true，否则返回false</returns>
    public virtual async Task<bool> DeleteAsync(int id)
    {
        var entity = await GetByIdAsync(id);
        if (entity == null)
            return false;

        return await DeleteAsync(entity);
    }

    /// <summary>
    /// 删除实体
    /// </summary>
    /// <param name="entity">要删除的实体</param>
    /// <returns>如果删除成功返回true，否则返回false</returns>
    public virtual async Task<bool> DeleteAsync(T entity)
    {
        if (entity == null)
            return false;

        _dbSet.Remove(entity);
        return await Task.FromResult(true);
    }

    /// <summary>
    /// 检查是否存在符合条件的实体
    /// </summary>
    /// <param name="predicate">查询条件</param>
    /// <returns>如果存在符合条件的实体返回true，否则返回false</returns>
    public virtual async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.AnyAsync(predicate);
    }

    /// <summary>
    /// 获取符合条件的实体数量
    /// </summary>
    /// <param name="predicate">查询条件，如果为null则统计所有实体</param>
    /// <returns>实体数量</returns>
    public virtual async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null)
    {
        if (predicate == null)
            return await _dbSet.CountAsync();
        
        return await _dbSet.CountAsync(predicate);
    }

    /// <summary>
    /// 保存所有待处理的更改
    /// </summary>
    /// <returns>受影响的实体数量</returns>
    public virtual async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }
}