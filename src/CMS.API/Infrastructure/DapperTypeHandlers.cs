using System.Data;
using Dapper;

namespace CMS.API.Infrastructure;

/// <summary>Maps SQL <c>date</c> to <see cref="DateOnly"/>.</summary>
public class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
{
    public override DateOnly Parse(object value) => DateOnly.FromDateTime((DateTime)value);

    public override void SetValue(IDbDataParameter parameter, DateOnly value)
    {
        parameter.DbType = DbType.Date;
        parameter.Value = value.ToDateTime(TimeOnly.MinValue);
    }
}

/// <summary>Maps SQL <c>time(7)</c> to <see cref="TimeOnly"/>.</summary>
public class TimeOnlyTypeHandler : SqlMapper.TypeHandler<TimeOnly>
{
    public override TimeOnly Parse(object value) => TimeOnly.FromTimeSpan((TimeSpan)value);

    public override void SetValue(IDbDataParameter parameter, TimeOnly value)
    {
        parameter.DbType = DbType.Time;
        parameter.Value = value.ToTimeSpan();
    }
}

/// <summary>Registers the project-wide Dapper type handlers. Safe to call more than once.</summary>
public static class DapperConfig
{
    private static bool _registered;
    private static readonly object Gate = new();

    public static void Register()
    {
        lock (Gate)
        {
            if (_registered) return;

            SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
            SqlMapper.AddTypeHandler(new TimeOnlyTypeHandler());
            DefaultTypeMap.MatchNamesWithUnderscores = false;

            _registered = true;
        }
    }
}
