using System;
using System.Threading;
using System.Threading.Tasks;
using Larpx.PersonalTools.TypeU.Models.Dtos;
using Larpx.PersonalTools.TypeU.Network.Protocol;
using Larpx.PersonalTools.TypeU.Network.Tcp;
using Microsoft.Extensions.Logging;
using ProtoBuf;

namespace Larpx.PersonalTools.TypeU.Services.Student;

/// <summary>
/// 成绩回传结果。
/// </summary>
public sealed class ResultSubmitResult
{
    /// <summary>是否成功。</summary>
    public bool Success { get; init; }

    /// <summary>失败原因（成功时为 null）。</summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// 成绩回传服务：考试结束时打包加密成绩回传，含失败重试。
/// </summary>
public sealed class ResultSubmitService
{
    private readonly TcpExamClient _client;
    private readonly ILogger<ResultSubmitService>? _logger;
    private readonly int _maxRetries;
    private readonly TimeSpan _retryInterval;

    /// <summary>
    /// 初始化服务。
    /// </summary>
    /// <param name="client">TCP 客户端。</param>
    /// <param name="maxRetries">最大重试次数（默认 3）。</param>
    /// <param name="retryIntervalMs">重试间隔（默认 1000 毫秒）。</param>
    /// <param name="logger">日志。</param>
    public ResultSubmitService(
        TcpExamClient client,
        int maxRetries = 3,
        int retryIntervalMs = 1000,
        ILogger<ResultSubmitService>? logger = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _maxRetries = maxRetries;
        _retryInterval = TimeSpan.FromMilliseconds(retryIntervalMs);
        _logger = logger;
    }

    /// <summary>
    /// 提交成绩。失败时按 maxRetries 重试；重试耗尽则缓存到本地文件等待断线补传。
    /// </summary>
    /// <param name="result">成绩 DTO。</param>
    /// <returns>提交结果。</returns>
    public async Task<ResultSubmitResult> SubmitAsync(ExamResultDto result)
    {
        if (result is null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        byte[] payload;
        using (var ms = new System.IO.MemoryStream())
        {
            Serializer.Serialize(ms, result);
            payload = ms.ToArray();
        }

        for (var attempt = 1; attempt <= _maxRetries; attempt++)
        {
            if (!_client.IsConnected)
            {
                _logger?.LogWarning("成绩回传第 {Attempt} 次失败：未连接", attempt);
                await Task.Delay(_retryInterval).ConfigureAwait(false);
                continue;
            }

            try
            {
                await _client.SendAsync(MessageType.ResultSubmit, payload).ConfigureAwait(false);
                _logger?.LogInformation("成绩已回传：会话 {SessionId} 学号 {StudentId} 速度 {Speed}",
                    result.SessionId, result.StudentId, result.Speed);
                return new ResultSubmitResult { Success = true };
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "成绩回传第 {Attempt} 次失败", attempt);
                await Task.Delay(_retryInterval).ConfigureAwait(false);
            }
        }

        // 全部重试失败：缓存到本地（由调用方决定后续补救策略）。
        CacheLocally(result);
        return new ResultSubmitResult
        {
            Success = false,
            ErrorMessage = $"成绩回传失败（已重试 {_maxRetries} 次），已缓存到本地等待补传。"
        };
    }

    private void CacheLocally(ExamResultDto result)
    {
        try
        {
            var dir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TypeU",
                "pending-results");
            System.IO.Directory.CreateDirectory(dir);
            var path = System.IO.Path.Combine(dir, $"{result.RecordId:D}.bin");
            using var ms = new System.IO.MemoryStream();
            Serializer.Serialize(ms, result);
            System.IO.File.WriteAllBytes(path, ms.ToArray());
            _logger?.LogInformation("成绩已缓存到本地：{Path}", path);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "本地缓存成绩失败");
        }
    }
}
