namespace Maxwell;
public record ChatDefinition
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public string? Title { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow.RemoveMilliseconds();    
}

    