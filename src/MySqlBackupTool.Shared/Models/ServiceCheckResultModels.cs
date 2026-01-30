using System.ServiceProcess;

namespace MySqlBackupTool.Shared.Models
{
    /// <summary>
    /// 服务检查结果
    /// 包含Windows服务的详细状态和可操作性信息
    /// </summary>
    public class ServiceCheckResult
    {
        /// <summary>
        /// 服务名称
        /// </summary>
        public string ServiceName { get; set; } = string.Empty;
        
        /// <summary>
        /// 服务是否存在
        /// </summary>
        public bool Exists { get; set; }
        
        /// <summary>
        /// 服务显示名称
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;
        
        /// <summary>
        /// 服务当前状态
        /// </summary>
        public ServiceControllerStatus? Status { get; set; }
        
        /// <summary>
        /// 服务类型
        /// </summary>
        public ServiceType? ServiceType { get; set; }
        
        /// <summary>
        /// 服务是否可以停止
        /// </summary>
        public bool? CanStop { get; set; }
        
        /// <summary>
        /// 服务是否可以暂停和继续
        /// </summary>
        public bool? CanPauseAndContinue { get; set; }
        
        /// <summary>
        /// 服务是否可以关闭
        /// </summary>
        public bool? CanShutdown { get; set; }
        
        /// <summary>
        /// 依赖此服务的其他服务
        /// </summary>
        public string[] DependentServices { get; set; } = Array.Empty<string>();
        
        /// <summary>
        /// 此服务依赖的其他服务
        /// </summary>
        public string[] ServicesDependedOn { get; set; } = Array.Empty<string>();
        
        /// <summary>
        /// 访问错误信息
        /// </summary>
        public string AccessError { get; set; } = string.Empty;
        
        /// <summary>
        /// 错误消息
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;
        
        /// <summary>
        /// 服务是否正在运行
        /// </summary>
        public bool IsRunning => Status == ServiceControllerStatus.Running;
        
        /// <summary>
        /// 服务是否已停止
        /// </summary>
        public bool IsStopped => Status == ServiceControllerStatus.Stopped;
        
        /// <summary>
        /// 服务是否可以进行备份
        /// 服务存在且（已停止或可以停止且正在运行）
        /// </summary>
        public bool CanBeBackedUp => Exists && (IsStopped || (CanStop == true && IsRunning));
        
        /// <summary>
        /// 备份建议信息
        /// </summary>
        public string? BackupAdvice { get; set; }
    }

    /// <summary>
    /// 服务基本信息
    /// 包含服务的基础状态信息
    /// </summary>
    public class ServiceInfo
    {
        /// <summary>
        /// 服务名称
        /// </summary>
        public string ServiceName { get; set; } = string.Empty;
        
        /// <summary>
        /// 服务显示名称
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;
        
        /// <summary>
        /// 服务状态
        /// </summary>
        public ServiceControllerStatus Status { get; set; }
        
        /// <summary>
        /// 服务类型
        /// </summary>
        public ServiceType ServiceType { get; set; }
        
        /// <summary>
        /// 服务是否正在运行
        /// </summary>
        public bool IsRunning => Status == ServiceControllerStatus.Running;
        
        /// <summary>
        /// 服务是否可以停止
        /// </summary>
        public bool IsStoppable { get; set; }
        
        /// <summary>
        /// 状态描述（中文）
        /// </summary>
        public string StatusDescription => GetStatusDescription();

        /// <summary>
        /// 获取服务状态的中文描述
        /// </summary>
        /// <returns>状态的中文描述</returns>
        private string GetStatusDescription()
        {
            return Status switch
            {
                ServiceControllerStatus.Running => "运行中",
                ServiceControllerStatus.Stopped => "已停止",
                ServiceControllerStatus.StartPending => "正在启动",
                ServiceControllerStatus.StopPending => "正在停止",
                ServiceControllerStatus.Paused => "已暂停",
                ServiceControllerStatus.PausePending => "正在暂停",
                ServiceControllerStatus.ContinuePending => "正在恢复",
                _ => Status.ToString()
            };
        }
    }

    /// <summary>
    /// 服务详细信息
    /// 继承自ServiceInfo，包含更详细的服务信息
    /// </summary>
    public class ServiceDetailInfo : ServiceInfo
    {
        /// <summary>
        /// 服务是否可以停止
        /// </summary>
        public bool CanStop { get; set; }
        
        /// <summary>
        /// 服务是否可以暂停和继续
        /// </summary>
        public bool CanPauseAndContinue { get; set; }
        
        /// <summary>
        /// 服务是否可以关闭
        /// </summary>
        public bool CanShutdown { get; set; }
        
        /// <summary>
        /// 依赖此服务的其他服务列表
        /// </summary>
        public string[] DependentServices { get; set; } = Array.Empty<string>();
        
        /// <summary>
        /// 此服务依赖的其他服务列表
        /// </summary>
        public string[] ServicesDependedOn { get; set; } = Array.Empty<string>();
        
        /// <summary>
        /// 服务启动时间
        /// </summary>
        public DateTime? StartTime { get; set; }
        
        /// <summary>
        /// 服务运行账户
        /// </summary>
        public string Account { get; set; } = string.Empty;
        
        /// <summary>
        /// 服务描述
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// 服务可执行文件路径
        /// </summary>
        public string Path { get; set; } = string.Empty;
        
        /// <summary>
        /// 服务启动类型
        /// </summary>
        public string StartType { get; set; } = string.Empty;
        
        /// <summary>
        /// 服务登录身份
        /// </summary>
        public string LogOnAs { get; set; } = string.Empty;
    }
}
