using System.Reflection;

namespace Bisa.Pagination.Core;

internal static class ExpressionHelper
{
    /// <summary>
    /// from an Expression like `x => x.CreatedAt` (which is due to the signature of Func<T,object?>
    /// (usually wrapped with a Convert node) extracts the actual name and type of the Member.
    /// </summary>
    public static (string Name, Type Type) GetMember<T>(Expression<Func<T, object?>> expression)
    {
        var body = expression.Body;
        if (body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
            body = unary.Operand;

        if (body is not MemberExpression member)
            throw new ArgumentException("KeySelector must be a direct access to Property/Field, eg x => x.Id.",
                nameof(expression));

        var type = member.Member switch
        {
            PropertyInfo p => p.PropertyType,
            FieldInfo f => f.FieldType,
            _ => throw new ArgumentException("KeySelector must refer to a Property or Field.", nameof(expression))
        };

        return (member.Member.Name, type);
    }

    /// <summary>Constructs a well-typed MemberExpression (without additional Convert) on the given parameter.</summary>
    public static Expression BuildMemberAccess(ParameterExpression parameter, string propertyName) =>
        Expression.PropertyOrField(parameter, propertyName);

    /// <summary>
    /// Creates a comparison expression (greater than/less than) between a member and a constant.
    /// <paramref name="underlyingTypedConstant"/> must be created with the Underlying type (non-nullable) of the member;
    /// This method itself converts to Nullable if needed.
    /// for string from String.Compare and for types that do not have a comparison operator (such as Guid)
    /// Uses CompareTo to translate to SQL in most EF Core Providers.
    /// </summary>
    public static Expression BuildCompare(Expression member, Expression underlyingTypedConstant, bool greaterThan)
    {
        var memberType = member.Type;
        var underlyingMemberType = Nullable.GetUnderlyingType(memberType) ?? memberType;

        if (underlyingMemberType == typeof(string))
        {
            var compareMethod =
                typeof(string).GetMethod(nameof(string.Compare), new[] { typeof(string), typeof(string) })!;
            var call = Expression.Call(compareMethod, member, underlyingTypedConstant);
            return greaterThan
                ? Expression.GreaterThan(call, Expression.Constant(0))
                : Expression.LessThan(call, Expression.Constant(0));
        }

        // If the member is Nullable<T>, we also convert the constant to the same Nullable<T>
        // so that both GreaterThan/LessThan operands are exactly the same type (Lifted Comparison standard).
        var nativeConstant = memberType != underlyingTypedConstant.Type
            ? Expression.Convert(underlyingTypedConstant, memberType)
            : underlyingTypedConstant;

        try
        {
            return greaterThan
                ? Expression.GreaterThan(member, nativeConstant)
                : Expression.LessThan(member, nativeConstant);
        }
        catch (InvalidOperationException)
        {
            var compareTo =
                underlyingMemberType.GetMethod(nameof(IComparable.CompareTo), new[] { underlyingMemberType });
            if (compareTo is null)
                throw new NotSupportedException(
                    $"Type '{memberType.Name}' has no compare operator and no CompareTo operator; not supported for Keyset Pagination." +
                    "Suggestion: use a comparable column (number/date/string) as the sort key.");

            var nonNullMember = Nullable.GetUnderlyingType(memberType) is not null
                ? Expression.Property(member, "Value")
                : member;
            // Here we must pass the underlying (non-nullable) version of the constant because it is a signature
            // CompareTo(T) is defined with non-nullable type.
            var call = Expression.Call(nonNullMember, compareTo, underlyingTypedConstant);
            var comparison = greaterThan
                ? Expression.GreaterThan(call, Expression.Constant(0))
                : Expression.LessThan(call, Expression.Constant(0));

            if (Nullable.GetUnderlyingType(memberType) is null)
                return comparison;

            // If Nullable, the comparison is only valid when HasValue is.
            var hasValue = Expression.Property(member, "HasValue");
            return Expression.AndAlso(hasValue, comparison);
        }
    }
}