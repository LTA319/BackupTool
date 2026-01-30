using Microsoft.EntityFrameworkCore;
using MySqlBackupTool.Shared.Interfaces;
using System.Linq.Expressions;

namespace MySqlBackupTool.Shared.Data.Repositories;

/// <summary>
/// 通用仓储模式实现类
/// 提供标准的CRUD（创建、读取、更新、删除）操作和常用查询方法
/// </summary>
/// <typeparam name="T">实体类型，必须是引用类型</typeparam>
/// <remarks>
/// 该类实现了仓储模式，为所有实体类型提供统一的数据访问接口
/// 主要特性：
/// 1. 支持异步操作，提高应用程序性能
/// 2. 提供灵活的查询条件支持
/// 3. 统一的错误处理和参数验证
/// 4. 支持Entity Framework Core的所有功能
/// 
/// 使用示例：
/// var repository = new Repository&lt;BackupConfiguration&gt;(context);
/// var configs = await repository.GetAllAsync();
/// </remarks>
public class Repository<T> : IRepository<T> where T : class
{
    #region 受保护的字段

    /// <summary>
    /// Entity Framework数据库上下文实例
    /// 提供对数据库的访问和操作功能
    /// </summary>
    protected readonly BackupDbContext _context;
    
    /// <summary>
    /// 当前实体类型的DbSet实例
    /// 提供对特定实体表的直接访问
    /// </summary>
    protected readonly DbSet<T> _dbSet;

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化Repository类的新实例
    /// </summary>
    /// <param name="context">Entity Framework数据库上下文</param>
    /// <exception cref="ArgumentNullException">当context参数为null时抛出</exception>
    public Repository(BackupDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _dbSet = _context.Set<T>();
    }

    #endregion

    #region 查询操作

    /// <summary>
    /// 异步获取所有实体记录
    /// </summary>
    /// <returns>包含所有实体的集合</returns>
    /// <remarks>
    /// 注意：对于大型数据集，建议使用分页查询或添加过滤条件
    /// 该方法会将所有数据加载到内存中
    /// </remarks>
    public virtual async Task<IEnumerable<T>> GetAllAsync()
    {
        return await _dbSet.ToListAsync();
    }

    /// <summary>
    /// 根据指定条件异步获取实体集合
    /// </summary>
    /// <param name="predicate">查询条件表达式</param>
    /// <returns>符合条件的实体集合</returns>
    /// <remarks>
    /// 使用Lambda表达式作为查询条件，支持复杂的查询逻辑
    /// 示例：await repository.GetAsync(x => x.IsActive == true)
    /// </remarks>
    /// <exception cref="ArgumentNullException">当predicate参数为null时抛出</exception>
    public virtual async Task<IEnumerable<T>> GetAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.Where(predicate).ToListAsync();
    }

    /// <summary>
    /// 根据主键ID异步获取单个实体
    /// </summary>
    /// <param name="id">实体的主键ID</param>
    /// <returns>匹配的实体对象，如果不存在则返回null</returns>
    /// <remarks>
    /// 该方法使用Entity Framework的Find方法，会首先检查上下文缓存
    /// 如果缓存中不存在，则查询数据库
    /// </remarks>
    public virtual async Task<T?> GetByIdAsync(int id)
    {
        return await _dbSet.FindAsync(id);
    }

    /// <summary>
    /// 获取符合条件的第一个实体，如果没有找到则返回null
    /// </summary>
    /// <param name="predicate">查询条件表达式</param>
    /// <returns>第一个符合条件的实体，如果不存在则返回null</returns>
    /// <remarks>
    /// 该方法只返回第一个匹配的记录，适用于预期只有一个结果的查询
    /// 如果有多个匹配记录，只返回第一个
    /// </remarks>
    /// <exception cref="ArgumentNullException">当predicate参数为null时抛出</exception>
    public virtual async Task<T?> GetFirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.FirstOrDefaultAsync(predicate);
    }

    #endregion

    #region 修改操作

    /// <summary>
    /// 异步添加新实体到数据库
    /// </summary>
    /// <param name="entity">要添加的实体对象</param>
    /// <returns>添加后的实体对象（包含生成的ID等信息）</returns>
    /// <remarks>
    /// 该方法只是将实体添加到上下文的跟踪中，需要调用SaveChangesAsync()才能真正保存到数据库
    /// 添加后的实体会包含数据库生成的主键值
    /// </remarks>
    /// <exception cref="ArgumentNullException">当entity参数为null时抛出</exception>
    public virtual async Task<T> AddAsync(T entity)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        var entry = await _dbSet.AddAsync(entity);
        return entry.Entity;
    }

    /// <summary>
    /// 异步更新现有实体
    /// </summary>
    /// <param name="entity">要更新的实体对象</param>
    /// <returns>更新后的实体对象</returns>
    /// <remarks>
    /// 该方法会标记实体为已修改状态，需要调用SaveChangesAsync()才能真正保存到数据库
    /// Entity Framework会自动检测属性变化并只更新修改过的字段
    /// </remarks>
    /// <exception cref="ArgumentNullException">当entity参数为null时抛出</exception>
    public virtual async Task<T> UpdateAsync(T entity)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        _dbSet.Update(entity);
        return await Task.FromResult(entity);
    }

    /// <summary>
    /// 根据主键ID异步删除实体
    /// </summary>
    /// <param name="id">要删除的实体主键ID</param>
    /// <returns>如果删除成功返回true，如果实体不存在返回false</returns>
    /// <remarks>
    /// 该方法会先查找实体，如果存在则删除
    /// 需要调用SaveChangesAsync()才能真正从数据库中删除
    /// </remarks>
    public virtual async Task<bool> DeleteAsync(int id)
    {
        var entity = await GetByIdAsync(id);
        if (entity == null)
            return false;

        return await DeleteAsync(entity);
    }

    /// <summary>
    /// 异步删除指定实体
    /// </summary>
    /// <param name="entity">要删除的实体对象</param>
    /// <returns>如果删除成功返回true，如果实体为null返回false</returns>
    /// <remarks>
    /// 该方法直接删除指定的实体对象
    /// 需要调用SaveChangesAsync()才能真正从数据库中删除
    /// </remarks>
    public virtual async Task<bool> DeleteAsync(T entity)
    {
        if (entity == null)
            return false;

        _dbSet.Remove(entity);
        return await Task.FromResult(true);
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 检查是否存在符合条件的实体
    /// </summary>
    /// <param name="predicate">查询条件表达式</param>
    /// <returns>如果存在符合条件的实体返回true，否则返回false</returns>
    /// <remarks>
    /// 该方法只检查是否存在匹配的记录，不会加载实际的实体数据
    /// 性能比GetAsync()方法更好，适用于只需要检查存在性的场景
    /// </remarks>
    /// <exception cref="ArgumentNullException">当predicate参数为null时抛出</exception>
    public virtual async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.AnyAsync(predicate);
    }

    /// <summary>
    /// 获取符合条件的实体数量
    /// </summary>
    /// <param name="predicate">查询条件表达式，如果为null则统计所有实体</param>
    /// <returns>符合条件的实体数量</returns>
    /// <remarks>
    /// 该方法只返回数量，不会加载实际的实体数据
    /// 如果predicate为null，则统计表中的所有记录数
    /// </remarks>
    public virtual async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null)
    {
        if (predicate == null)
            return await _dbSet.CountAsync();
        
        return await _dbSet.CountAsync(predicate);
    }

    /// <summary>
    /// 异步保存所有待处理的更改到数据库
    /// </summary>
    /// <returns>受影响的实体数量</returns>
    /// <remarks>
    /// 该方法会将上下文中所有待处理的更改（添加、修改、删除）提交到数据库
    /// 返回值表示实际受影响的记录数
    /// 如果没有任何更改，返回0
    /// </remarks>
    /// <exception cref="DbUpdateException">当数据库更新失败时抛出</exception>
    public virtual async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    #endregion
}