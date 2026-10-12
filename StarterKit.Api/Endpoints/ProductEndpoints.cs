using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using StarterKit.Api.Data;
using StarterKit.Api.Domain;
using StarterKit.Api.Validation;

namespace StarterKit.Api.Endpoints;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/products")
            .WithTags("Products")
            .RequireAuthorization();

        group.MapGet("/", GetAll);
        group.MapGet("/{id:int}", GetById);
        group.MapPost("/", Create).AddEndpointFilter<ValidationFilter<ProductRequest>>();
        group.MapPut("/{id:int}", Update).AddEndpointFilter<ValidationFilter<ProductRequest>>();
        group.MapDelete("/{id:int}", Delete);

        return app;
    }

    private static async Task<Ok<PagedResponse<ProductResponse>>> GetAll(
        AppDbContext db, int page = 1, int pageSize = 20)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var total = await db.Products.CountAsync();
        var items = await db.Products
            .AsNoTracking()
            .OrderBy(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductResponse(p.Id, p.Name, p.Description, p.Price, p.CreatedAt))
            .ToListAsync();

        return TypedResults.Ok(new PagedResponse<ProductResponse>(items, page, pageSize, total));
    }

    private static async Task<Results<Ok<ProductResponse>, NotFound>> GetById(int id, AppDbContext db)
    {
        var p = await db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return p is null ? TypedResults.NotFound() : TypedResults.Ok(ToResponse(p));
    }

    private static async Task<Created<ProductResponse>> Create(ProductRequest req, AppDbContext db)
    {
        var product = new Product { Name = req.Name, Description = req.Description, Price = req.Price };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        return TypedResults.Created($"/api/products/{product.Id}", ToResponse(product));
    }

    private static async Task<Results<Ok<ProductResponse>, NotFound>> Update(
        int id, ProductRequest req, AppDbContext db)
    {
        var product = await db.Products.FindAsync(id);
        if (product is null) return TypedResults.NotFound();

        product.Name = req.Name;
        product.Description = req.Description;
        product.Price = req.Price;
        await db.SaveChangesAsync();

        return TypedResults.Ok(ToResponse(product));
    }

    private static async Task<Results<NoContent, NotFound>> Delete(int id, AppDbContext db)
    {
        var product = await db.Products.FindAsync(id);
        if (product is null) return TypedResults.NotFound();

        db.Products.Remove(product);
        await db.SaveChangesAsync();
        return TypedResults.NoContent();
    }

    private static ProductResponse ToResponse(Product p) =>
        new(p.Id, p.Name, p.Description, p.Price, p.CreatedAt);
}