namespace Bisa.Pagination.Core;

internal static class CursorPositionExtractor
{
    /// <summary>Compiles the KeySelector delegates once and reuses them for each item (optimized for performance).</summary>
    public static Func<T, CursorPosition> CreateExtractor<T>(IReadOnlyList<SortSpecification<T>> specs)
    {
        var compiled = specs.Select(s => (spec: s, fn: s.KeySelector.Compile())).ToList();

        return item =>
        {
            var keys = new List<CursorKeyValue>(compiled.Count);
            foreach (var (spec, fn) in compiled)
            {
                var value = fn(item);
                // The boxed value is always either null or exactly of the Underlying type (CLR never
                // does not box Nullable<T> with wrapper); So TypeName should be the same type
                // Underlying to be consistent with Allow-list in ICursorCodec (such as DefaultCursorCodec).
                var underlyingType = Nullable.GetUnderlyingType(spec.PropertyType) ?? spec.PropertyType;
                keys.Add(new CursorKeyValue(spec.PropertyName, value, underlyingType.FullName ?? underlyingType.Name));
            }

            return new CursorPosition(keys, DateTimeOffset.UtcNow);
        };
    }
}
