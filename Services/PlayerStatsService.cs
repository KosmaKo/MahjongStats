using MahjongStats.Models;

namespace MahjongStats.Services
{
    public interface IPlayerStatsService
    {
        Task<PlayerStats> CalculateStatsAsync(List<MahjongGame> games, string playerName, IDatabaseService databaseService);
        Task<OverallResults> CalculateOverallResultsAsync(List<MahjongGame> games, List<string> playerNames);
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

            var normalizedPlayerName = playerName.ToUpper();
            var matchingGames = games.Where(g => g.Players.Any(p =>
                (p.Name?.ToUpper() ?? "").Contains(normalizedPlayerName))).ToList();
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
                var playerIndex = game.Players.FindIndex(p =>
                    (p.Name?.ToUpper() ?? "").Contains(normalizedPlayerName));
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
            // Use [0] for base game points (before uma/chombo) to determine placement
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
            if (dealerIndex == playerIndex)
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
            if (winnerIndex == playerIndex)
            {
                stats.TsumoCount++;
                hasWinningRound = true;
            }

            //stats for rounds as dealer
            if (dealerIndex == playerIndex)
            {
                stats.OyaRoundCount++;

                //tsumo on our oya - it hurts
                if (winnerIndex != playerIndex)
                {
                    stats.OyaEnemyTsumoCount++;
                    if (IsHaneman(round.Data?.Score))
                    {
                        stats.OyaTsumoManganCount++;
                        stats.OyaTsumoHanemanCount++;
                    }

                    else if (IsMangan(round.Data?.Score))
                    {
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

        public async Task<OverallResults> CalculateOverallResultsAsync(List<MahjongGame> games, List<string> playerNames)
        {
            return await OverallResultsCalculator.CalculateOverallResultsAsync(games, playerNames);
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

    public static class OverallResultsCalculator
    {
        public static async Task<OverallResults> CalculateOverallResultsAsync(List<MahjongGame> games, List<string> playerNames)
        {
            var results = new OverallResults();

            if (games.Count == 0 || playerNames.Count == 0)
                return results;

            var playerSummaries = new Dictionary<string, PlayerRankingSummary>();

            // Normalize player names to uppercase for matching
            var normalizedPlayerNames = playerNames.Select(p => p.ToUpper()).ToList();

            // Initialize summaries for each player
            foreach (var playerName in playerNames)
            {
                playerSummaries[playerName.ToUpper()] = new PlayerRankingSummary
                {
                    PlayerName = playerName
                };
            }

            // Process each game
            foreach (var game in games)
            {
                // Calculate final rankings for ALL players based on base points (before uma)
                var allGameStandings = new List<(string PlayerName, int BasePoints, int TotalPoints, int Rank, string FilterKey)>();

                for (int i = 0; i < game.Players.Count; i++)
                {
                    var playerName = game.Players[i].Name?.ToUpper() ?? "Unknown";
                    var basePoints = game.Points[i][0]; // Base game points for ranking
                    var totalPoints = game.Points[i].Sum(); // Total including uma/chombo for display

                    // Check if this player matches any of our filter strings (substring match)
                    var matchingFilterKey = normalizedPlayerNames.FirstOrDefault(filter =>
                        playerName.Contains(filter));

                    allGameStandings.Add((playerName, basePoints, totalPoints, 0, matchingFilterKey ?? ""));
                }

                // Sort by BASE POINTS descending and assign ranks
                allGameStandings = allGameStandings.OrderByDescending(x => x.BasePoints).ToList();
                var rankedStandings = new List<(string PlayerName, int BasePoints, int TotalPoints, int Rank, string FilterKey)>();
                for (int rank = 0; rank < allGameStandings.Count; rank++)
                {
                    var standing = allGameStandings[rank];
                    rankedStandings.Add((standing.PlayerName, standing.BasePoints, standing.TotalPoints, rank + 1, standing.FilterKey));
                }

                // Update summaries only for filtered players
                foreach (var standing in rankedStandings)
                {
                    if (!string.IsNullOrEmpty(standing.FilterKey) && playerSummaries.ContainsKey(standing.FilterKey))
                    {
                        var summary = playerSummaries[standing.FilterKey];
                        summary.GamesPlayed++;
                        summary.TotalPoints += standing.TotalPoints; // Use total points for cumulative score

                        switch (standing.Rank)
                        {
                            case 1:
                                summary.FirstPlaces++;
                                break;
                            case 2:
                                summary.SecondPlaces++;
                                break;
                            case 3:
                                summary.ThirdPlaces++;
                                break;
                            case 4:
                                summary.FourthPlaces++;
                                break;
                        }
                    }
                }
            }

            // Calculate average ranks
            foreach (var summary in playerSummaries.Values)
            {
                if (summary.GamesPlayed > 0)
                {
                    var totalRankPoints = (summary.FirstPlaces * 1) +
                                         (summary.SecondPlaces * 2) +
                                         (summary.ThirdPlaces * 3) +
                                         (summary.FourthPlaces * 4);
                    summary.AverageRank = (double)totalRankPoints / summary.GamesPlayed;
                }
            }

            // Sort by total points descending
            results.PlayerRankings = playerSummaries.Values
                .OrderByDescending(p => p.TotalPoints)
                .ToList();

            return await Task.FromResult(results);
        }
    }
}

