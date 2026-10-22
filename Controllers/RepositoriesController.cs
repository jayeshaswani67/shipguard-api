
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using ShipGuard.Api.Models;

namespace ShipGuard.Api.Controllers;
         
[ApiController]
[Route("api/repositories")]
public class RepositoriesController : ControllerBase
{
    private readonly IMongoCollection<RepositoryDocument> _repositories;

    public RepositoriesController(IMongoDatabase database)
    {
        _repositories = database
            .GetCollection<RepositoryDocument>("Repositories");
    }

    [HttpGet]
    public async Task<ActionResult<List<RepositoryDocument>>> GetAll()
    {
        var repositories = await _repositories
            .Find(_ => true)
            .SortByDescending(r => r.CreatedAt)
            .ToListAsync();

        return Ok(repositories);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<RepositoryDocument>> GetById(
        string id)
    {
        if (!MongoDB.Bson.ObjectId.TryParse(id, out _))
            return BadRequest(new { message = "Invalid repository ID." });

        var repository = await _repositories
            .Find(r => r.Id == id)
            .FirstOrDefaultAsync();

        if (repository is null)
            return NotFound(new { message = "Repository not found." });

        return Ok(repository);
    }

    [HttpPost]
    public async Task<ActionResult<RepositoryDocument>> Create(
        [FromBody] CreateRepositoryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.RepositoryUrl))
        {
            return BadRequest(new
            {
                message = "Repository name and URL are required."
            });
        }

        if (!Uri.TryCreate(
                request.RepositoryUrl,
                UriKind.Absolute,
                out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps &&
             uri.Scheme != Uri.UriSchemeHttp) ||
            string.IsNullOrWhiteSpace(uri.Host))
        {
            return BadRequest(new
            {
                message = "A valid HTTP or HTTPS repository URL is required."
            });
        }

        var repository = new RepositoryDocument
        {
            Name = request.Name.Trim(),
            RepositoryUrl = request.RepositoryUrl.Trim(),
            Branch = string.IsNullOrWhiteSpace(request.Branch)
                ? "main"
                : request.Branch.Trim()
        };

        await _repositories.InsertOneAsync(repository);

        return CreatedAtAction(
            nameof(GetById),
            new { id = repository.Id },
            repository);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!MongoDB.Bson.ObjectId.TryParse(id, out _))
            return BadRequest(new { message = "Invalid repository ID." });

        var result = await _repositories.DeleteOneAsync(
            r => r.Id == id);

        if (result.DeletedCount == 0)
            return NotFound(new { message = "Repository not found." });

        return NoContent();
    }
}

public class CreateRepositoryRequest
{
    public string Name { get; set; } = string.Empty;

    public string RepositoryUrl { get; set; } = string.Empty;

    public string Branch { get; set; } = "main";
}
