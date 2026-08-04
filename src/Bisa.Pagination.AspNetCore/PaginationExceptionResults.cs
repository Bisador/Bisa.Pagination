using Bisa.Pagination.Abstractions;
using Bisa.Pagination.Abstractions.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bisa.Pagination.AspNetCore;

/// <summary>
/// Helper to turn cursor exceptions (invalid/manipulated/expired) into a response
/// HTTP 400 with standard ProblemDetails format, instead of returning 500.
/// In Minimal APIs: <c>catch (PaginationException ex) { return ex.ToProblemResult(); }</c>
/// In MVC Controllers, you can put the same logic inside an ExceptionFilter.
/// </summary>
public static class PaginationExceptionResults
{
    public static IResult ToProblemResult(this PaginationException exception) => exception switch
    {
        InvalidCursorException => Results.Problem(title: "Cursor is invalid", detail: exception.Message, statusCode: StatusCodes.Status400BadRequest),
        TamperedCursorException => Results.Problem(title: "Cursor has been tampered with", detail: exception.Message, statusCode: StatusCodes.Status400BadRequest),
        ExpiredCursorException => Results.Problem(title:"Cursor is expired", detail: exception.Message, statusCode: StatusCodes.Status400BadRequest),
        CursorSchemaMismatchException => Results.Problem(title:"Cursor structure is inconsistent", detail: exception.Message, statusCode: StatusCodes.Status400BadRequest),
        _ => Results.Problem(title: "خطای صفحه‌بندی", detail: exception.Message, statusCode: StatusCodes.Status400BadRequest)
    };
}
