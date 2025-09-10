using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering; // ✅ Needed for SelectList
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

using IfsahApp.Core.Enums;
using IfsahApp.Core.Models;
using IfsahApp.Core.ViewModels;
using IfsahApp.Hubs;
using IfsahApp.Infrastructure.Data;
using IfsahApp.Infrastructure.Services;
using IfsahApp.Utils;

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
                var recipients = await _context.Users
                    .Where(u => u.IsActive && u.Role == Role.Admin)
                    .Select(u => new { u.Id, u.Email, u.ADUserName })
                    .ToListAsync();

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

                // push to id group
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

                // also push to email and ADUserName groups
                await Task.WhenAll(notes.Select(n =>
                {
                    var r = recipients.FirstOrDefault(x => x.Id == n.RecipientId);
                    if (r == null) return Task.CompletedTask;

                    var payload = new
                    {
                        id = n.Id,
                        eventType = n.EventType,
                        message = n.Message,
                        createdAt = n.CreatedAt.ToString("u"),
                        url = Url.Action("Details", "Dashboard", new { id = disclosure.Id })
                    };

                    var tasks = new List<Task>
                    {
                        _hub.Clients.Group($"user-{r.Id}").SendAsync("Notify", payload)
                    };
                    if (!string.IsNullOrWhiteSpace(r.Email))
                        tasks.Add(_hub.Clients.Group($"user-{r.Email}").SendAsync("Notify", payload));
                    if (!string.IsNullOrWhiteSpace(r.ADUserName))
                        tasks.Add(_hub.Clients.Group($"user-{r.ADUserName}").SendAsync("Notify", payload));

                    return Task.WhenAll(tasks);
                }));

                return RedirectToAction("SubmitDisclosure", new { reportNumber = disclosure.DisclosureNumber });
            }

            // ❗❗ FIX: repopulate a proper SelectList when validation fails
            {
                var culture = Thread.CurrentThread.CurrentCulture.TwoLetterISOLanguageName;

                var disclosureTypes = await _context.DisclosureTypes
                    .Select(dt => new
                    {
                        dt.Id,
                        ArabicName  = dt.ArabicName  ?? dt.EnglishName, // safe fallback
                        EnglishName = dt.EnglishName ?? dt.ArabicName
                    })
                    .ToListAsync();

                var displayField = (culture == "ar") ? "ArabicName" : "EnglishName";
                ViewBag.DisclosureTypes = new SelectList(disclosureTypes, "Id", displayField, model.DisclosureTypeId);
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult SubmitDisclosure(string reportNumber)
        {
            ViewData["ReportNumber"] = reportNumber;
            return View();
        }
    }
}
