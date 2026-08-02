namespace Bisa.Pagination.Abstractions.Enums;

/// <summary> 
/// User decides the count should calculate or not
/// (Like when count is calculated before).
/// </summary>
public enum CountMode
{ 
    None = 0, 
    Compute = 1, 
    Provided = 2
}