using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Larpx.PersonalTools.TypeU.Models.Enums;

namespace Larpx.PersonalTools.TypeU.Services.Teacher;

/// <summary>
/// 监控看板服务：聚合学生实时状态，分发异常预警。
/// </summary>
public sealed class MonitoringService
{
    private readonly ConcurrentDictionary<string, StudentMonitorState> _states = new();

    /// <summary>
    /// 学生状态变更事件（参数：学号、最新状态）。
    /// </summary>
    public event Action<StudentMonitorState>? StateUpdated;

    /// <summary>
    /// 异常预警事件（参数：学号、异常原因）。
    /// </summary>
    public event Action<string, string>? AnomalyAlert;

    /// <summary>
    /// 学生上线（注册到监控）。
    /// </summary>
    public void RegisterStudent(string studentId, string name, string clientIp)
    {
        var state = new StudentMonitorState
        {
            StudentId = studentId,
            Name = name,
            ClientIp = clientIp,
            Status = StudentStatus.Online,
            LastSeenUtc = DateTime.UtcNow
        };
        _states[studentId] = state;
        StateUpdated?.Invoke(state);
    }

    /// <summary>
    /// 学生下线。
    /// </summary>
    public void UnregisterStudent(string studentId)
    {
        if (_states.TryGetValue(studentId, out var state))
        {
            state.Status = StudentStatus.Offline;
            StateUpdated?.Invoke(state);
        }
    }

    /// <summary>
    /// 更新实时速度与正确率（由状态上报触发）。
    /// </summary>
    public void UpdateProgress(string studentId, double speed, double accuracy, int progress, int anomalyCount)
    {
        if (!_states.TryGetValue(studentId, out var state))
        {
            return;
        }

        state.Speed = speed;
        state.Accuracy = accuracy;
        state.Progress = progress;
        state.AnomalyCount = anomalyCount;
        state.LastSeenUtc = DateTime.UtcNow;

        if (state.Status == StudentStatus.Online)
        {
            state.Status = StudentStatus.Examining;
        }

        if (anomalyCount > 0)
        {
            state.Status = StudentStatus.Anomaly;
            AnomalyAlert?.Invoke(studentId, $"累计异常 {anomalyCount} 次");
        }

        StateUpdated?.Invoke(state);
    }

    /// <summary>
    /// 标记学生已提交。
    /// </summary>
    public void MarkSubmitted(string studentId)
    {
        if (_states.TryGetValue(studentId, out var state))
        {
            state.Status = StudentStatus.Submitted;
            state.LastSeenUtc = DateTime.UtcNow;
            StateUpdated?.Invoke(state);
        }
    }

    /// <summary>
    /// 获取全部监控状态快照。
    /// </summary>
    public IReadOnlyList<StudentMonitorState> GetAllStates() => _states.Values.ToList();

    /// <summary>
    /// 按状态过滤。
    /// </summary>
    public IReadOnlyList<StudentMonitorState> GetByStatus(StudentStatus status)
        => _states.Values.Where(s => s.Status == status).ToList();

    /// <summary>
    /// 重置全部状态（重新考试时调用）。
    /// </summary>
    public void ResetAll()
    {
        foreach (var state in _states.Values)
        {
            state.Speed = 0;
            state.Accuracy = 0;
            state.Progress = 0;
            state.AnomalyCount = 0;
            state.Status = StudentStatus.Online;
            StateUpdated?.Invoke(state);
        }
    }
}

/// <summary>
/// 单个学生的监控看板状态。
/// </summary>
public sealed class StudentMonitorState
{
    /// <summary>学号。</summary>
    public string StudentId { get; set; } = string.Empty;

    /// <summary>姓名。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>客户端 IP。</summary>
    public string ClientIp { get; set; } = string.Empty;

    /// <summary>状态。</summary>
    public StudentStatus Status { get; set; }

    /// <summary>当前速度（字/分钟）。</summary>
    public double Speed { get; set; }

    /// <summary>当前正确率。</summary>
    public double Accuracy { get; set; }

    /// <summary>已输入字符数。</summary>
    public int Progress { get; set; }

    /// <summary>累计异常次数。</summary>
    public int AnomalyCount { get; set; }

    /// <summary>最近一次上报时间（UTC）。</summary>
    public DateTime LastSeenUtc { get; set; }
}
