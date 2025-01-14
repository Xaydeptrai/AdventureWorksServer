using AdventureWorksServer.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace AdventureWorksServer.Controllers
{
    public class ProductionReportController : Controller
    {
        private readonly AdventureWorks2022Context _context;

        public ProductionReportController(AdventureWorks2022Context context)
        {
            _context = context;
        }

        [HttpGet("production-cost-by-location")]
        public IActionResult GetProductionCostByAssemblyLocation(int? year)
        {
            // Measure execution time
            var stopwatch = Stopwatch.StartNew();

            // Query to calculate production cost by assembly location
            var productionCostByLocation = _context.WorkOrders
                .Join(_context.WorkOrderRoutings,
                      wo => wo.WorkOrderId,
                      wor => wor.WorkOrderId,
                      (wo, wor) => new { wo, wor })
                .Join(_context.Locations,
                      wow => wow.wor.LocationId,
                      l => l.LocationId,
                      (wow, l) => new { wow.wo, wow.wor, l })
                .Where(joined => year == null || joined.wo.DueDate.Year == year) // Filter by year if provided
                .GroupBy(joined => joined.l.Name) // Group by location name
                .Select(group => new
                {
                    Location = group.Key,
                    TotalProductionCost = group.Sum(item => item.wor.ActualCost) // Sum ActualCost
                })
                .OrderByDescending(result => result.TotalProductionCost)
                .ToList();

            // Stop stopwatch
            stopwatch.Stop();

            return Ok(new
            {
                Year = year,
                ProductionCosts = productionCostByLocation,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds
            });
        }

        [HttpGet("products-completed-by-year")]
        public IActionResult GetProductsCompletedByYear()
        {
            var completedProducts = _context.WorkOrders
                .Where(wo => wo.EndDate != null) // Chỉ lấy các WorkOrder đã hoàn thành
                .GroupBy(wo => wo.EndDate.Value.Year)
                .Select(group => new
                {
                    Year = group.Key,
                    TotalCompletedProducts = group.Sum(wo => wo.OrderQty)
                })
                .OrderBy(result => result.Year)
                .ToList();

            return Ok(completedProducts);
        }

        [HttpGet("production-efficiency-by-year")]
        public IActionResult GetProductionEfficiencyByYear()
        {
            var efficiency = _context.WorkOrders
                .Where(wo => wo.StartDate != null && wo.EndDate != null)
                .GroupBy(wo => wo.EndDate.Value.Year)
                .Select(group => new
                {
                    Year = group.Key,
                    AverageActualDuration = group.Average(wo => EF.Functions.DateDiffDay(wo.StartDate, wo.EndDate)),
                    AveragePlannedDuration = group.Average(wo => EF.Functions.DateDiffDay(wo.StartDate, wo.DueDate))
                })
                .OrderBy(result => result.Year)
                .ToList();

            return Ok(efficiency);
        }

        [HttpGet("defective-products-rate")]
        public IActionResult GetDefectiveProductsRate()
        {
            var defectiveRate = _context.WorkOrders
                .GroupBy(wo => wo.ScrapReasonId != null)
                .Select(group => new
                {
                    IsDefective = group.Key,
                    TotalProducts = group.Sum(wo => wo.OrderQty)
                })
                .ToList();

            var totalProducts = defectiveRate.Sum(r => r.TotalProducts);
            var defectives = defectiveRate.FirstOrDefault(r => r.IsDefective)?.TotalProducts ?? 0;

            return Ok(new
            {
                TotalProducts = totalProducts,
                DefectiveProducts = defectives,
                DefectiveRatePercentage = (defectives / (double)totalProducts) * 100
            });
        }

        [HttpGet("top-10-products-produced")]
        public IActionResult GetTop10ProductsProduced(int? year = null)
        {
            // Đo thời gian thực thi
            var stopwatch = Stopwatch.StartNew();

            // Query cơ bản với điều kiện lọc nếu có năm
            var query = _context.WorkOrders
                .Join(_context.Products,
                      wo => wo.ProductId,
                      p => p.ProductId,
                      (wo, p) => new { wo, p });

            if (year.HasValue)
            {
                query = query.Where(joined => joined.wo.DueDate.Year == year.Value);
            }

            // Thống kê top 10 sản phẩm được sản xuất nhiều nhất
            var topProducts = query
                .GroupBy(joined => new { joined.p.Name, joined.p.ProductNumber })
                .Select(group => new
                {
                    group.Key.Name,
                    group.Key.ProductNumber,
                    TotalProduced = group.Sum(item => item.wo.OrderQty)
                })
                .OrderByDescending(result => result.TotalProduced)
                .Take(10)
                .ToList();

            // Dừng stopwatch
            stopwatch.Stop();

            // Trả về kết quả kèm thời gian thực thi
            return Ok(new
            {
                Year = year.HasValue ? year.Value.ToString() : "All Years",
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                TopProducts = topProducts
            });
        }

    }
}
