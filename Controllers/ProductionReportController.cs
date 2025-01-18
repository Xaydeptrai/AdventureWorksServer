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
                .Where(wo => wo.EndDate != null)
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
        public IActionResult GetDefectiveProductsRate([FromQuery] int? year)
        {
            var defectiveRateQuery = _context.WorkOrders
                .Where(wo => !year.HasValue || wo.StartDate.Year == year.Value) // Lọc theo năm nếu có
                .GroupBy(wo => new
                {
                    Year = wo.StartDate.Year,
                    IsDefective = wo.ScrapReasonId != null
                })
                .Select(group => new
                {
                    Year = group.Key.Year,
                    IsDefective = group.Key.IsDefective,
                    TotalProducts = group.Sum(wo => wo.OrderQty)
                });

            var defectiveRateByYear = defectiveRateQuery.ToList();

            var defectiveRateSummary = defectiveRateByYear
                .GroupBy(r => r.Year)
                .Select(group => new
                {
                    Year = group.Key,
                    TotalProducts = group.Sum(r => r.TotalProducts),
                    DefectiveProducts = group.FirstOrDefault(r => r.IsDefective)?.TotalProducts ?? 0,
                    DefectiveRatePercentage = (group.FirstOrDefault(r => r.IsDefective)?.TotalProducts ?? 0) /
                                               (double)group.Sum(r => r.TotalProducts) * 100
                })
                .ToList();

            return Ok(defectiveRateSummary);
        }


        [HttpGet("top-10-products-produced")]
        public IActionResult GetTop10ProductsProduced(int? year = null)
        {
            var stopwatch = Stopwatch.StartNew();

            var query = _context.WorkOrders
                .Join(_context.Products,
                      wo => wo.ProductId,
                      p => p.ProductId,
                      (wo, p) => new { wo, p });

            if (year.HasValue)
            {
                query = query.Where(joined => joined.wo.DueDate.Year == year.Value);
            }

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

            stopwatch.Stop();

            return Ok(new
            {
                Year = year.HasValue ? year.Value.ToString() : "All Years",
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                TopProducts = topProducts
            });
        }

    }
}
