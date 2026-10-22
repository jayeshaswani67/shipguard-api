
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using ShipGuard.Api.Models;

namespace ShipGuard.Api.Controllers;

[ApiController]
[Route("api/scans")]
public class ScansController : ControllerBase
{
    private readonly IMongoCollection<RepositoryDocument> _repositories;
    private readonly IMongoCollection<ScanDocument> _scans;

    public ScansController(IMongoDatabase database)
    {
        _repositories = database.GetCollection<RepositoryDocument>("Repositories");
        _scans = database.GetCollection<ScanDocument>("Scans");
    }

    // GET: /api/scans
    [HttpGet]
    public async Task<ActionResult<List<ScanDocument>>> GetAll()
    {
        var scans = await _scans
            .Find(_ => true)
            .SortByDescending(s => s.CreatedAt)
            .ToListAsync();

        return Ok(scans);
    }

    // GET: /api/scans/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<ScanDocument>> GetById(string id)
    {
        if (!ObjectId.TryParse(id, out _))
        {
            return BadRequest(new
            {
                message = "Invalid scan ID."
            });
        }

        var scan = await _scans
            .Find(s => s.Id == id)
            .FirstOrDefaultAsync();

        if (scan is null)
        {
            return NotFound(new
            {
                message = "Scan not found."
            });
        }

        return Ok(scan);
    }

    // POST: /api/scans
    // Creates a prototype scan with illustrative findings only.
    [HttpPost]
    public async Task<ActionResult<ScanDocument>> CreateDemoScan(
        [FromBody] CreateScanRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RepositoryId) ||
            !ObjectId.TryParse(request.RepositoryId, out _))
        {
            return BadRequest(new
            {
                message = "A valid repository ID is required."
            });
        }

        var repository = await _repositories
            .Find(r => r.Id == request.RepositoryId)
            .FirstOrDefaultAsync();

        if (repository is null)
        {
            return NotFound(new
            {
                message = "Register the repository before scanning."
            });
        }

        var scan = new ScanDocument
        {
            RepositoryId = repository.Id!,
            RepositoryName = repository.Name,
            RepositoryUrl = repository.RepositoryUrl,
            Branch = repository.Branch,
            Status = "DemoCompleted",
            ScanMode = "Demo",
            CreatedAt = DateTime.UtcNow,
            Findings =
            [
                new ScanFinding
                {
                    Title = "Example: missing input validation",
                    Severity = "Medium",
                    Description =
                        "Illustrative finding only. No source code was analyzed.",
                    FilePath = "src/example.ts",
                    Recommendation =
                        "Validate untrusted input at the API boundary."
                },
                new ScanFinding
                {
                    Title = "Example: missing error handling",
                    Severity = "Low",
                    Description =
                        "Illustrative finding only. No source code was analyzed.",
                    FilePath = "src/example.ts",
                    Recommendation =
                        "Handle expected failures and return useful error messages."
                }
            ]
        };

        await _scans.InsertOneAsync(scan);

        return CreatedAtAction(
            nameof(GetById),
            new { id = scan.Id },
            scan);
    }
}

// Request model used by POST /api/scans
public class CreateScanRequest
{
    public string RepositoryId { get; set; } = string.Empty;
}