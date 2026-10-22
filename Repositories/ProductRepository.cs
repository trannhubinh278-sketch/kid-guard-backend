using Microsoft.EntityFrameworkCore;
using NhuBinh_2921.Models;

namespace NhuBinh_2921.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly ApplicationDbContext _context;

    public ProductRepository(ApplicationDbContext context) => _context = context;

    public async Task<IEnumerable<Product>> GetProductsAsync() =>
        await _context.Products.AsNoTracking().Include(p => p.Category)
            .OrderBy(p => p.Id).ToListAsync();

    public async Task<Product?> GetProductByIdAsync(int id) =>
        await _context.Products.AsNoTracking().Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == id);

    public async Task<IEnumerable<Product>> SearchProductsByNameAsync(string name) =>
        await _context.Products.AsNoTracking().Include(p => p.Category)
            .Where(p => p.Name != null && p.Name.Contains(name))
            .OrderBy(p => p.Name).ToListAsync();

    public async Task AddProductAsync(Product product)
    {
        _context.Products.Add(product);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> UpdateProductAsync(Product product)
    {
        if (!await _context.Products.AnyAsync(p => p.Id == product.Id))
            return false;

        product.Category = null;
        _context.Entry(product).State = EntityState.Modified;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteProductAsync(int id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null)
            return false;

        _context.Products.Remove(product);
        await _context.SaveChangesAsync();
        return true;
    }
}
