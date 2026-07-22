using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Larpx.PersonalTools.TypeU.Models.Enums;

namespace Larpx.PersonalTools.TypeU.Services.Teacher;

/// <summary>
/// 监控看板：连接态/登录态/速度/最小化。
/// </summary>
public sealed class MonitoringService
{
    private readonly ConcurrentDictionary<string, StudentMonitorState> _byKey = new();

    /// <summary>状态变更。</summary>
    public event Action<StudentMonitorState>? StateUpdated;

    /// <summary>异常预警。</summary>
    public event Action<string, string>? AnomalyAlert;

    /// <summary>
    /// 登记连接（未登录也可见）。
    /// </summary>
    public void RegisterConnection(string clientId, string deviceFingerprint, string clientIp, string computerName)
    {
        var state = _byKey.GetOrAdd(clientId, _ => new StudentMonitorState { ClientId = clientId });
        state.ClientId = clientId;
        state.DeviceFingerprint = deviceFingerprint;
        state.ClientIp = clientIp;
        state.ComputerName = computerName;
        state.Status = StudentStatus.Online;
        state.IsLoggedIn = false;
        state.LastSeenUtc = DateTime.UtcNow;
        StateUpdated?.Invoke(state);
    }

    /// <summary>
    /// 标记已登录。
    /// </summary>
    public void MarkLoggedIn(string clientId, string studentId, string name)
    {
        if (!_byKey.TryGetValue(clientId, out var state))
        {
            state = new StudentMonitorState { ClientId = clientId };
            _byKey[clientId] = state;
        }

        state.StudentId = studentId;
        state.Name = name;
        state.IsLoggedIn = true;
        state.Status = StudentStatus.Examining;
        state.LastSeenUtc = DateTime.UtcNow;
        StateUpdated?.Invoke(state);
    }

    /// <summary>
    /// 按学号注册（兼容旧路径）。
    /// </summary>
    public void RegisterStudent(string studentId, string name, string clientIp)
    {
        var existing = _byKey.Values.FirstOrDefault(s => s.StudentId == studentId);
        var key = existing?.ClientId ?? studentId;
        var state = _byKey.GetOrAdd(key, _ => new StudentMonitorState { ClientId = key });
        state.StudentId = studentId;
        state.Name = name;
        state.ClientIp = clientIp;
        state.IsLoggedIn = true;
        state.Status = StudentStatus.Online;
        state.LastSeenUtc = DateTime.UtcNow;
        StateUpdated?.Invoke(state);
    }

    /// <summary>
    /// 连接断开。
    /// </summary>
    public void UnregisterConnection(string clientId)
    {
        if (_byKey.TryGetValue(clientId, out var state))
        {
            state.Status = StudentStatus.Offline;
            StateUpdated?.Invoke(state);
        }
    }

    /// <summary>
    /// 学生下线（按学号）。
    /// </summary>
    public void UnregisterStudent(string studentId)
    {
        foreach (var state in _byKey.Values.Where(s => s.StudentId == studentId))
        {
            state.IsLoggedIn = false;
            state.Status = StudentStatus.Online;
            state.StudentId = string.Empty;
            state.Name = string.Empty;
            StateUpdated?.Invoke(state);
        }
    }

    /// <summary>
    /// 更新进度与窗口状态。
    /// </summary>
    public void UpdateProgress(
        string key,
        double speed,
        double accuracy,
        int progress,
        int anomalyCount,
        bool isMinimized,
        string clientMode)
    {
        if (!_byKey.TryGetValue(key, out var state))
        {
            state = _byKey.Values.FirstOrDefault(s =>
                s.StudentId == key || s.DeviceFingerprint == key);
            if (state is null)
            {
                return;
            }
        }

        state.Speed = speed;
        state.Accuracy = accuracy;
        state.Progress = progress;
        state.AnomalyCount = anomalyCount;
        state.IsMinimized = isMinimized;
        state.ClientMode = clientMode ?? string.Empty;
        state.LastSeenUtc = DateTime.UtcNow;

        if (state.IsLoggedIn && state.Status == StudentStatus.Online)
        {
            state.Status = StudentStatus.Examining;
        }

        if (anomalyCount > 0)
        {
            state.Status = StudentStatus.Anomaly;
            AnomalyAlert?.Invoke(state.StudentId.Length > 0 ? state.StudentId : key, $"累计异常 {anomalyCount} 次");
        }

        StateUpdated?.Invoke(state);
    }

    /// <summary>
    /// 兼容旧签名。
    /// </summary>
    public void UpdateProgress(string studentId, double speed, double accuracy, int progress, int anomalyCount)
        => UpdateProgress(studentId, speed, accuracy, progress, anomalyCount, false, string.Empty);

    /// <summary>
    /// 标记已提交一次成绩。
    /// </summary>
    public void MarkSubmitted(string studentId)
    {
        foreach (var state in _byKey.Values.Where(s => s.StudentId == studentId))
        {
            state.Status = StudentStatus.Submitted;
            state.LastSeenUtc = DateTime.UtcNow;
            StateUpdated?.Invoke(state);
        }
    }

    /// <summary>
    /// 设置允许登出标记（看板展示）。
    /// </summary>
    public void SetLogoutAllowed(string studentId, bool allowed)
    {
        foreach (var state in _byKey.Values.Where(s => s.StudentId == studentId))
        {
            state.LogoutAllowed = allowed;
            StateUpdated?.Invoke(state);
        }
    }

    /// <summary>
    /// 全部快照。
    /// </summary>
    public IReadOnlyList<StudentMonitorState> GetAllStates() => _byKey.Values.ToList();

    /// <summary>
    /// 按状态过滤。
    /// </summary>
    public IReadOnlyList<StudentMonitorState> GetByStatus(StudentStatus status)
        => _byKey.Values.Where(s => s.Status == status).ToList();

    /// <summary>
    /// 重置进度。
    /// </summary>
    public void ResetAll()
    {
        foreach (var state in _byKey.Values)
        {
            state.Speed = 0;
            state.Accuracy = 0;
            state.Progress = 0;
            state.AnomalyCount = 0;
            if (state.Status != StudentStatus.Offline)
            {
                state.Status = state.IsLoggedIn ? StudentStatus.Examining : StudentStatus.Online;
            }

            StateUpdated?.Invoke(state);
        }
    }
}

/// <summary>
/// 监控看板单行状态。
/// </summary>
public sealed class StudentMonitorState
{
    /// <summary>TCP 客户端 ID。</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>学号。</summary>
    public string StudentId { get; set; } = string.Empty;

    /// <summary>姓名。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>设备指纹。</summary>
    public string DeviceFingerprint { get; set; } = string.Empty;

    /// <summary>计算机名。</summary>
    public string ComputerName { get; set; } = string.Empty;

    /// <summary>客户端 IP。</summary>
    public string ClientIp { get; set; } = string.Empty;

    /// <summary>状态。</summary>
    public StudentStatus Status { get; set; }

    /// <summary>是否已登录。</summary>
    public bool IsLoggedIn { get; set; }

    /// <summary>是否允许登出。</summary>
    public bool LogoutAllowed { get; set; }

    /// <summary>窗口是否最小化。</summary>
    public bool IsMinimized { get; set; }

    /// <summary>客户端模式文案。</summary>
    public string ClientMode { get; set; } = string.Empty;

    /// <summary>速度。</summary>
    public double Speed { get; set; }

    /// <summary>正确率。</summary>
    public double Accuracy { get; set; }

    /// <summary>进度。</summary>
    public int Progress { get; set; }

    /// <summary>异常次数。</summary>
    public int AnomalyCount { get; set; }

    /// <summary>最近上报时间。</summary>
    public DateTime LastSeenUtc { get; set; }
}
