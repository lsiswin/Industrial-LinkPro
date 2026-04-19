using System;
using System.Collections.Generic;

namespace RadioIndustrialApp.Models;

public class LoginRequest
{
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string[] Roles { get; set; } = Array.Empty<string>();
}

public class DashboardStatsDto
{
    public int TotalDevices { get; set; }
    public int OnlineDevices { get; set; }
    public int ErrorDevices { get; set; }
    public int OpcNodeCount { get; set; }
    public int ConnectedClients { get; set; }
    public long PersistedDataCount { get; set; }
    public int DataThroughput { get; set; }
}

public class DeviceDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
    public string Protocol { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public List<DataPointDto> DataPoints { get; set; } = new();
}

public class DataPointDto
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public string NodeId { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
}

// ==================== 追加的API DTO模型 ==================== //

public class OperationLogDto
{
    public Guid Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class PermissionSummaryResponse
{
    public string UserName { get; set; } = string.Empty;
    public string[] Roles { get; set; } = Array.Empty<string>();
    public string[] Permissions { get; set; } = Array.Empty<string>();
}

public class BatchPlcDataRequest
{
    public List<PlcDataItemRequest> DataItems { get; set; } = new();
}

public class PlcDataItemRequest
{
    public Guid DeviceId { get; set; }
    public string TagName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public object Value { get; set; } = new object();
}

public class BatchGetNodeIdRequest
{
    public List<Guid> DataPointIds { get; set; } = new();
}

public class UpdateDataPointNodeIdRequest
{
    public string NodeId { get; set; } = string.Empty;
    public int NamespaceIndex { get; set; }
}

public class UpdateNodeIdBatchRequest
{
    public Guid DataPointId { get; set; }
    public string NodeId { get; set; } = string.Empty;
    public int NamespaceIndex { get; set; }
}
