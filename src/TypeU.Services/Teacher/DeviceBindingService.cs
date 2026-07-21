using System;
using System.Collections.Generic;
using System.Linq;
using Larpx.PersonalTools.TypeU.Data.Repositories;
using Larpx.PersonalTools.TypeU.Models.Entities;
using Microsoft.Extensions.Logging;
using StudentEntity = Larpx.PersonalTools.TypeU.Models.Entities.Student;

namespace Larpx.PersonalTools.TypeU.Services.Teacher;

/// <summary>
/// 设备绑定管理服务：查看绑定、强制解绑。
/// </summary>
public sealed class DeviceBindingService
{
    private readonly StudentRepository _studentRepository;
    private readonly ILogger<DeviceBindingService>? _logger;

    /// <summary>
    /// 初始化服务。
    /// </summary>
    public DeviceBindingService(StudentRepository studentRepository, ILogger<DeviceBindingService>? logger = null)
    {
        _studentRepository = studentRepository ?? throw new ArgumentNullException(nameof(studentRepository));
        _logger = logger;
    }

    /// <summary>
    /// 查询全部学生绑定信息。
    /// </summary>
    public IReadOnlyList<StudentEntity> GetAllBindings() => _studentRepository.GetAll();

    /// <summary>
    /// 查询指定学生的绑定详情。
    /// </summary>
    public StudentEntity? GetBinding(string studentId) => _studentRepository.GetById(studentId);

    /// <summary>
    /// 计算绑定剩余时长（秒，已过期返回 0）。
    /// </summary>
    public long GetRemainingSeconds(string studentId)
    {
        var student = _studentRepository.GetById(studentId);
        if (student is null)
        {
            return 0;
        }

        var now = DateTime.UtcNow;
        var diff = student.ExpiresAt - now;
        return diff.TotalSeconds > 0 ? (long)diff.TotalSeconds : 0;
    }

    /// <summary>
    /// 判断学号当前是否已绑定其他未过期设备。
    /// 返回 (是否绑定, 已绑定的设备指纹)。
    /// </summary>
    public (bool bound, string? fingerprint) IsBoundToOtherDevice(string studentId, string currentFingerprint)
    {
        var fp = _studentRepository.GetActiveBoundFingerprint(studentId, DateTime.UtcNow);
        if (fp is null)
        {
            return (false, null);
        }
        return (!string.Equals(fp, currentFingerprint, StringComparison.Ordinal), fp);
    }

    /// <summary>
    /// 绑定当前设备指纹（2 小时过期）。
    /// </summary>
    public void Bind(string studentId, string name, string fingerprint)
    {
        var existing = _studentRepository.GetById(studentId);
        var now = DateTime.UtcNow;
        if (existing is null)
        {
            var student = new StudentEntity
            {
                StudentId = studentId,
                Name = name,
                DeviceFingerprint = fingerprint,
                BoundAt = now,
                ExpiresAt = now.AddHours(2),
                Status = Models.Enums.StudentStatus.Online
            };
            _studentRepository.Insert(student);
            _logger?.LogInformation("学生 {StudentId} 绑定设备 {Fingerprint}", studentId, fingerprint);
        }
        else
        {
            existing.Name = name;
            existing.DeviceFingerprint = fingerprint;
            existing.BoundAt = now;
            existing.ExpiresAt = now.AddHours(2);
            _studentRepository.Update(existing);
            _logger?.LogInformation("学生 {StudentId} 重新绑定设备 {Fingerprint}", studentId, fingerprint);
        }
    }

    /// <summary>
    /// 强制解绑学生设备。
    /// </summary>
    public void Unbind(string studentId)
    {
        _studentRepository.Unbind(studentId);
        _logger?.LogInformation("学生 {StudentId} 设备已被强制解绑", studentId);
    }
}
