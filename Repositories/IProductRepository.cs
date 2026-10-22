using NhuBinh_2921.Models;

namespace NhuBinh_2921.Repositories;

public interface IProductRepository
{
    Task<IEnumerable<Product>> GetProductsAsync();
    Task<Product?> GetProductByIdAsync(int id);
    Task<IEnumerable<Product>> SearchProductsByNameAsync(string name);
    Task AddProductAsync(Product product);
    Task<bool> UpdateProductAsync(Product product);
    Task<bool> DeleteProductAsync(int id);
}
