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
    public DateTime CreatedDateTime { get; set; }
    public DateTime FetchedDateTime { get; set; }
}
