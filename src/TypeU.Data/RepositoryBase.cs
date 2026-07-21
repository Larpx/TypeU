using System;
using System.Data;

namespace Larpx.PersonalTools.TypeU.Data;

/// <summary>
/// 仓储基类：提供共享的连接工厂与便捷查询入口。
/// </summary>
public abstract class RepositoryBase
{
    private readonly SqliteConnectionFactory _factory;

    /// <summary>
    /// 初始化仓储。
    /// </summary>
    protected RepositoryBase(SqliteConnectionFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <summary>
    /// 打开一个新连接（需由调用方 Dispose）。
    /// </summary>
    protected IDbConnection OpenConnection() => _factory.Create();
}
