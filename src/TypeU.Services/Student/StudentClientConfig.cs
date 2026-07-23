using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Larpx.PersonalTools.TypeU.Services.Student;

/// <summary>
/// 学生端本地配置（教师 IP/端口）。
/// </summary>
public sealed partial class StudentClientConfig
{
    /// <summary>教师端 IP。</summary>
    public string TeacherHost { get; set; } = string.Empty;

    /// <summary>教师端 TCP 端口。</summary>
    public int TeacherPort { get; set; } = 5700;

    /// <summary>
    /// 默认配置文件路径。
    /// </summary>
    public static string DefaultPath =>
        Path.Combine(AppContext.BaseDirectory, "student-config.json");

    /// <summary>
    /// 从磁盘加载；不存在则返回空配置。
    /// </summary>
    public static StudentClientConfig Load(string? path = null)
    {
        path ??= DefaultPath;
        try
        {
            if (!File.Exists(path))
            {
                return new StudentClientConfig();
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize(json, ConfigJsonContext.Default.StudentClientConfig) ?? new StudentClientConfig();
        }
        catch
        {
            return new StudentClientConfig();
        }
    }

    /// <summary>
    /// 保存到磁盘。
    /// </summary>
    public void Save(string? path = null)
    {
        path ??= DefaultPath;
        File.WriteAllText(path, JsonSerializer.Serialize(this, ConfigJsonContext.Default.StudentClientConfig));
    }

    /// <summary>
    /// Native AOT 友好的 JSON 序列化上下文（源生成，避免反射与动态代码生成）。
    /// </summary>
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(StudentClientConfig))]
    internal sealed partial class ConfigJsonContext : JsonSerializerContext
    {
    }
}
