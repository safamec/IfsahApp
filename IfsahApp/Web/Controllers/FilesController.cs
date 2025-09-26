// FilesController.cs
using IfsahApp.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using System.IO;

[Route("Files")]
public class FilesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _env;

    public FilesController(ApplicationDbContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    [HttpGet("Download/{id:int}")]
    public async Task<IActionResult> Download(int id)
    {
        var file = await _context.DisclosureAttachments
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id);

        if (file == null)
            return NotFound();

        // 1) Resolve path robustly:
        //    - If FullPath is absolute, use it.
        //    - If FullPath is relative (e.g., "uploads/abc.pdf"), map under wwwroot.
        //    - Otherwise fall back to wwwroot/uploads/<FileName>
        string? resolved = null;

        if (!string.IsNullOrWhiteSpace(file.FullPath))
        {
            resolved = Path.IsPathRooted(file.FullPath)
                ? file.FullPath
                : Path.Combine(_env.WebRootPath, file.FullPath.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }

        if (string.IsNullOrWhiteSpace(resolved))
        {
            resolved = Path.Combine(_env.WebRootPath, "uploads", file.FileName);
        }

        if (!System.IO.File.Exists(resolved))
        {
            // last-chance: maybe FileName already contains extension inside uploads
            var tryUploads = Path.Combine(_env.WebRootPath, "uploads", Path.GetFileName(file.FileName));
            if (!System.IO.File.Exists(tryUploads))
                return NotFound();
            resolved = tryUploads;
        }

        // 2) Infer content type
        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(resolved, out var contentType))
            contentType = "application/octet-stream";

        // 3) Choose download name (original if available)
        var downloadName = !string.IsNullOrWhiteSpace(file.OriginalFileName)
            ? file.OriginalFileName
            : file.FileName;

        // 4) Stream file (better for large files) and force download
        var stream = new FileStream(resolved, FileMode.Open, FileAccess.Read, FileShare.Read);
        return File(stream, contentType, fileDownloadName: downloadName);
    }
}
