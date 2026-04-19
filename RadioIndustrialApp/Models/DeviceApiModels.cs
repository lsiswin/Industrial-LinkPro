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

// ==================== 报警模型 ==================== //

public enum AlarmSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2,
    Critical = 3
}

public enum AlarmStatus
{
    Active = 0,
    Acknowledged = 1,
    Resolved = 2
}

public class AlarmDto
{
    public Guid Id { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public AlarmSeverity Severity { get; set; }
    public AlarmStatus Status { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public DateTimeOffset? AcknowledgedAtUtc { get; set; }
    public string? AcknowledgedBy { get; set; }
    public DateTimeOffset? ResolvedAtUtc { get; set; }
}

// ==================== 追加的API DTO模型 ==================== //

public class OperationLogDto
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
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
    public string Value { get; set; } = string.Empty;
    public string ValueType { get; set; } = string.Empty;
    public string? Quality { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}

public class PlcDataRecordDto
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public string Address { get; set; } = string.Empty;
    public string TagName { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string ValueType { get; set; } = string.Empty;
    public string Quality { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public class PlcDataPagedResponse
{
    public List<PlcDataRecordDto> Records { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
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
