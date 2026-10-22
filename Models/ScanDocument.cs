
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ShipGuard.Api.Models;

public class ScanFinding
{
    public string Title { get; set; } = string.Empty;

    public string Severity { get; set; } = "Low";

    public string Description { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public string Recommendation { get; set; } = string.Empty;
}

public class ScanDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string RepositoryId { get; set; } = string.Empty;

    public string RepositoryName { get; set; } = string.Empty;

    public string RepositoryUrl { get; set; } = string.Empty;

    public string Branch { get; set; } = "main";

    // Pending, Running, Completed, Failed, or DemoCompleted
    public string Status { get; set; } = "Pending";

    // Demo initially; real analysis can be added later
    public string ScanMode { get; set; } = "Demo";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<ScanFinding> Findings { get; set; } = new();
}