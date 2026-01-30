namespace MySqlBackupTool.Shared.Models;

/// <summary>
/// SSL/TLS服务的配置选项
/// 定义网络通信中的安全连接设置
/// </summary>
public class SslConfiguration
{
    /// <summary>
    /// 是否为网络通信使用SSL/TLS
    /// 默认启用以确保通信安全
    /// </summary>
    public bool UseSSL { get; set; } = true;

    /// <summary>
    /// 服务器证书文件路径
    /// 用于SSL/TLS连接的服务器端证书
    /// </summary>
    public string? ServerCertificatePath { get; set; }

    /// <summary>
    /// 服务器证书文件密码
    /// 用于解密受密码保护的证书文件
    /// </summary>
    public string? ServerCertificatePassword { get; set; }

    /// <summary>
    /// 是否要求客户端证书
    /// 启用双向SSL认证时需要设置为true
    /// </summary>
    public bool RequireClientCertificate { get; set; } = false;

    /// <summary>
    /// 是否在客户端验证服务器证书
    /// 建议在生产环境中启用以防止中间人攻击
    /// </summary>
    public bool ValidateServerCertificate { get; set; } = true;

    /// <summary>
    /// 是否允许自签名证书
    /// 在开发环境中可能需要启用，生产环境建议禁用
    /// </summary>
    public bool AllowSelfSignedCertificates { get; set; } = false;

    /// <summary>
    /// 用于验证的预期证书主题名称
    /// 用于验证服务器证书的身份
    /// </summary>
    public string? ExpectedCertificateSubject { get; set; }

    /// <summary>
    /// 用于验证的证书指纹
    /// 提供额外的证书验证层
    /// </summary>
    public string? CertificateThumbprint { get; set; }
}