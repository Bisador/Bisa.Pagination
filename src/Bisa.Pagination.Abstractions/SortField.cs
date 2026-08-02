using Bisa.Pagination.Abstractions.Enums;

namespace Bisa.Pagination.Abstractions;

/// <summary>
/// A sort key. To support Composite Key, a list of this type
/// (in order of priority) are used; For example [CreatedAt DESC, Id ASC].
/// </summary>
/// <param name="Name">
/// The logical/physical name of the field. In Core, it is extracted from Expression;
/// In Dapper you have to give the column name directly.
/// </param>
/// <param name="Direction">The sorting direction of this key.</param>
public sealed record SortField(string Name, SortDirection Direction = SortDirection.Ascending)
{
    public override string ToString() => $"{Name} {(Direction == SortDirection.Ascending ? "ASC" : "DESC")}";
}
