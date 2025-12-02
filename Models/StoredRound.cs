namespace MahjongStats.Models;

/// <summary>
/// Entity for storing rounds in the database
/// </summary>
public class StoredRound
{
    public int Id { get; set; }
    public string GameId { get; set; } = string.Empty;
    public string RoundJson { get; set; } = string.Empty;
    
    private DateTime _createdDateTime = DateTime.UtcNow;
    public DateTime CreatedDateTime 
    { 
        get => _createdDateTime;
        set => _createdDateTime = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    }
}
