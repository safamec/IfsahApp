using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using IfsahApp.Utils;                 // TempData.Get/Set extensions
using IfsahApp.Core.Enums;            // PeopleType
using IfsahApp.Core.ViewModels;       // PeopleViewModel

namespace IfsahApp.Web.Controllers
{
    [AutoValidateAntiforgeryToken]
    public class PeopleController : Controller
    {
        private readonly ILogger<PeopleController> _logger;
        public PeopleController(ILogger<PeopleController> logger) => _logger = logger;

        private const string TempDataSuspectedKey = "SuspectedPersons";
        private const string TempDataRelatedKey = "RelatedPersons";

        // GET: /People/Create?peopletype=Suspected|Related
        [HttpGet]
        [IgnoreAntiforgeryToken]
        public IActionResult Create(PeopleType peopletype = PeopleType.Related)
        {
            var vm = new PeopleViewModel { Type = peopletype };
            return PartialView("Create", vm); // Views/People/Create.cshtml
        }

        // POST: /People/Create (AJAX JSON)
        [HttpPost]
        public IActionResult Create(PeopleViewModel model)
        {
            // 1) FORCE the type from the query if present
            PeopleType type;
            var rawType = Request.Query["peopletype"].ToString();
            if (!string.IsNullOrWhiteSpace(rawType) &&
                Enum.TryParse<PeopleType>(rawType, true, out var parsed))
            {
                type = parsed;
                model.Type = parsed; // keep model consistent
            }
            else
            {
                // fallback to posted model
                type = model.Type;
            }

            // 2) Validate
            if (!ModelState.IsValid)
            {
                return Json(new
                {
                    ok = false,
                    errors = ModelState.ToDictionary(
                        kv => kv.Key,
                        kv => kv.Value?.Errors?.FirstOrDefault()?.ErrorMessage
                    )
                });
            }

            // 3) Persist to TempData (optional, helpful when navigating back/forward)
            var key = (type == PeopleType.Suspected) ? "SuspectedPersons" : "RelatedPersons";
            var list = TempData.Get<List<PeopleViewModel>>(key) ?? new List<PeopleViewModel>();
            list.Add(model);
            TempData.Set(key, list);
            TempData.Keep(key);

            // 4) CRITICAL: prefix must match the type we just determined
            var prefix = (type == PeopleType.Suspected) ? "SuspectedPersons" : "RelatedPersons";
            var index = list.Count - 1;

            return Json(new
            {
                ok = true,
                prefix,
                index,
                person = new
                {
                    name = model.Name ?? string.Empty,
                    email = model.Email ?? string.Empty,
                    phone = model.Phone ?? string.Empty,
                    organization = model.Organization ?? string.Empty
                }
            });
        }
    }
}