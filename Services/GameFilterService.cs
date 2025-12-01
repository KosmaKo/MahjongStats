using MahjongStats.Models;

namespace MahjongStats.Services
{
    public interface IGameFilterService
    {
        List<MahjongGame> FilterGames(
            List<MahjongGame> games,
            DateTime? minDate,
            DateTime? maxDate,
            List<string>? playerNames);

        Task<Dictionary<string, List<RoundDetail>>> FetchAllRoundsThrottledAsync(
            List<MahjongGame> games,
            string bearerToken,
            IMahjongTrackerService apiService,
            int delayMilliseconds = 100,
            IProgress<(int current, int total)>? progress = null);
    }

    public class GameFilterService : IGameFilterService
    {
        private readonly ILogger<GameFilterService> _logger;

        public GameFilterService(ILogger<GameFilterService> logger)
        {
            _logger = logger;
        }

        public List<MahjongGame> FilterGames(
            List<MahjongGame> games,
            DateTime? minDate,
            DateTime? maxDate,
            List<string>? playerNames)
        {
            if (games == null || games.Count == 0)
                return new List<MahjongGame>();

            var filtered = games;

            // Filter by date range
            if (minDate.HasValue || maxDate.HasValue)
            {
                filtered = filtered.Where(g =>
                {
                    var gameDate = g.CreatedDateTime;
                    if (minDate.HasValue && gameDate < minDate.Value)
                        return false;
                    if (maxDate.HasValue && gameDate > maxDate.Value.AddDays(1)) // Add 1 day to include the entire max date
                        return false;
                    return true;
                }).ToList();

                _logger.LogInformation($"Filtered by date: {minDate:yyyy-MM-dd} to {maxDate:yyyy-MM-dd}. Remaining: {filtered.Count} games");
            }

            // Filter by players
            if (playerNames != null && playerNames.Count > 0)
            {
                var normalizedPlayerNames = playerNames
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Select(p => p.Trim().ToLowerInvariant())
                    .ToList();

                if (normalizedPlayerNames.Count > 0)
                {
                    filtered = filtered.Where(g =>
                    {
                        // Get all player names in this game
                        var gamePlayerNames = g.Players
                            .Select(p => (p.Name ?? "").ToLowerInvariant())
                            .ToList();

                        // Check if all filter players are in this game (substring match, case-insensitive)
                        return normalizedPlayerNames.All(filterPlayer =>
                            gamePlayerNames.Any(gamePlayer =>
                                gamePlayer.Contains(filterPlayer)));
                    }).ToList();

                    _logger.LogInformation($"Filtered by players: {string.Join(", ", playerNames)}. Remaining: {filtered.Count} games");
                }
            }

            return filtered;
        }

        public async Task<Dictionary<string, List<RoundDetail>>> FetchAllRoundsThrottledAsync(
            List<MahjongGame> games,
            string bearerToken,
            IMahjongTrackerService apiService,
            int delayMilliseconds = 100,
            IProgress<(int current, int total)>? progress = null)
        {
            var roundsDict = new Dictionary<string, List<RoundDetail>>();

            if (games == null || games.Count == 0)
                return roundsDict;

            _logger.LogInformation($"Starting to fetch rounds for {games.Count} games with {delayMilliseconds}ms throttle");

            for (int i = 0; i < games.Count; i++)
            {
                var game = games[i];
                if (game.Id == null)
                    continue;

                try
                {
                    var rounds = await apiService.GetGameRoundsAsync(game.Id, bearerToken);
                    roundsDict[game.Id] = rounds;

                    // Report progress
                    progress?.Report((i + 1, games.Count));

                    // Throttle requests (delay between requests, not after the last one)
                    if (i < games.Count - 1)
                    {
                        await Task.Delay(delayMilliseconds);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error fetching rounds for game {game.Id}: {ex.Message}");
                    // Continue with next game even if this one fails
                }
            }

            _logger.LogInformation($"Completed fetching rounds. Got rounds for {roundsDict.Count} games");
            return roundsDict;
        }
    }
}
