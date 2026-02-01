using Microsoft.EntityFrameworkCore;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using System.ComponentModel.DataAnnotations;

namespace MySqlBackupTool.Shared.Data.Repositories;

/// <summary>
/// 备份配置实体的仓储实现
/// 提供备份配置的专门数据访问方法
/// </summary>
public class BackupConfigurationRepository : Repository<BackupConfiguration>, IBackupConfigurationRepository
{
    /// <summary>
    /// 构造函数，初始化备份配置仓储
    /// </summary>
    /// <param name="context">数据库上下文</param>
    public BackupConfigurationRepository(BackupDbContext context) : base(context)
    {
    }

    /// <summary>
    /// 获取所有活跃的备份配置
    /// </summary>
    /// <returns>活跃的备份配置集合</returns>
    public async Task<IEnumerable<BackupConfiguration>> GetActiveConfigurationsAsync()
    {
        return await _dbSet.Where(c => c.IsActive).ToListAsync();
    }

    /// <summary>
    /// 根据名称获取备份配置
    /// </summary>
    /// <param name="name">配置名称</param>
    /// <returns>匹配的备份配置，如果不存在则返回null</returns>
    public async Task<BackupConfiguration?> GetByNameAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        return await _dbSet.FirstOrDefaultAsync(c => c.Name == name);
    }

    /// <summary>
    /// 检查配置名称是否唯一
    /// </summary>
    /// <param name="name">要检查的名称</param>
    /// <param name="excludeId">要排除的配置ID（用于更新时检查）</param>
    /// <returns>如果名称唯一返回true，否则返回false</returns>
    public async Task<bool> IsNameUniqueAsync(string name, int excludeId = 0)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        return !await _dbSet.AnyAsync(c => c.Name == name && c.Id != excludeId);
    }

    /// <summary>
    /// 激活指定的备份配置
    /// </summary>
    /// <param name="id">配置ID</param>
    /// <returns>如果激活成功返回true，否则返回false</returns>
    public async Task<bool> ActivateConfigurationAsync(int id)
    {
        var configuration = await GetByIdAsync(id);
        if (configuration == null)
            return false;

        configuration.IsActive = true;
        await UpdateAsync(configuration);
        await SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// 停用指定的备份配置
    /// </summary>
    /// <param name="id">配置ID</param>
    /// <returns>如果停用成功返回true，否则返回false</returns>
    public async Task<bool> DeactivateConfigurationAsync(int id)
    {
        var configuration = await GetByIdAsync(id);
        if (configuration == null)
            return false;

        configuration.IsActive = false;
        await UpdateAsync(configuration);
        await SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// 根据目标服务器获取备份配置
    /// </summary>
    /// <param name="ipAddress">服务器IP地址</param>
    /// <param name="port">服务器端口</param>
    /// <returns>匹配的备份配置集合</returns>
    public async Task<IEnumerable<BackupConfiguration>> GetByTargetServerAsync(string ipAddress, int port)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
            return Enumerable.Empty<BackupConfiguration>();

        // 由于TargetServer存储为JSON，我们需要加载所有配置并在内存中过滤
        // 为了更好的性能，如果此查询经常使用，考虑为IP和端口添加单独的列
        var allConfigurations = await GetAllAsync();
        return allConfigurations.Where(c => 
            c.TargetServer.IPAddress == ipAddress && 
            c.TargetServer.Port == port);
    }

    /// <summary>
    /// 验证并保存备份配置
    /// </summary>
    /// <param name="configuration">要验证和保存的配置</param>
    /// <returns>包含成功状态、错误列表和保存后配置的元组</returns>
    public async Task<(bool Success, List<string> Errors, BackupConfiguration? Configuration)> ValidateAndSaveAsync(BackupConfiguration configuration)
    {
        return await ValidateAndSaveAsync(configuration, validateConnections: true);
    }

    /// <summary>
    /// 验证并保存备份配置，可选择是否验证连接
    /// </summary>
    /// <param name="configuration">要验证和保存的配置</param>
    /// <param name="validateConnections">是否验证连接参数</param>
    /// <returns>包含成功状态、错误列表和保存后配置的元组</returns>
    public async Task<(bool Success, List<string> Errors, BackupConfiguration? Configuration)> ValidateAndSaveAsync(BackupConfiguration configuration, bool validateConnections)
    {
        var errors = new List<string>();

        if (configuration == null)
        {
            errors.Add("Configuration cannot be null");
            return (false, errors, null);
        }

        try
        {
            // 验证数据注解
            var validationContext = new ValidationContext(configuration);
            var validationResults = new List<ValidationResult>();
            
            if (!Validator.TryValidateObject(configuration, validationContext, validationResults, true))
            {
                errors.AddRange(validationResults.Select(vr => vr.ErrorMessage ?? "Validation error"));
            }

            // 验证身份验证凭据
            if (!ValidateCredentialFields(configuration, errors))
            {
                // 错误已添加到errors列表中
            }

            // 检查名称唯一性
            if (!await IsNameUniqueAsync(configuration.Name, configuration.Id))
            {
                errors.Add($"Configuration name '{configuration.Name}' is already in use");
            }

            // 仅在请求时验证连接参数
            if (validateConnections)
            {
                var (isValid, connectionErrors) = await configuration.ValidateConnectionParametersAsync();
                if (!isValid)
                {
                    errors.AddRange(connectionErrors);
                }
            }

            // 如果有验证错误，返回它们
            if (errors.Count > 0)
            {
                return (false, errors, null);
            }

            // 保存配置
            BackupConfiguration savedConfiguration;
            if (configuration.Id == 0)
            {
                // 新配置
                configuration.CreatedAt = DateTime.UtcNow;
                savedConfiguration = await AddAsync(configuration);
            }
            else
            {
                // 更新现有配置
                savedConfiguration = await UpdateAsync(configuration);
            }

            await SaveChangesAsync();
            return (true, errors, savedConfiguration);
        }
        catch (Exception ex)
        {
            errors.Add($"Error saving configuration: {ex.Message}");
            return (false, errors, null);
        }
    }

    /// <summary>
    /// 验证凭据格式（确保不包含冒号，因为会影响token格式）
    /// </summary>
    /// <param name="configuration">要验证的配置</param>
    /// <param name="errors">错误列表，验证失败时会添加错误信息</param>
    /// <returns>如果凭据有效返回true，否则返回false</returns>
    private bool ValidateCredentialFields(BackupConfiguration configuration, List<string> errors)
    {
        var isValid = true;

        // 验证ClientId
        if (string.IsNullOrWhiteSpace(configuration.ClientId))
        {
            errors.Add("Client ID cannot be empty or whitespace");
            isValid = false;
        }
        else if (configuration.ClientId.Length > 100)
        {
            errors.Add("Client ID cannot exceed 100 characters");
            isValid = false;
        }

        // 验证ClientSecret
        if (string.IsNullOrWhiteSpace(configuration.ClientSecret))
        {
            errors.Add("Client Secret cannot be empty or whitespace");
            isValid = false;
        }
        else if (configuration.ClientSecret.Length > 200)
        {
            errors.Add("Client Secret cannot exceed 200 characters");
            isValid = false;
        }

        // 验证凭据格式（确保不包含冒号，因为会影响token格式）
        if (!string.IsNullOrWhiteSpace(configuration.ClientId) && configuration.ClientId.Contains(':'))
        {
            errors.Add("Client ID cannot contain colon (:) character");
            isValid = false;
        }

        if (!string.IsNullOrWhiteSpace(configuration.ClientSecret) && configuration.ClientSecret.Contains(':'))
        {
            errors.Add("Client Secret cannot contain colon (:) character");
            isValid = false;
        }

        return isValid;
    }

    /// <summary>
    /// 更新配置的身份验证凭据
    /// </summary>
    /// <param name="configurationId">配置ID</param>
    /// <param name="clientId">新的客户端ID</param>
    /// <param name="clientSecret">新的客户端密钥</param>
    /// <returns>包含成功状态和错误列表的元组</returns>
    public async Task<(bool Success, List<string> Errors)> UpdateCredentialsAsync(int configurationId, string clientId, string clientSecret)
    {
        var errors = new List<string>();

        try
        {
            var configuration = await GetByIdAsync(configurationId);
            if (configuration == null)
            {
                errors.Add($"Configuration with ID {configurationId} not found");
                return (false, errors);
            }

            // 临时设置新凭据进行验证
            var originalClientId = configuration.ClientId;
            var originalClientSecret = configuration.ClientSecret;
            
            configuration.ClientId = clientId;
            configuration.ClientSecret = clientSecret;

            // 验证新凭据
            if (!ValidateCredentialFields(configuration, errors))
            {
                // 恢复原始凭据
                configuration.ClientId = originalClientId;
                configuration.ClientSecret = originalClientSecret;
                return (false, errors);
            }

            // 保存更新的配置
            await UpdateAsync(configuration);
            await SaveChangesAsync();

            return (true, errors);
        }
        catch (Exception ex)
        {
            errors.Add($"Error updating credentials: {ex.Message}");
            return (false, errors);
        }
    }

    /// <summary>
    /// 添加新的备份配置，确保设置创建时间
    /// </summary>
    /// <param name="entity">要添加的配置实体</param>
    /// <returns>添加后的配置实体</returns>
    public override async Task<BackupConfiguration> AddAsync(BackupConfiguration entity)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        // 确保设置了CreatedAt
        if (entity.CreatedAt == default)
            entity.CreatedAt = DateTime.UtcNow;

        return await base.AddAsync(entity);
    }

    /// <summary>
    /// 更新备份配置，保持原有的创建时间
    /// </summary>
    /// <param name="entity">要更新的配置实体</param>
    /// <returns>更新后的配置实体</returns>
    public override async Task<BackupConfiguration> UpdateAsync(BackupConfiguration entity)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        // 更新时不修改CreatedAt
        var existingEntity = await _dbSet.AsNoTracking().FirstOrDefaultAsync(e => e.Id == entity.Id);
        if (existingEntity != null)
        {
            entity.CreatedAt = existingEntity.CreatedAt;
        }

        return await base.UpdateAsync(entity);
    }
}