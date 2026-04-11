namespace Maxwell;
public record ChatDefinition
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public string? Title { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;    
}

    