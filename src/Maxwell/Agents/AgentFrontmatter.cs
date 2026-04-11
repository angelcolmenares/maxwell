namespace Maxwell;

public record AgentFrontmatter
{
    public required string Name { get; init; }
    public required string Model { get; init; }
    public string Description { get; init; } = "";
    public string Role { get; init; } = "";
}