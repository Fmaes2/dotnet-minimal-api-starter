namespace StarterKit.Api.Endpoints;

public record ProductRequest(string Name, string? Description, decimal Price);

public record ProductResponse(int Id, string Name, string? Description, decimal Price, DateTime CreatedAt);

public record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);