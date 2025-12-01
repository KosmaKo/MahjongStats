using System.Text.Json.Serialization;

namespace MahjongStats.Models
{
    public class RoundDetail
    {
        [JsonPropertyName("round")]
        public string? Round { get; set; }

        [JsonPropertyName("outcome")]
        public string? Outcome { get; set; }

        [JsonPropertyName("points")]
        public List<List<int>> Points { get; set; } = new();

        [JsonPropertyName("data")]
        public RoundData? Data { get; set; }

        /// <summary>
        /// Game ID this round belongs to (populated when fetching rounds)
        /// </summary>
        public string? GameId { get; set; }

        /// <summary>
        /// Gets the dealer (oya) player index for this round based on the round string (e.g., "E1", "S2")
        /// </summary>
        public int? GetDealerIndex()
        {
            if (string.IsNullOrEmpty(Round) || Round.Length < 2)
                return null;

            // Round format: E1, E2, E3, E4, E1-2, S1, S2, etc.
            // The digit after the wind (or after E/S/W/N) indicates the dealer
            char dealerChar = Round[1];
            if (char.IsDigit(dealerChar))
            {
                if (int.TryParse(dealerChar.ToString(), out int dealerNumber))
                {
                    // Convert 1-4 to 0-3 index
                    return dealerNumber - 1;
                }
            }
            return null;
        }
    }

    public class RoundData
    {
        [JsonPropertyName("riichi")]
        public List<string> Riichi { get; set; } = new();

        [JsonPropertyName("score")]
        public ScoreInfo? Score { get; set; }

        [JsonPropertyName("winner_seat")]
        public string? WinnerSeat { get; set; }

        [JsonPropertyName("loser_seat")]
        public string? LoserSeat { get; set; }

        [JsonPropertyName("winners")]
        public List<Winner> Winners { get; set; } = new();
    }

    public class ScoreInfo
    {
        [JsonPropertyName("fu")]
        public int? Fu { get; set; }

        [JsonPropertyName("han")]
        public int Han { get; set; }
    }

    public class Winner
    {
        [JsonPropertyName("seat")]
        public string? Seat { get; set; }

        [JsonPropertyName("score")]
        public ScoreInfo? Score { get; set; }
    }
}
