namespace MahjongStats.Models;

/// <summary>
/// Entity for storing games in the database
/// </summary>
public class StoredGame
{
    public int Id { get; set; }
    public string GameId { get; set; } = string.Empty;
    public string Players { get; set; } = string.Empty;
    public string PointsJson { get; set; } = string.Empty;
    
    private DateTime _createdDateTime = DateTime.UtcNow;
    public DateTime CreatedDateTime 
    { 
        get => _createdDateTime;
        set => _createdDateTime = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    }
    
    private DateTime _fetchedDateTime = DateTime.UtcNow;
    public DateTime FetchedDateTime 
    { 
        get => _fetchedDateTime;
        set => _fetchedDateTime = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    }
}
