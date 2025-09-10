using Microsoft.AspNetCore.Mvc;
using IfsahApp.Core.Models;
using IfsahApp.Infrastructure.Data;
using IfsahApp.Core.ViewModels;
using IfsahApp.Infrastructure.Services;
using AutoMapper;
using IfsahApp.Utils;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using IfsahApp.Hubs;
using IfsahApp.Core.Enums;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace IfsahApp.Web.Controllers
{
    public class DisclosureController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IEnumLocalizer _enumLocalizer;
        private readonly IMapper _mapper;
        private readonly IHubContext<NotificationHub> _hub; // notifications hub

        public DisclosureController(
            ApplicationDbContext context,
            IWebHostEnvironment env,
            IEnumLocalizer enumLocalizer,
            IMapper mapper,
            IHubContext<NotificationHub> hub)
        {
            _context = context;
            _env = env;
            _enumLocalizer = enumLocalizer;
            _mapper = mapper;
            _hub = hub;
        }

    [HttpGet]
    public IActionResult Create()
    {
        var culture = Thread.CurrentThread.CurrentCulture.TwoLetterISOLanguageName;

        var disclosureTypes = _context.DisclosureTypes.ToList();

        // Choose display field based on culture
        string displayField = culture == "ar" ? "ArabicName" : "EnglishName";

        ViewBag.DisclosureTypes = new SelectList(disclosureTypes, "Id", displayField);

        return View(new DisclosureFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(DisclosureFormViewModel model)
    {
        if (ModelState.IsValid)
        {
            var disclosure = _mapper.Map<Disclosure>(model);
            disclosure.DisclosureNumber = DisclosureNumberGeneratorHelper.Generate();
            disclosure.SubmittedById = 1;

            disclosure.SuspectedPeople ??= new List<SuspectedPerson>();
            disclosure.RelatedPeople ??= new List<RelatedPerson>();

            if (model.SuspectedPersons != null)
            {
                foreach (var suspected in model.SuspectedPersons)
                    disclosure.SuspectedPeople.Add(suspected);
            }

            if (model.RelatedPersons != null)
            {
                foreach (var related in model.RelatedPersons)
                    disclosure.RelatedPeople.Add(related);
            }

            if (model.Attachments != null && model.Attachments.Count > 0)
            {
                disclosure.Attachments ??= new List<DisclosureAttachment>();

                foreach (var file in model.Attachments)
                {
                    var (savedFileName, error) = await FilePathHelper.SaveFileAsync(file, _env);

                    if (savedFileName == null)
                    {
                        ModelState.AddModelError("Attachments", error ?? "Unknown error while saving the file.");
                        continue;
                    }

                    var extension = Path.GetExtension(savedFileName).TrimStart('.');

                    disclosure.Attachments.Add(new DisclosureAttachment
                    {
                        FileName = savedFileName,
                        FileType = extension,
                        FileSize = file.Length
                    });
                }
            }

                _context.Disclosures.Add(disclosure);
                await _context.SaveChangesAsync();

                // -------------------------------
                // Notifications
                // -------------------------------
                // 1) choose recipients (here: )
                var recipients = await _context.Users
                    .Where(u => u.IsActive && u.Role == Role.Admin)
                    .Select(u => new { u.Id, u.Email })
                    .ToListAsync();

                // 2) create rows
                var notes = recipients.Select(r => new Notification
                {
                    RecipientId = r.Id,
                    EventType   = "Disclosure",
                    Message     = $"New disclosure {disclosure.DisclosureNumber} created",
                    EmailAddress = r.Email,
                    IsRead      = false,
                    CreatedAt   = DateTime.UtcNow
                }).ToList();

                _context.Notifications.AddRange(notes);
                await _context.SaveChangesAsync();

                // 3) live push via SignalR (by numeric id group)
                await Task.WhenAll(notes.Select(n =>
                    _hub.Clients.Group($"user-{n.RecipientId}")
                        .SendAsync("Notify", new
                        {
                            id = n.Id,
                            eventType = n.EventType,
                            message = n.Message,
                            createdAt = n.CreatedAt.ToString("u"),
                            url = Url.Action("Details", "Dashboard", new { id = disclosure.Id })
                        })
                ));

                // --- ADDED: also push to email and ADUserName groups so clients joined by those identifiers receive it ---
                var recipientKeys = await _context.Users
                    .Where(u => recipients.Select(r => r.Id).Contains(u.Id))
                    .Select(u => new { u.Id, u.Email, u.ADUserName })
                    .ToListAsync();

                await Task.WhenAll(notes.Select(async n =>
                {
                    var r = recipientKeys.FirstOrDefault(x => x.Id == n.RecipientId);
                    if (r == null) return;

                    var payload = new
                    {
                        id = n.Id,
                        eventType = n.EventType,
                        message = n.Message,
                        createdAt = n.CreatedAt.ToString("u"),
                        url = Url.Action("Details", "Dashboard", new { id = disclosure.Id })
                    };

                    var tasks = new List<Task>();
                    if (!string.IsNullOrWhiteSpace(r.Email))
                        tasks.Add(_hub.Clients.Group($"user-{r.Email}").SendAsync("Notify", payload));
                    if (!string.IsNullOrWhiteSpace(r.ADUserName))
                        tasks.Add(_hub.Clients.Group($"user-{r.ADUserName}").SendAsync("Notify", payload));

                    await Task.WhenAll(tasks);
                }));
                // --- /ADDED ---
                // -------------------------------

            return RedirectToAction("SubmitDisclosure", new { reportNumber = disclosure.DisclosureNumber });
        }

        ViewBag.DisclosureTypes = _context.DisclosureTypes.ToList();
        return View(model);
    }

    [HttpGet]
    public IActionResult SubmitDisclosure(string reportNumber)
    {
        ViewData["ReportNumber"] = reportNumber;
        return View();
    }
}

