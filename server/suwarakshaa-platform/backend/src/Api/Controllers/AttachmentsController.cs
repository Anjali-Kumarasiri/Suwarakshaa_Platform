using System.Security.Cryptography;
using Api.Extensions;
using Api.Services;
using Application.Strategies;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Api.Controllers;

/// Supporting evidence (photo / PDF) for a request. Secure upload handling:
/// size limit, file type detected from the CONTENT (not the name or the client's content-type), server-generated file name,
/// storage outside the web root, SHA-256 recorded, per-request limit, owner-only upload, owner/staff-only download,
/// downloads are forced as attachments with nosniff.
/// Limitation to document: no antivirus scan.
[ApiController]
[Route("api/requests/{requestId:long}/attachments")]
[Authorize]
public class AttachmentsController : ControllerBase
{
    private const long MaxBytes = 5 * 1024 * 1024;
    private const int MaxPerRequest = 5;
    private readonly AppDbContext _db; private readonly string _dir;
    public AttachmentsController(AppDbContext db, IConfiguration cfg)
    {
        _db = db;
        _dir = Path.GetFullPath(cfg["Storage:Path"] ?? "uploads");
        Directory.CreateDirectory(_dir);
    }

    private static (string Type, string Ext)? Detect(byte[] b)
    {
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return ("image/jpeg", ".jpg");
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A) return ("image/png", ".png");
        if (b.Length >= 4 && b[0] == 0x25 && b[1] == 0x50 && b[2] == 0x44 && b[3] == 0x46) return ("application/pdf", ".pdf");
        if (b.Length >= 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P') return ("image/webp", ".webp");
        return null;
    }

    [HttpPost, RequestSizeLimit(MaxBytes + 65536)]
    public async Task<IActionResult> Upload(long requestId, IFormFile file, CancellationToken ct)
    {
        var req = await _db.AssistanceRequests.FindAsync(new object[] { requestId }, ct);
        if (req is null) return NotFound();
        if (req.SubmittedBy != User.GetUserId()) return Forbid();
        if (file is null || file.Length == 0) return BadRequest(new { error = "No file received." });
        if (file.Length > MaxBytes) return StatusCode(413, new { error = "File is larger than 5 MB." });
        if (await _db.RequestAttachments.CountAsync(a => a.RequestId == requestId, ct) >= MaxPerRequest) return Conflict(new { error = $"At most {MaxPerRequest} files per request." });

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();
        var kind = Detect(bytes);
        if (kind is null) return StatusCode(415, new { error = "Only JPEG, PNG, WebP and PDF files are accepted." });

        var stored = Guid.NewGuid().ToString("N") + kind.Value.Ext;
        await System.IO.File.WriteAllBytesAsync(Path.Combine(_dir, stored), bytes, ct);
        var original = TextSanitizer.Clean(Path.GetFileName(file.FileName), 200);
        var a = new RequestAttachment
        {
            RequestId = requestId, StoredFileName = stored, OriginalFileName = string.IsNullOrWhiteSpace(original) ? "file" + kind.Value.Ext : original,
            ContentType = kind.Value.Type, SizeBytes = bytes.Length, Sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()
        };
        _db.RequestAttachments.Add(a);
        Workflow.Audit(_db, User.GetUserId(), "ATTACHMENT_UPLOADED", "AssistanceRequest", requestId.ToString(), null, a.Sha256);
        await _db.SaveChangesAsync(ct);
        return Created("", new { a.AttachmentId, a.OriginalFileName, a.ContentType, a.SizeBytes });
    }

    [HttpGet]
    public async Task<IActionResult> List(long requestId, CancellationToken ct)
    {
        var req = await _db.AssistanceRequests.FindAsync(new object[] { requestId }, ct);
        if (req is null) return NotFound();
        if (req.SubmittedBy != User.GetUserId() && !User.IsStaff()) return Forbid();
        return Ok(await _db.RequestAttachments.Where(a => a.RequestId == requestId).Select(a => new { a.AttachmentId, a.OriginalFileName, a.ContentType, a.SizeBytes, a.UploadedAt }).ToListAsync(ct));
    }

    [HttpGet("{attachmentId:long}/download")]
    public async Task<IActionResult> Download(long requestId, long attachmentId, CancellationToken ct)
    {
        var req = await _db.AssistanceRequests.FindAsync(new object[] { requestId }, ct);
        var a = await _db.RequestAttachments.FirstOrDefaultAsync(x => x.AttachmentId == attachmentId && x.RequestId == requestId, ct);
        if (req is null || a is null) return NotFound();
        if (req.SubmittedBy != User.GetUserId() && !User.IsStaff()) return Forbid();
        if (Path.GetFileName(a.StoredFileName) != a.StoredFileName) return NotFound();        // defence in depth against path tricks
        var path = Path.Combine(_dir, a.StoredFileName);
        if (!System.IO.File.Exists(path)) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(System.IO.File.OpenRead(path), a.ContentType, a.OriginalFileName);       // Content-Disposition: attachment
    }
}
