namespace IndustrialLinkPro.OpcServer.Drivers;

/// <summary>
/// 设备连接字符串解析工具类，用于将类似 "host=192.168.1.100;port=502" 的字符串解析为键值对字典
/// </summary>
internal static class ConnectionStringParser
{
    /// <summary>
    /// 解析连接字符串并返回字典
    /// </summary>
    /// <param name="connectionString">分号分隔的键值对字符串</param>
    /// <returns>不区分大小写的参数键值对字典</returns>
    public static IReadOnlyDictionary<string, string> Parse(string connectionString)
    {
        return connectionString
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(segment => segment.Split('=', 2, StringSplitOptions.TrimEntries))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.OrdinalIgnoreCase);
    }
}
