namespace Bisa.Pagination.Abstractions.Exceptions;
 
public sealed class TamperedCursorException() : PaginationException("Cursor is tampered (invalid signature)."); 