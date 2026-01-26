using System.ServiceProcess;

namespace MySqlBackupTool.Shared.Models
{
    public class ServiceCheckResult
    {
        public string ServiceName { get; set; } = string.Empty;
        public bool Exists { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public ServiceControllerStatus? Status { get; set; }
        public ServiceType? ServiceType { get; set; }
        public bool? CanStop { get; set; }
        public bool? CanPauseAndContinue { get; set; }
        public bool? CanShutdown { get; set; }
        public string[] DependentServices { get; set; } = Array.Empty<string>();
        public string[] ServicesDependedOn { get; set; } = Array.Empty<string>();
        public string AccessError { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public bool IsRunning => Status == ServiceControllerStatus.Running;
        public bool IsStopped => Status == ServiceControllerStatus.Stopped;
        public bool CanBeBackedUp => Exists && (IsStopped || (CanStop == true && IsRunning));
        public string? BackupAdvice { get; set; }
    }

    public class ServiceInfo
    {
        public string ServiceName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public ServiceControllerStatus Status { get; set; }
        public ServiceType ServiceType { get; set; }
        public bool IsRunning => Status == ServiceControllerStatus.Running;
        public bool IsStoppable { get; set; }
        public string StatusDescription => GetStatusDescription();

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

    public class ServiceDetailInfo : ServiceInfo
    {
        public bool CanStop { get; set; }
        public bool CanPauseAndContinue { get; set; }
        public bool CanShutdown { get; set; }
        public string[] DependentServices { get; set; } = Array.Empty<string>();
        public string[] ServicesDependedOn { get; set; } = Array.Empty<string>();
        public DateTime? StartTime { get; set; }
        public string Account { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string StartType { get; set; } = string.Empty;
        public string LogOnAs { get; set; } = string.Empty;
    }
}
