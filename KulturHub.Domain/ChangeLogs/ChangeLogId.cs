namespace KulturHub.Domain.ChangeLogs;

public readonly record struct ChangeLogId(Guid Value)
{
    public static ChangeLogId New() => new(Guid.NewGuid());

    public static ChangeLogId From(Guid value) => new(value);
}
