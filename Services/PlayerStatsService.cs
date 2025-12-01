using MahjongStats.Models;

namespace MahjongStats.Services
{
    public interface IPlayerStatsService
    {
        Task<PlayerStats> CalculateStatsAsync(List<MahjongGame> games, string playerName, IDatabaseService databaseService);
    }

    /// <summary>
    /// Extension methods for player seat conversions
    /// </summary>
    public static class PlayerSeatExtensions
    {
        /// <summary>
        /// Converts a player seat string (e.g., "Player1", "Player2") to a 0-based player index
        /// </summary>
        public static int? ToPlayerIndex(this string? seatString)
        {
            if (string.IsNullOrEmpty(seatString))
                return null;

            // Parse "Player1" -> 0, "Player2" -> 1, etc.
            if (seatString.StartsWith("Player", StringComparison.OrdinalIgnoreCase))
            {
                var numberPart = seatString.Substring(6); // Remove "Player" prefix
                if (int.TryParse(numberPart, out int playerNumber) && playerNumber >= 1 && playerNumber <= 4)
                {
                    return playerNumber - 1; // Convert 1-4 to 0-3
                }
            }

            // Also try direct numeric parsing for backward compatibility
            if (int.TryParse(seatString, out int directIndex))
            {
                return directIndex;
            }

            return null;
        }
    }

    public class PlayerStatsService : IPlayerStatsService
    {
        public async Task<PlayerStats> CalculateStatsAsync(List<MahjongGame> games, string playerName, IDatabaseService databaseService)
        {
            var stats = new PlayerStats { PlayerName = playerName };

            if (string.IsNullOrWhiteSpace(playerName) || games.Count == 0)
                return stats;

            var matchingGames = games.Where(g => g.Players.Any(p => p.Name?.Equals(playerName, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
            if (matchingGames.Count == 0)
                return stats;

            // Fetch all rounds from database
            var allRounds = await databaseService.GetAllRoundsAsync();
            var roundDetails = allRounds.Values.SelectMany(r => r).ToList();

            stats.GamesPlayed = matchingGames.Count;

            var placements = new List<int>();
            var pointsList = new List<int>();
            var yakitoriCount = 0;
            var dealInCount = 0;
            var oyaRoundCount = 0;
            var oyaTsumoManganCount = 0;
            var oyaTsumoHanemanCount = 0;
            var oyaEnemyTsumoCount = 0;
            var winningGames = 0;
            var totalRoundsPlayed = 0;
            var tsumoCount = 0;
            var ronCount = 0;

            foreach (var game in matchingGames)
            {
                var playerIndex = game.Players.FindIndex(p => p.Name?.Equals(playerName, StringComparison.OrdinalIgnoreCase) ?? false);
                if (playerIndex == -1) continue;

                // Game-level stats
                var placement = CalculatePlacement(game, playerIndex);
                placements.Add(placement);
                pointsList.Add(game.Points[playerIndex].Sum());
                if (placement <= 2) winningGames++;

                // Round-level stats
                var gameRounds = roundDetails.Where(r => r.GameId == game.Id).ToList();
                var roundStats = AnalyzeRounds(gameRounds, playerIndex);

                totalRoundsPlayed += gameRounds.Count;
                yakitoriCount += roundStats.HasYakitori ? 1 : 0;
                dealInCount += roundStats.DealInCount;
                oyaEnemyTsumoCount += roundStats.OyaEnemyTsumoCount;
                oyaTsumoManganCount += roundStats.OyaTsumoManganCount;
                oyaTsumoHanemanCount += roundStats.OyaTsumoHanemanCount;
                oyaRoundCount += roundStats.OyaRoundCount;
                tsumoCount += roundStats.TsumoCount;
                ronCount += roundStats.RonCount;
            }

            // Calculate final statistics
            stats.RoundsPlayed = totalRoundsPlayed;
            stats.AverageRank = placements.Count > 0 ? placements.Average() : 0;
            stats.AveragePoints = pointsList.Count > 0 ? pointsList.Average() : 0;
            stats.YakitoriRate = stats.GamesPlayed > 0 ? (yakitoriCount * 100.0) / stats.GamesPlayed : 0;
            stats.DealInRate = totalRoundsPlayed > 0 ? (dealInCount * 100.0) / totalRoundsPlayed : 0;
            stats.WinningRate = stats.GamesPlayed > 0 ? (winningGames * 100.0) / stats.GamesPlayed : 0;
            stats.TsumoRateOnOya = oyaRoundCount > 0 ? (oyaEnemyTsumoCount * 100.0) / oyaRoundCount : 0;
            stats.ManganPlusTsumoRateOnOya = oyaEnemyTsumoCount > 0 ? (oyaTsumoManganCount * 100.0) / oyaEnemyTsumoCount : 0;
            stats.HanemanPlusTsumoRateOnOya = oyaEnemyTsumoCount > 0 ? (oyaTsumoHanemanCount * 100.0) / oyaEnemyTsumoCount : 0;
            stats.TsumoRate = (tsumoCount + ronCount) > 0 ? (int)((tsumoCount * 100.0) / (tsumoCount + ronCount)) : 0;
            return stats;
        }

        private int CalculatePlacement(MahjongGame game, int playerIndex)
        {
            var playerPoints = game.Points[playerIndex][0];
            var betterCount = 0;
            for (int i = 0; i < game.Points.Count; i++)
            {
                if (i != playerIndex && game.Points[i][0] > playerPoints)
                    betterCount++;
            }
            return betterCount + 1;
        }

        private RoundStats AnalyzeRounds(List<RoundDetail> gameRounds, int playerIndex)
        {
            var stats = new RoundStats();
            var hasWinningRound = false;

            foreach (var round in gameRounds)
            {

                if (round.Outcome?.Equals("Ron", StringComparison.OrdinalIgnoreCase) ?? false)
                {
                    AnalyzeRonRound(round, playerIndex, stats, ref hasWinningRound);
                }
                else if (round.Outcome?.Equals("Tsumo", StringComparison.OrdinalIgnoreCase) ?? false)
                {
                    AnalyzeTsumoRound(round, playerIndex, stats, ref hasWinningRound);
                }
            }

            stats.HasYakitori = !hasWinningRound;
            return stats;
        }

        private void AnalyzeRonRound(RoundDetail round, int playerIndex, RoundStats stats, ref bool hasWinningRound)
        {
            var dealerIndex = round.GetDealerIndex();

            //stats for rounds as dealer
            if(dealerIndex == playerIndex)
                stats.OyaRoundCount++;

            // we won
            if (round.Data?.Winners != null && round.Data.Winners.Any(w => w.Seat.ToPlayerIndex() == playerIndex))
            {
                hasWinningRound = true;
                stats.RonCount++;
            }

            //we lost
            if (round.Data?.LoserSeat.ToPlayerIndex() == playerIndex)
                stats.DealInCount++;
        }

        private void AnalyzeTsumoRound(RoundDetail round, int playerIndex, RoundStats stats, ref bool hasWinningRound)
        {
            var dealerIndex = round.GetDealerIndex();
            var winnerIndex = round.Data?.WinnerSeat.ToPlayerIndex();

            // we won
            if (winnerIndex == playerIndex){
                stats.TsumoCount++;
                hasWinningRound = true;
            }

            //stats for rounds as dealer
            if(dealerIndex == playerIndex){
                stats.OyaRoundCount++;            
                
                //tsumo on our oya - it hurts
                if (winnerIndex != playerIndex)
                {
                    stats.OyaEnemyTsumoCount++;
                    if (IsHaneman(round.Data?.Score)){
                        stats.OyaTsumoManganCount++;
                        stats.OyaTsumoHanemanCount++;
                    }

                    else if (IsMangan(round.Data?.Score)){
                        stats.OyaTsumoManganCount++;
                    }
                }                
            }
        }

        private bool IsMangan(ScoreInfo? score)
        {
            if (score == null) return false;
            var han = score.Han;
            var fu = score.Fu ?? 0;
            return han >= 5 || (han == 4 && fu >= 40) || (han == 3 && fu >= 70);
        }
        private bool IsHaneman(ScoreInfo? score)
        {
            if (score == null) return false;
            var han = score.Han;
            return han >= 6; 
        }

    }

    /// <summary>
    /// Temporary object to hold round analysis results
    /// </summary>
    internal class RoundStats
    {
        public bool HasYakitori { get; set; }
        public int DealInCount { get; set; }
        public int OyaRoundCount { get; set; }
        public int TsumoCount { get; set; }
        public int RonCount { get; set; }
        public int OyaEnemyTsumoCount { get; set; }
        public int OyaTsumoManganCount { get; set; }
        public int OyaTsumoHanemanCount { get; set; }
    }

    public class PlayerStats
    {
        public string PlayerName { get; set; } = "";
        public int GamesPlayed { get; set; }
        public int RoundsPlayed { get; set; }
        public double AverageRank { get; set; }
        public double AveragePoints { get; set; }
        public double YakitoriRate { get; set; } // %
        public double DealInRate { get; set; } // %
        public double WinningRate { get; set; } // % (1st or 2nd)
        public double TsumoRateOnOya { get; set; } // %
        public double ManganPlusTsumoRateOnOya { get; set; } // %
        public double HanemanPlusTsumoRateOnOya { get; set; } // %
        public int TsumoRate { get; set; }// %
    }
}
