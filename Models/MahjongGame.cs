using System.Text.Json.Serialization;

namespace MahjongStats.Models
{
    public class MahjongGame
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("confirmed")]
        public bool Confirmed { get; set; }

        [JsonPropertyName("created_at")]
        public long CreatedAt { get; set; }

        [JsonPropertyName("honba")]
        public int Honba { get; set; }

        [JsonPropertyName("players")]
        public List<Player> Players { get; set; } = new();

        [JsonPropertyName("points")]
        public List<List<int>> Points { get; set; } = new();

        [JsonPropertyName("riichi")]
        public int Riichi { get; set; }

        [JsonPropertyName("round")]
        public string? Round { get; set; }

        [JsonPropertyName("settings")]
        public GameSettings? Settings { get; set; }

        public DateTime CreatedDateTime => UnixTimeStampToDateTime(CreatedAt);

        private static DateTime UnixTimeStampToDateTime(long unixTimeStamp)
        {
            var dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dateTime = dateTime.AddSeconds(unixTimeStamp).ToLocalTime();
            return dateTime;
        }
    }

    public class Player
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    public class GameSettings
    {
        [JsonPropertyName("chonbo_type")]
        public string? ChonboType { get; set; }

        [JsonPropertyName("initial_points")]
        public int InitialPoints { get; set; }

        [JsonPropertyName("kiriage_mangan")]
        public bool KiriageMangan { get; set; }

        [JsonPropertyName("yakitori")]
        public bool Yakitori { get; set; }
    }
}
