namespace Bisa.Pagination.Abstractions.Exceptions;
 
public sealed class ExpiredCursorException() : PaginationException("Cursor is expired.");