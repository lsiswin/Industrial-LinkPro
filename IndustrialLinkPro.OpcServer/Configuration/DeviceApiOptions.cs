namespace IndustrialLinkPro.OpcServer.Configuration;

/// <summary>
/// 设备 API 的配置选项，用于连接云端或后端的设备中心管理系统
/// </summary>
public sealed class DeviceApiOptions
{
    /// <summary>
    /// 配置文件中的节点名称
    /// </summary>
    public const string SectionName = "DeviceApi";

    /// <summary>
    /// API 的基础 URL 地址
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:5288";

    /// <summary>
    /// API 请求的超时时长（秒）
    /// </summary>
    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// 认证配置选项，用于鉴权 API 通信
    /// </summary>
    public AuthOptions Auth { get; set; } = new();
}

/// <summary>
/// 远程 API 的身份认证配置选项
/// </summary>
public sealed class AuthOptions
{
    /// <summary>
    /// 认证用户名
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// 认证密码
    /// </summary>
    public string Password { get; set; } = string.Empty;
}
