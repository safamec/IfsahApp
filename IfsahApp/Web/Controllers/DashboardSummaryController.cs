using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;  // <-- Add this for [AllowAnonymous]
using IfsahApp.Core.Enums;
using IfsahApp.Core.ViewModels;
using IfsahApp.Core.Models;
using IfsahApp.Infrastructure.Data;

namespace IfsahApp.Web.Controllers
{
    public class DashboardSummaryController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardSummaryController(ApplicationDbContext context)
        {
            _context = context;
        }

        [AllowAnonymous]  
        public IActionResult Index()
        {
            // Group disclosures by month (based on IncidentStartDate) and aggregate counts
            var summaryData = _context.Disclosures
                .Where(d => d.IncidentStartDate.HasValue)  // filter out null IncidentStartDate
                .AsEnumerable()
                .GroupBy(d => new { Year = d.IncidentStartDate.Value.Year, Month = d.IncidentStartDate.Value.Month })
                .Select(g => new DashboardSummaryViewModel
                {
                    Month = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMMM yyyy"),
                    NumberOfDisclosures = g.Count(),
                    DisclosuresUnderReview = g.Count(d => d.Status == DisclosureStatus.InReview),
                    DisclosuresInProcess = g.Count(d => d.Status == DisclosureStatus.Assigned),
                    CancelledDisclosures = g.Count(d => d.Status == DisclosureStatus.Completed)
                })
                .OrderBy(vm => DateTime.ParseExact(vm.Month, "MMMM yyyy", null))
                .ToList();

            return View(summaryData);
        }
    }
}
