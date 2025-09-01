using Microsoft.AspNetCore.Mvc;
using YourProjectNamespace.Models;

namespace YourProjectNamespace.Controllers
{
    public class ReviewController : Controller
    {
        public IActionResult Index()
        {
            var cases = new List<CaseItem>
            {
                new CaseItem
                {
                    Type = "Financial Misconduct",
                    Reference = "VR-1704567890-abc123def",
                    Date = new DateTime(2024, 12, 15),
                    Location = "Finance Department, Building A",
                    Status = "Under Review",
                    Description = "Suspected unauthorized financial transactions in Q4 2024"
                },
                new CaseItem
                {
                    Type = "Policy Breach",
                    Reference = "VR-1704567894-mno234pqr",
                    Date = new DateTime(2024, 12, 16),
                    Location = "IT Department",
                    Status = "Under Review",
                    Description = "Company policy violations regarding data handling procedures"
                }
            };

            return View(cases);
        }
    }
}
