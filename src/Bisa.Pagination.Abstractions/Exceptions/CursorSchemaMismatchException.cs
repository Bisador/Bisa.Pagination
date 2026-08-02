namespace Bisa.Pagination.Abstractions.Exceptions;
 
public sealed class CursorSchemaMismatchException()
    : PaginationException("The cursor structure does not match the current sort keys."); 