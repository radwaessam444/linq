using System;
using System.Collections.Generic;
using System.Linq;

// --- Models ---
public enum OrderStatus { Pending, Shipped, Delivered, Cancelled }

public class Customer
{
    public int Id { get; set; }
    public string FullName { get; set; }
    public string Email { get; set; }
    public string City { get; set; }
    public DateTime RegisteredAt { get; set; }
    public List<Order> Orders { get; set; } = new();
    public List<Review> Reviews { get; set; } = new();
}

public class Order
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; }
    public DateTime OrderDate { get; set; }
    public OrderStatus Status { get; set; }
    public List<OrderItem> Items { get; set; } = new();
}

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Discount { get; set; } // 0.10 = 10%
}

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public int CategoryId { get; set; }
    public Category Category { get; set; }
    public List<OrderItem> OrderItems { get; set; } = new();
    public List<Review> Reviews { get; set; } = new();
}

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; }
    public List<Product> Products { get; set; } = new();
}

public class Review
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; }
    public int Rating { get; set; } // 1..5
    public string Comment { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ProductFilter
{
    public string SearchText { get; set; }
    public int? CategoryId { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public bool InStockOnly { get; set; }
    public string SortBy { get; set; } // "name", "price", "rating"
    public bool SortDescending { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class ProductSearchResultDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public string CategoryName { get; set; }
    public double AverageRating { get; set; }
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
}

class Program
{
    static void Main(string[] args)
    {
        // --- Mock Data Initialization ---
        var categories = new List<Category>
        {
            new Category { Id = 1, Name = "Laptops" },
            new Category { Id = 2, Name = "Accessories" }
        };

        var products = new List<Product>
        {
            new Product { Id = 1, Name = "Gaming Laptop", Price = 1200m, Stock = 10, CategoryId = 1, Category = categories[0] },
            new Product { Id = 2, Name = "Ultrabook", Price = 900m, Stock = 4, CategoryId = 1, Category = categories[0] },
            new Product { Id = 3, Name = "Wireless Mouse", Price = 25m, Stock = 2, CategoryId = 2, Category = categories[1] }
        };
        categories[0].Products = new List<Product> { products[0], products[1] };
        categories[1].Products = new List<Product> { products[2] };

        var customers = new List<Customer>
        {
            new Customer { Id = 1, FullName = "Alice Smith", Email = "alice@mail.com", City = "Cairo", RegisteredAt = DateTime.UtcNow.AddDays(-10) },
            new Customer { Id = 2, FullName = "Bob Jones", Email = "bob@mail.com", City = "Alexandria", RegisteredAt = DateTime.UtcNow.AddDays(-60) }
        };

        var orders = new List<Order>
        {
            new Order { Id = 1, CustomerId = 1, Customer = customers[0], OrderDate = new DateTime(2025, 5, 10), Status = OrderStatus.Delivered },
            new Order { Id = 2, CustomerId = 2, Customer = customers[1], OrderDate = new DateTime(2025, 6, 15), Status = OrderStatus.Pending }
        };
        customers[0].Orders = new List<Order> { orders[0] };
        customers[1].Orders = new List<Order> { orders[1] };

        var orderItems = new List<OrderItem>
        {
            new OrderItem { Id = 1, OrderId = 1, Order = orders[0], ProductId = 1, Product = products[0], Quantity = 1, UnitPrice = 1200m, Discount = 0.10m },
            new OrderItem { Id = 2, OrderId = 2, Order = orders[1], ProductId = 3, Product = products[2], Quantity = 5, UnitPrice = 25m, Discount = 0.0m }
        };
        orders[0].Items = new List<OrderItem> { orderItems[0] };
        orders[1].Items = new List<OrderItem> { orderItems[1] };
        products[0].OrderItems = new List<OrderItem> { orderItems[0] };
        products[2].OrderItems = new List<OrderItem> { orderItems[1] };

        var reviews = new List<Review>
        {
            new Review { Id = 1, ProductId = 1, Product = products[0], CustomerId = 1, Customer = customers[0], Rating = 5, Comment = "Great laptop!", CreatedAt = DateTime.UtcNow }
        };
        products[0].Reviews = reviews;
        customers[0].Reviews = reviews;


        // --- Level 1: Warm-up ---

        // 1. For every delivered order, return OrderId, CustomerName, and Total, ordered by Total descending.
        var q1 = orders
            .Where(o => o.Status == OrderStatus.Delivered)
            .Select(o => new {
                OrderId = o.Id,
                CustomerName = o.Customer.FullName,
                Total = o.Items.Sum(i => i.Quantity * i.UnitPrice * (1 - i.Discount))
            })
            .OrderByDescending(x => x.Total)
            .ToList();

        // 2. Count how many orders exist per status, with the percentage of the whole.
        var totalOrders = orders.Count;
        var q2 = orders
            .GroupBy(o => o.Status)
            .Select(g => new {
                Status = g.Key,
                Count = g.Count(),
                Percentage = totalOrders == 0 ? 0 : (double)g.Count() / totalOrders * 100
            })
            .ToList();

        // 3. Return products whose price is above the average price of their own category.
        var q3 = products
            .Where(p => p.Price > products
                .Where(p2 => p2.CategoryId == p.CategoryId)
                .Average(p2 => p2.Price))
            .ToList();

        // 4. Return products that were never ordered.
        var q4 = products
            .Where(p => !p.OrderItems.Any())
            .ToList();

        // 5. Return customers who registered in the last 30 days and have at least one order.
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
        var q5 = customers
            .Where(c => c.RegisteredAt >= thirtyDaysAgo && c.Orders.Any())
            .ToList();

        // 6. Return customers who cancelled more than 2 orders, with the number of cancelled orders.
        var q6 = customers
            .Where(c => c.Orders.Count(o => o.Status == OrderStatus.Cancelled) > 2)
            .Select(c => new {
                Customer = c,
                CancelledCount = c.Orders.Count(o => o.Status == OrderStatus.Cancelled)
            })
            .ToList();


        // --- Level 2: Grouping and aggregation ---

        // 7. Top 5 customers by total spending: name, order count, total spent, average order value.
        var q7 = customers
            .Select(c => new {
                Name = c.FullName,
                OrderCount = c.Orders.Count(o => o.Status == OrderStatus.Delivered),
                TotalSpent = c.Orders
                    .Where(o => o.Status == OrderStatus.Delivered)
                    .SelectMany(o => o.Items)
                    .Sum(i => i.Quantity * i.UnitPrice * (1 - i.Discount)),
                AvgOrderValue = c.Orders.Count(o => o.Status == OrderStatus.Delivered) == 0 ? 0 :
                    c.Orders
                        .Where(o => o.Status == OrderStatus.Delivered)
                        .Average(o => o.Items.Sum(i => i.Quantity * i.UnitPrice * (1 - i.Discount)))
            })
            .OrderByDescending(x => x.TotalSpent)
            .Take(5)
            .ToList();

        // 8. Monthly report for 2025: Year, Month, OrderCount, Revenue, ordered by month.
        var q8 = orders
            .Where(o => o.Status == OrderStatus.Delivered && o.OrderDate.Year == 2025)
            .GroupBy(o => new { o.OrderDate.Year, o.OrderDate.Month })
            .Select(g => new {
                Year = g.Key.Year,
                Month = g.Key.Month,
                OrderCount = g.Count(),
                Revenue = g.SelectMany(o => o.Items)
                           .Sum(i => i.Quantity * i.UnitPrice * (1 - i.Discount))
            })
            .OrderBy(x => x.Month)
            .ToList();

        // 9. Revenue per category, ordered by revenue descending.
        var q9 = categories
            .Select(c => new {
                CategoryName = c.Name,
                Revenue = c.Products
                    .SelectMany(p => p.OrderItems)
                    .Where(oi => oi.Order.Status == OrderStatus.Delivered)
                    .Sum(oi => oi.Quantity * oi.UnitPrice * (1 - oi.Discount))
            })
            .OrderByDescending(x => x.Revenue)
            .ToList();

        // 10. Count customers who never placed an order, grouped by city, ordered by count descending.
        var q10 = customers
            .Where(c => !c.Orders.Any())
            .GroupBy(c => c.City)
            .Select(g => new {
                City = g.Key,
                Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .ToList();

        // 11. Products with at least 10 reviews and an average rating of 4.5 or more: name, category, review count, average rating.
        var q11 = products
            .Where(p => p.Reviews.Count >= 10 && p.Reviews.Average(r => r.Rating) >= 4.5)
            .Select(p => new {
                Name = p.Name,
                Category = p.Category.Name,
                ReviewCount = p.Reviews.Count,
                AverageRating = p.Reviews.Average(r => (double)r.Rating)
            })
            .ToList();

        // 12. For each category, return the top 2 most expensive products.
        var q12 = categories
            .Select(c => new {
                Category = c.Name,
                TopProducts = c.Products
                    .OrderByDescending(p => p.Price)
                    .Take(2)
                    .Select(p => new { p.Name, p.Price })
                    .ToList()
            })
            .ToList();

        // 13. Products whose stock is lower than the total quantity in Pending orders. Show name, stock, and pending quantity.
        var q13 = products
            .Select(p => new {
                Name = p.Name,
                Stock = p.Stock,
                PendingQuantity = p.OrderItems
                    .Where(oi => oi.Order.Status == OrderStatus.Pending)
                    .Sum(oi => oi.Quantity)
            })
            .Where(x => x.Stock < x.PendingQuantity)
            .ToList();

        // 14. Customers whose total spending is above the average spending of all customers.
        var allCustomerSpendings = customers.ToDictionary(
            c => c.Id,
            c => c.Orders.Where(o => o.Status == OrderStatus.Delivered).SelectMany(o => o.Items).Sum(i => i.Quantity * i.UnitPrice * (1 - i.Discount))
        );
        var avgSpending = allCustomerSpendings.Values.Any() ? allCustomerSpendings.Values.Average() : 0;
        var q14 = customers
            .Where(c => allCustomerSpendings[c.Id] > avgSpending)
            .ToList();


        // --- Level 3: Joins, subqueries, set logic ---

        // 15. For each category, return its best-selling product by quantity. Categories with no sales must still appear.
        var q15 = categories
            .Select(c => new {
                CategoryName = c.Name,
                BestSellingProduct = c.Products
                    .Select(p => new {
                        Product = p,
                        TotalQty = p.OrderItems
                            .Where(oi => oi.Order.Status == OrderStatus.Delivered)
                            .Sum(oi => oi.Quantity)
                    })
                    .OrderByDescending(x => x.TotalQty)
                    .Select(x => x.Product.Name)
                    .FirstOrDefault()
            })
            .ToList();

        // 16. Customers who bought every product in the category "Laptops".
        var laptopProductIds = products
            .Where(p => p.Category.Name == "Laptops")
            .Select(p => p.Id)
            .ToList();

        var q16 = customers
            .Where(c => laptopProductIds.All(lpId =>
                c.Orders
                    .Where(o => o.Status == OrderStatus.Delivered)
                    .SelectMany(o => o.Items)
                    .Any(oi => oi.ProductId == lpId)
            ))
            .ToList();

        // 17. Reviews written by a customer for a product they never bought.
        var q17 = reviews
            .Where(r => !orders
                .Any(o => o.CustomerId == r.CustomerId &&
                          o.Status == OrderStatus.Delivered &&
                          o.Items.Any(i => i.ProductId == r.ProductId))
            )
            .ToList();

        // 18. Return all customers with their order count, including customers with zero orders (left join with GroupJoin).
        var q18 = customers
            .GroupJoin(
                orders,
                c => c.Id,
                o => o.CustomerId,
                (c, custOrders) => new {
                    CustomerName = c.FullName,
                    OrderCount = custOrders.Count()
                }
            )
            .ToList();

        // 19. The top 5 product pairs that appear together in the same order most often.
        var q19 = orderItems
            .Select(oi => new { oi.OrderId, oi.ProductId })
            .Distinct()
            .SelectMany(
                oi1 => orderItems
                    .Where(oi2 => oi2.OrderId == oi1.OrderId && oi2.ProductId > oi1.ProductId)
                    .Select(oi2 => new { Product1Id = oi1.ProductId, Product2Id = oi2.ProductId })
            )
            .GroupBy(p => new { p.Product1Id, p.Product2Id })
            .Select(g => new {
                Product1Id = g.Key.Product1Id,
                Product2Id = g.Key.Product2Id,
                Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .ToList();

        // 20. For each customer, return their name and their most recent order with each item's product name and quantity, in one query.
        var q20 = customers
            .Select(c => new {
                CustomerName = c.FullName,
                MostRecentOrder = c.Orders
                    .OrderByDescending(o => o.OrderDate)
                    .Select(o => new {
                        o.Id,
                        o.OrderDate,
                        Items = o.Items.Select(i => new {
                            ProductName = i.Product.Name,
                            i.Quantity
                        }).ToList()
                    })
                    .FirstOrDefault()
            })
            .ToList();

        // 21. For each customer, return their favorite category (the category with the highest total quantity bought).
        var q21 = customers
            .Select(c => new {
                CustomerName = c.FullName,
                FavoriteCategory = c.Orders
                    .Where(o => o.Status == OrderStatus.Delivered)
                    .SelectMany(o => o.Items)
                    .GroupBy(i => i.Product.Category.Name)
                    .Select(g => new {
                        CategoryName = g.Key,
                        TotalQty = g.Sum(i => i.Quantity)
                    })
                    .OrderByDescending(x => x.TotalQty)
                    .Select(x => x.CategoryName)
                    .FirstOrDefault()
            })
            .ToList();

        // 22. Rank products by revenue (1, 2, 3 ...) and return Rank, Product, Revenue.
        var q22 = products
            .Select(p => new {
                Product = p.Name,
                Revenue = p.OrderItems
                    .Where(oi => oi.Order.Status == OrderStatus.Delivered)
                    .Sum(oi => oi.Quantity * oi.UnitPrice * (1 - oi.Discount))
            })
            .OrderByDescending(x => x.Revenue)
            .ToList()
            .Select((x, index) => new {
                Rank = index + 1,
                x.Product,
                x.Revenue
            })
            .ToList();


        // --- Level 4: Real-world methods ---

        var filter = new ProductFilter { SearchText = "Laptop", PageNumber = 1, PageSize = 10 };
        var q23Result = SearchProducts(products, filter);

        // Q24 simulation (In-memory update)
        foreach (var p in products.Where(p => p.Category.Name == "Accessories" && p.Stock < 5))
        {
            p.Price *= 1.10m;
        }

        // Q25 simulation (In-memory delete)
        var oneYearAgo = DateTime.UtcNow.AddYears(-1);
        orders.RemoveAll(o => o.Status == OrderStatus.Cancelled && o.OrderDate < oneYearAgo);


        // --- Bonus (a) ---
        Console.WriteLine("All 25 LINQ questions executed successfully with zero package installations!");
    }

    public static PagedResult<ProductSearchResultDto> SearchProducts(List<Product> products, ProductFilter f)
    {
        var query = products.AsQueryable();

        if (!string.IsNullOrWhiteSpace(f.SearchText))
        {
            query = query.Where(p => p.Name.Contains(f.SearchText, StringComparison.OrdinalIgnoreCase));
        }

        if (f.CategoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == f.CategoryId.Value);
        }

        if (f.MinPrice.HasValue)
        {
            query = query.Where(p => p.Price >= f.MinPrice.Value);
        }

        if (f.MaxPrice.HasValue)
        {
            query = query.Where(p => p.Price <= f.MaxPrice.Value);
        }

        if (f.InStockOnly)
        {
            query = query.Where(p => p.Stock > 0);
        }

        var totalCount = query.Count();

        query = f.SortBy?.ToLower() switch
        {
            "price" => f.SortDescending ? query.OrderByDescending(p => p.Price) : query.OrderBy(p => p.Price),
            "rating" => f.SortDescending ? query.OrderByDescending(p => p.Reviews.Any() ? p.Reviews.Average(r => r.Rating) : 0) : query.OrderBy(p => p.Reviews.Any() ? p.Reviews.Average(r => r.Rating) : 0),
            _ => f.SortDescending ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name)
        };

        var items = query
            .Skip((f.PageNumber - 1) * f.PageSize)
            .Take(f.PageSize)
            .Select(p => new ProductSearchResultDto
            {
                Id = p.Id,
                Name = p.Name,
                Price = p.Price,
                Stock = p.Stock,
                CategoryName = p.Category.Name,
                AverageRating = p.Reviews.Any() ? p.Reviews.Average(r => (double)r.Rating) : 0
            })
            .ToList();

        return new PagedResult<ProductSearchResultDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = f.PageNumber,
            PageSize = f.PageSize
        };
    }
}