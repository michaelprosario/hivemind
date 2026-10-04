namespace HiveMind.Core.Common;
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount);
