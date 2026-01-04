namespace MahjongStats.Models
{
    public class OverallResults
    {
        public List<PlayerRankingSummary> PlayerRankings { get; set; } = new();
    }

    public class PlayerRankingSummary
    {
        public string PlayerName { get; set; } = string.Empty;
        public int TotalPoints { get; set; }
        public int FirstPlaces { get; set; }
        public int SecondPlaces { get; set; }
        public int ThirdPlaces { get; set; }
        public int FourthPlaces { get; set; }
        public int GamesPlayed { get; set; }
        public double AverageRank { get; set; }
    }
}
