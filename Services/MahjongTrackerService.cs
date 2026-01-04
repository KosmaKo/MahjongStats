using MahjongStats.Models;
using System.Net.Http.Json;

namespace MahjongStats.Services
{
    public interface IMahjongTrackerService
    {
        Task<List<MahjongGame>> GetGamesAsync(string bearerToken);
        Task<List<RoundDetail>> GetGameRoundsAsync(string gameId, string bearerToken);
        Task<List<MahjongGame>> FetchAndSyncNewGamesAsync(string bearerToken);
        Task<List<string>> GetGamesNeedingRoundsAsync();
        Task<List<MahjongGame>> SyncGamesFromDateAsync(DateTime syncFromDate, string bearerToken);
        Task<(List<string> gameIds, int totalRounds)> SyncAllMissingRoundsAsync(string bearerToken, int throttleDelayMs = 100);
    }

    public class MahjongTrackerService : IMahjongTrackerService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<MahjongTrackerService> _logger;
        private readonly IDatabaseService _databaseService;
        private readonly string _baseUrl;
        private const string ConfigKey = "MahjongTracker:ApiUrl";

        public MahjongTrackerService(HttpClient httpClient, ILogger<MahjongTrackerService> logger, IDatabaseService databaseService, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            _databaseService = databaseService;
            _baseUrl = configuration[ConfigKey] ?? throw new InvalidOperationException($"Missing configuration key: {ConfigKey}");
        }

        public async Task<List<MahjongGame>> GetGamesAsync(string bearerToken)
        {
            try
            {
                if (string.IsNullOrEmpty(bearerToken))
                {
                    _logger.LogError("Bearer token is null or empty");
                    throw new InvalidOperationException("Bearer token is required");
                }

                var url = $"{_baseUrl}/games?show_all=1";

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);

                var response = await _httpClient.SendAsync(request);

                response.EnsureSuccessStatusCode();

                var games = await response.Content.ReadFromJsonAsync<List<MahjongGame>>();

                // Save to database in bulk (replacing all games)
                if (games != null && games.Any())
                {
                    try
                    {
                        await _databaseService.ReplaceAllGamesAsync(games);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Failed to save games to database: {ex.Message}");
                    }
                }

                return games ?? new List<MahjongGame>();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError($"HTTP Request Error fetching games: {ex.StatusCode} - {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching games: {ex.GetType().Name} - {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }

        public async Task<List<RoundDetail>> GetGameRoundsAsync(string gameId, string bearerToken)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/games/{gameId}/rounds");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);

                var response = await _httpClient.SendAsync(request);

                // Handle 500 errors gracefully - skip this game and continue with others
                if (response.StatusCode == System.Net.HttpStatusCode.InternalServerError)
                {
                    _logger.LogWarning($"API returned 500 error for game {gameId}, skipping this game");
                    return new List<RoundDetail>();
                }

                response.EnsureSuccessStatusCode();

                var rounds = await response.Content.ReadFromJsonAsync<List<RoundDetail>>();

                // Populate GameId for each round
                if (rounds != null)
                {
                    foreach (var round in rounds)
                    {
                        round.GameId = gameId;
                    }
                }

                // Save to database
                if (rounds != null)
                {
                    try
                    {
                        await _databaseService.SaveRoundsAsync(gameId, rounds);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Failed to save rounds for game {gameId}: {ex.Message}");
                    }
                }

                return rounds ?? new List<RoundDetail>();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching rounds for game {gameId}: {ex.Message}");
                throw;
            }
        }

        public async Task<List<MahjongGame>> FetchAndSyncNewGamesAsync(string bearerToken)
        {
            try
            {
                if (string.IsNullOrEmpty(bearerToken))
                {
                    _logger.LogError("Bearer token is null or empty");
                    throw new InvalidOperationException("Bearer token is required");
                }

                // Fetch all games from API
                var url = $"{_baseUrl}/games?show_all=1";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var apiGames = await response.Content.ReadFromJsonAsync<List<MahjongGame>>();
                if (apiGames == null || !apiGames.Any())
                {
                    return new List<MahjongGame>();
                }

                // Get existing games from database
                var existingGameIds = await _databaseService.GetAllGameIdsAsync();
                var existingGameIdSet = new HashSet<string>(existingGameIds);

                // Find new games
                var newGames = apiGames
                    .Where(g => !string.IsNullOrEmpty(g.Id) && !existingGameIdSet.Contains(g.Id))
                    .ToList();

                // Save new games if any
                if (newGames.Any())
                {
                    try
                    {
                        await _databaseService.SaveGamesAsync(newGames);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Failed to save new games: {ex.Message}");
                    }
                }

                return newGames;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError($"HTTP Request Error fetching games: {ex.StatusCode} - {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching and syncing games: {ex.GetType().Name} - {ex.Message}");
                throw;
            }
        }

        public async Task<List<string>> GetGamesNeedingRoundsAsync()
        {
            try
            {
                // Get all games
                var allGameIds = await _databaseService.GetAllGameIdsAsync();

                // Get games that already have rounds
                var gamesWithRounds = await _databaseService.GetGamesWithRoundsAsync();
                var gamesWithRoundsSet = new HashSet<string>(gamesWithRounds);

                // Return games without rounds
                return allGameIds
                    .Where(gameId => !gamesWithRoundsSet.Contains(gameId))
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error determining games needing rounds: {ex.Message}");
                throw;
            }
        }

        public async Task<List<MahjongGame>> SyncGamesFromDateAsync(DateTime syncFromDate, string bearerToken)
        {
            try
            {
                if (string.IsNullOrEmpty(bearerToken))
                {
                    _logger.LogError("Bearer token is null or empty");
                    throw new InvalidOperationException("Bearer token is required");
                }

                // Delete games created after the sync date from database
                await _databaseService.DeleteGamesCreatedAfterAsync(syncFromDate);

                // Fetch all games from API
                var url = $"{_baseUrl}/games?show_all=1";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var apiGames = await response.Content.ReadFromJsonAsync<List<MahjongGame>>();
                if (apiGames == null || !apiGames.Any())
                {
                    return new List<MahjongGame>();
                }

                // Filter games created after the sync date
                var gamesToSync = apiGames
                    .Where(g => !string.IsNullOrEmpty(g.Id) && g.CreatedDateTime > syncFromDate)
                    .ToList();

                // Save games if any
                if (gamesToSync.Any())
                {
                    try
                    {
                        await _databaseService.SaveGamesAsync(gamesToSync);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Failed to save synced games: {ex.Message}");
                    }
                }

                return gamesToSync;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError($"HTTP Request Error syncing games: {ex.StatusCode} - {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error syncing games from date {syncFromDate}: {ex.GetType().Name} - {ex.Message}");
                throw;
            }
        }

        public async Task<(List<string> gameIds, int totalRounds)> SyncAllMissingRoundsAsync(string bearerToken, int throttleDelayMs = 100)
        {
            try
            {
                if (string.IsNullOrEmpty(bearerToken))
                {
                    _logger.LogError("Bearer token is null or empty");
                    throw new InvalidOperationException("Bearer token is required");
                }

                // Get all games needing rounds
                var gamesNeedingRounds = await GetGamesNeedingRoundsAsync();
                if (!gamesNeedingRounds.Any())
                {
                    return (new List<string>(), 0);
                }

                // Get all games and filter to ones needing rounds, then sort by creation date (newest first)
                var allGames = await _databaseService.GetAllGamesAsync();
                var gamesNeedingRoundsSet = new HashSet<string>(gamesNeedingRounds);

                var sortedGamesNeedingRounds = allGames
                    .Where(g => !string.IsNullOrEmpty(g.Id) && gamesNeedingRoundsSet.Contains(g.Id))
                    .OrderByDescending(g => g.CreatedAt)
                    .Select(g => g.Id)
                    .ToList();

                var syncedGameIds = new List<string>();
                var totalRounds = 0;

                foreach (var gameId in sortedGamesNeedingRounds)
                {
                    try
                    {
                        var rounds = await GetGameRoundsAsync(gameId!, bearerToken);
                        totalRounds += rounds.Count;
                        syncedGameIds.Add(gameId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Failed to sync rounds for game {gameId}: {ex.Message}");
                    }

                    await Task.Delay(throttleDelayMs);
                }

                return (syncedGameIds, totalRounds);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error syncing all missing rounds: {ex.GetType().Name} - {ex.Message}");
                throw;
            }
        }
    }
}
