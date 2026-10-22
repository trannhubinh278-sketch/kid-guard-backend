using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NhuBinh_2921.Models;
using NhuBinh_2921.Repositories;

namespace NhuBinh_2921.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ProductApiController : ControllerBase
{
    private readonly IProductRepository _productRepository;
    private readonly ApplicationDbContext _context;

    public ProductApiController(IProductRepository productRepository, ApplicationDbContext context)
    {
        _productRepository = productRepository;
        _context = context;
    }

    [Authorize(Roles = "Admin,User")]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Product>>> GetProducts() =>
        Ok(await _productRepository.GetProductsAsync());

    [Authorize(Roles = "Admin,User")]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<Product>> GetProductById(int id)
    {
        var product = await _productRepository.GetProductByIdAsync(id);
        return product == null ? NotFound() : Ok(product);
    }

    [Authorize(Roles = "Admin,User")]
    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<Product>>> Search([FromQuery] string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(new { message = "Vui lòng nhập tên sản phẩm cần tìm." });

        return Ok(await _productRepository.SearchProductsByNameAsync(name.Trim()));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<Product>> AddProduct(Product product)
    {
        if (product.CategoryId.HasValue &&
            !await _context.Categories.AnyAsync(c => c.Id == product.CategoryId.Value))
            return BadRequest(new { message = "Danh mục không tồn tại." });

        product.Id = 0;
        product.Category = null;
        await _productRepository.AddProductAsync(product);
        return CreatedAtAction(nameof(GetProductById), new { id = product.Id }, product);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateProduct(int id, Product product)
    {
        if (id != product.Id)
            return BadRequest(new { message = "Id trên URL không khớp dữ liệu." });
        if (product.CategoryId.HasValue &&
            !await _context.Categories.AnyAsync(c => c.Id == product.CategoryId.Value))
            return BadRequest(new { message = "Danh mục không tồn tại." });

        return await _productRepository.UpdateProductAsync(product) ? NoContent() : NotFound();
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteProduct(int id) =>
        await _productRepository.DeleteProductAsync(id) ? NoContent() : NotFound();
}
