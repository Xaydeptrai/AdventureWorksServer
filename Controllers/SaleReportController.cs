using AdventureWorksServer.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace AdventureWorksServer.Controllers
{
    public class SaleReportController : Controller
    {
        private readonly AdventureWorks2022Context _context;

        public SaleReportController(AdventureWorks2022Context context)
        {
            _context = context;
        }

        [HttpGet("sales-percentage-by-year")]
        public IActionResult GetSalesPercentageByCategoryAndYear(int? year)
        {
            var stopwatch = Stopwatch.StartNew();

            // Tính tổng doanh số theo năm hoặc tổng tất cả nếu year = null
            var totalSalesQuery = _context.SalesOrderHeaders.AsQueryable();
            if (year.HasValue)
            {
                totalSalesQuery = totalSalesQuery.Where(soh => soh.OrderDate.Year == year.Value);
            }
            var totalSales = totalSalesQuery.Sum(soh => soh.TotalDue);

            // Tính phần trăm doanh số theo danh mục
            var salesQuery = _context.SalesOrderDetails
                .Join(_context.Products,
                      sod => sod.ProductId,
                      p => p.ProductId,
                      (sod, p) => new { sod, p })
                .Join(_context.ProductSubcategories,
                      sp => sp.p.ProductSubcategoryId,
                      ps => ps.ProductSubcategoryId,
                      (sp, ps) => new { sp.sod, sp.p, ps })
                .Join(_context.ProductCategories,
                      sps => sps.ps.ProductCategoryId,
                      pc => pc.ProductCategoryId,
                      (sps, pc) => new { sps.sod, sps.p, sps.ps, pc })
                .Join(_context.SalesOrderHeaders,
                      sps => sps.sod.SalesOrderId,
                      soh => soh.SalesOrderId,
                      (sps, soh) => new { sps.sod, sps.p, sps.ps, sps.pc, soh.OrderDate });

            // Lọc theo năm nếu year có giá trị
            if (year.HasValue)
            {
                salesQuery = salesQuery.Where(joined => joined.OrderDate.Year == year.Value);
            }

            var salesPercentageByCategory = salesQuery
                .GroupBy(joined => joined.pc.Name)
                .Select(group => new
                {
                    Category = group.Key,
                    TotalSales = group.Sum(item => item.sod.LineTotal),
                    SalesPercentage = totalSales > 0 ? group.Sum(item => item.sod.LineTotal) / totalSales * 100 : 0
                })
                .OrderBy(result => result.Category)
                .ToList();

            stopwatch.Stop();

            var executionTime = stopwatch.ElapsedMilliseconds;

            return Ok(new
            {
                Year = year,
                ExecutionTimeMs = executionTime,
                Data = salesPercentageByCategory
            });
        }

        [HttpGet("top-products-by-revenue")]
        public IActionResult GetTopProductsByRevenue(int? year)
        {
            var stopwatch = Stopwatch.StartNew();

            // Truy vấn cơ bản
            var productRevenueQuery = _context.SalesOrderDetails
                .Join(_context.Products,
                      sod => sod.ProductId,
                      p => p.ProductId,
                      (sod, p) => new { sod, p })
                .Join(_context.SalesOrderHeaders,
                      sp => sp.sod.SalesOrderId,
                      soh => soh.SalesOrderId,
                      (sp, soh) => new { sp.sod, sp.p, soh.OrderDate });

            // Lọc theo năm nếu year có giá trị
            if (year.HasValue)
            {
                productRevenueQuery = productRevenueQuery.Where(joined => joined.OrderDate.Year == year.Value);
            }

            // Tính tổng doanh thu theo sản phẩm và lấy top 10
            var topProducts = productRevenueQuery
                .GroupBy(joined => new { joined.p.ProductId, joined.p.Name })
                .Select(group => new
                {
                    ProductName = group.Key.Name,
                    TotalRevenue = group.Sum(item => item.sod.LineTotal)
                })
                .OrderByDescending(result => result.TotalRevenue)
                .Take(10)
                .ToList();

            stopwatch.Stop();

            return Ok(new
            {
                Year = year,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                TopProducts = topProducts
            });
        }

        [HttpGet("yearly-sales-by-city-and-category")]
        public IActionResult GetYearlySalesByCityAndCategory(int? year)
        {
            // Đo thời gian thực thi
            var stopwatch = Stopwatch.StartNew();

            // Lấy doanh thu của cả năm theo thành phố và danh mục sản phẩm
            var result = _context.SalesOrderDetails
                .Join(_context.SalesOrderHeaders,
                      sod => sod.SalesOrderId,
                      soh => soh.SalesOrderId,
                      (sod, soh) => new { sod, soh })
                .Join(_context.Products,
                      sod_soh => sod_soh.sod.ProductId,
                      p => p.ProductId,
                      (sod_soh, p) => new { sod_soh.sod, sod_soh.soh, p })
                .Join(_context.ProductSubcategories,
                      sp => sp.p.ProductSubcategoryId,
                      ps => ps.ProductSubcategoryId,
                      (sp, ps) => new { sp.sod, sp.soh, sp.p, ps })
                .Join(_context.ProductCategories,
                      sps => sps.ps.ProductCategoryId,
                      pc => pc.ProductCategoryId,
                      (sps, pc) => new { sps.sod, sps.soh, sps.p, sps.ps, pc })
                .Join(_context.Addresses,
                      soh => soh.soh.ShipToAddressId,
                      addr => addr.AddressId,
                      (soh, addr) => new { soh.sod, soh.soh, soh.p, soh.ps, soh.pc, addr.City })
                .Where(joined => year == null || joined.soh.OrderDate.Year == year) // Lọc theo năm nếu có
                .GroupBy(joined => new
                {
                    joined.City,
                    joined.pc.Name
                })
                .Select(group => new
                {
                    City = group.Key.City,
                    Category = group.Key.Name,
                    TotalRevenue = group.Sum(item => item.sod.LineTotal)
                })
                .ToList();

            // Lọc ra 10 thành phố có doanh thu cao nhất
            var topCities = result
                .GroupBy(r => r.City)
                .Select(group => new
                {
                    City = group.Key,
                    TotalCityRevenue = group.Sum(item => item.TotalRevenue)
                })
                .OrderByDescending(r => r.TotalCityRevenue)
                .Take(10)
                .Select(r => r.City)
                .ToList();

            // Lọc lại kết quả chỉ chứa 10 thành phố có doanh thu cao nhất
            var filteredResult = result
                .Where(r => topCities.Contains(r.City))
                .OrderBy(r => r.City)
                .ThenBy(r => r.Category)
                .ToList();

            // Dừng Stopwatch
            stopwatch.Stop();

            // Trả về kết quả kèm thời gian thực thi
            return Ok(new
            {
                Year = year,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                TopCities = topCities,
                SalesData = filteredResult
            });
        }

        [HttpGet("total-sales-year")]
        public IActionResult GetTotalSalesInYear(int? year)
        {
            var stopwatch = Stopwatch.StartNew();

            var totalSales = _context.SalesOrderHeaders
                .Where(soh => year == null || soh.OrderDate.Year == year)
                .Sum(soh => soh.TotalDue);

            // Dừng stopwatch
            stopwatch.Stop();

            return Ok(new
            {
                Year = year,
                TotalSales = totalSales,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds
            });
        }

    }
}
