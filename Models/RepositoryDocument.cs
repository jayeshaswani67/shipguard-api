
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ShipGuard.Api.Models;

public class RepositoryDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string RepositoryUrl { get; set; } = string.Empty;

    public string Branch { get; set; } = "main";

    public string Provider { get; set; } = "GitHub";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
