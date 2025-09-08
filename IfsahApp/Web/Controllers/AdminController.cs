using IfsahApp.Infrastructure.Data;
using IfsahApp.Core.Enums;
using IfsahApp.Core.Models;
using IfsahApp.Core.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace IfsahApp.Web.Controllers
{
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Admin/Assign/5
        public IActionResult Assign(int id)
        {
            var disclosureDetails = new DisclosureDetailsViewModel
            {
                Id = 1,
                Reference = "DISC-2025-0001",
                Type = "Security Breach",
                Date = new DateTime(2025, 9, 4, 14, 30, 0),
                Location = "Head Office - Floor 3",
                Status = DisclosureStatus.InReview,
                Description = "There was a suspected unauthorized access detected on the server.",

                Comments =
                [
                    new CommentViewModel
                    {
                        Text = "Initial investigation started.",
                        Author = "AdminUser1",
                        CreatedAt = DateTime.Now.AddDays(-2)
                    },
                    new CommentViewModel
                    {
                        Text = "Waiting for forensic analysis report.",
                        Author = "AdminUser2",
                        CreatedAt = DateTime.Now.AddDays(-1)
                    }
                ],

                NewComment = string.Empty,

                AssignToUserId = 3,

                AvailableUsers =
                [
                    new SelectListItem { Text = "User One", Value = "1" },
                    new SelectListItem { Text = "User Two", Value = "2" },
                    new SelectListItem { Text = "User Three", Value = "3" }
                ]
            };

            return View(disclosureDetails);
        }



        [HttpPost]
        public async Task<IActionResult> Assign(DisclosureDetailsViewModel model)
        {
            if (!ModelState.IsValid)
            {
                // لو فيه خطأ في البيانات، رجع الفورم مع نفس البيانات
                model.AvailableUsers = await _context.Users
                    .Select(u => new SelectListItem { Value = u.Id.ToString(), Text = u.ADUserName })
                    .ToListAsync();

                return View(model);
            }

            // جلب الـ Disclosure من الداتا بيز
            var disclosure = await _context.Disclosures
                .Include(d => d.Comments)
                .FirstOrDefaultAsync(d => d.Id == model.Id);

            if (disclosure == null)
            {
                return NotFound();
            }

            // إضافة تعليق جديد لو فيه نص
            if (!string.IsNullOrWhiteSpace(model.NewComment))
            {
                var newComment = new Comment
                {
                    DisclosureId = disclosure.Id,
                    Text = model.NewComment,
                    AuthorId = 1,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Comments.Add(newComment);
            }

            // تعيين المستخدم
            if (model.AssignToUserId.HasValue)
            {
                disclosure.AssignedToUserId = model.AssignToUserId.Value;
            }

            await _context.SaveChangesAsync();

            return RedirectToAction("Assign", new { id = disclosure.Id });
        }


    }
}
