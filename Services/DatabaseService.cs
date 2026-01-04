using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MahjongStats.Data;
using MahjongStats.Models;

namespace MahjongStats.Services;

public interface IDatabaseService
{
    Task<bool> GameExistsAsync(string gameId);
    Task SaveGamesAsync(List<MahjongGame> games);
    Task ReplaceAllGamesAsync(List<MahjongGame> games);
    Task SaveRoundsAsync(string gameId, List<RoundDetail> rounds);
    Task SaveRoundsBulkAsync(Dictionary<string, List<RoundDetail>> roundsByGameId);
    Task<MahjongGame?> GetGameAsync(string gameId);
    Task<List<RoundDetail>> GetRoundsAsync(string gameId);
    Task<Dictionary<string, List<RoundDetail>>> GetAllRoundsAsync();
    Task<List<MahjongGame>> GetAllGamesAsync();
    Task<List<string>> GetAllGameIdsAsync();
    Task<List<string>> GetGamesWithRoundsAsync();
    Task DeleteGameAsync(string gameId);
    Task DeleteGamesCreatedAfterAsync(DateTime date);
}

public class DatabaseService : IDatabaseService
{
    private readonly MahjongStatsContext _context;
    private readonly ILogger<DatabaseService> _logger;

    public DatabaseService(MahjongStatsContext context, ILogger<DatabaseService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> GameExistsAsync(string gameId)
    {
        return await _context.StoredGames.AnyAsync(g => g.GameId == gameId);
    }

    public async Task SaveGamesAsync(List<MahjongGame> games)
    {
        try
        {
            if (games == null || !games.Any())
            {
                _logger.LogWarning("No games to save");
                return;
            }

            var newGames = new List<StoredGame>();
            var gameIdsToUpdate = new List<string>();

            foreach (var game in games)
            {
                if (string.IsNullOrEmpty(game?.Id))
                {
                    _logger.LogWarning("Skipping game with null or empty Id");
                    continue;
                }

                var pointsJson = JsonSerializer.Serialize(game.Points);
                var playersString = string.Join(",", game.Players?.Select(p => p.Name ?? "") ?? new List<string>());

                var storedGame = new StoredGame
                {
                    GameId = game.Id,
                    Players = playersString,
                    PointsJson = pointsJson,
                    CreatedDateTime = DateTime.SpecifyKind(game.CreatedDateTime, DateTimeKind.Utc),
                    FetchedDateTime = DateTime.UtcNow
                };
                newGames.Add(storedGame);
                gameIdsToUpdate.Add(game.Id);
            }

            if (newGames.Any())
            {
                // Get existing games
                var existingGames = await _context.StoredGames
                    .Where(g => gameIdsToUpdate.Contains(g.GameId))
                    .ToListAsync();

                var existingGameIds = existingGames.Select(g => g.GameId).ToHashSet();

                // Remove existing games that we're about to replace
                if (existingGames.Any())
                {
                    // First delete rounds associated with these games
                    var roundsToDelete = await _context.StoredRounds
                        .Where(r => existingGameIds.Contains(r.GameId))
                        .ToListAsync();

                    if (roundsToDelete.Any())
                    {
                        _context.StoredRounds.RemoveRange(roundsToDelete);
                    }

                    // Then delete the games
                    _context.StoredGames.RemoveRange(existingGames);
                }

                // Add new games
                _context.StoredGames.AddRange(newGames);
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving games to database");
            throw;
        }
    }

    public async Task ReplaceAllGamesAsync(List<MahjongGame> games)
    {
        try
        {
            if (games == null || !games.Any())
            {
                _logger.LogWarning("No games to save");
                return;
            }

            // Clear all existing games and rounds and save fresh batch
            // First delete all rounds, then delete all games
            var allRounds = await _context.StoredRounds.ToListAsync();
            if (allRounds.Any())
            {
                _context.StoredRounds.RemoveRange(allRounds);
            }

            var allExistingGames = await _context.StoredGames.ToListAsync();
            if (allExistingGames.Any())
            {
                _context.StoredGames.RemoveRange(allExistingGames);
            }

            var newGames = new List<StoredGame>();

            foreach (var game in games)
            {
                if (string.IsNullOrEmpty(game?.Id))
                {
                    _logger.LogWarning("Skipping game with null or empty Id");
                    continue;
                }

                var pointsJson = JsonSerializer.Serialize(game.Points);
                var playersString = string.Join(",", game.Players?.Select(p => p.Name ?? "") ?? new List<string>());

                var storedGame = new StoredGame
                {
                    GameId = game.Id,
                    Players = playersString,
                    PointsJson = pointsJson,
                    CreatedDateTime = DateTime.SpecifyKind(game.CreatedDateTime, DateTimeKind.Utc),
                    FetchedDateTime = DateTime.UtcNow
                };
                newGames.Add(storedGame);
            }

            _context.StoredGames.AddRange(newGames);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error replacing all games in database");
            throw;
        }
    }

    public async Task SaveRoundsAsync(string gameId, List<RoundDetail> rounds)
    {
        if (string.IsNullOrEmpty(gameId) || rounds == null || !rounds.Any())
            return;
        await SaveRoundsBulkAsync(new Dictionary<string, List<RoundDetail>> { { gameId, rounds } });
    }

    public async Task SaveRoundsBulkAsync(Dictionary<string, List<RoundDetail>> roundsByGameId)
    {
        try
        {
            if (roundsByGameId == null || !roundsByGameId.Any())
            {
                _logger.LogWarning("No rounds to save");
                return;
            }

            var allGameIds = roundsByGameId.Keys.ToList();
            var existingRounds = await _context.StoredRounds
                .Where(r => allGameIds.Contains(r.GameId))
                .ToListAsync();

            var existingRoundsByGameId = existingRounds
                .GroupBy(r => r.GameId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var totalAdded = 0;
            var totalUpdated = 0;

            foreach (var kvp in roundsByGameId)
            {
                var gameId = kvp.Key;
                var rounds = kvp.Value;

                if (string.IsNullOrEmpty(gameId) || !rounds.Any())
                    continue;

                if (existingRoundsByGameId.TryGetValue(gameId, out var existingGameRounds))
                {
                    // Update existing rounds - remove old ones and add new ones
                    _context.StoredRounds.RemoveRange(existingGameRounds);
                    totalUpdated += existingGameRounds.Count;
                }

                var storedRounds = rounds.Select(round => new StoredRound
                {
                    GameId = gameId,
                    RoundJson = JsonSerializer.Serialize(round),
                    CreatedDateTime = DateTime.UtcNow
                }).ToList();

                _context.StoredRounds.AddRange(storedRounds);
                totalAdded += storedRounds.Count;
            }

            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving rounds in bulk to database");
            throw;
        }
    }

    public async Task<MahjongGame?> GetGameAsync(string gameId)
    {
        try
        {
            var storedGame = await _context.StoredGames.FirstOrDefaultAsync(g => g.GameId == gameId);

            if (storedGame == null)
                return null;

            // Reconstruct a minimal MahjongGame from stored data
            var pointsJson = JsonSerializer.Deserialize<List<List<int>>>(storedGame.PointsJson) ?? new List<List<int>>();
            var playerNames = storedGame.Players.Split(",");

            var game = new MahjongGame
            {
                Id = storedGame.GameId,
                Players = playerNames.Select(name => new Player { Name = name }).ToList(),
                Points = pointsJson,
                CreatedAt = (long)(storedGame.CreatedDateTime - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds
            };

            return game;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving game {GameId} from database", gameId);
            throw;
        }
    }

    public async Task<List<RoundDetail>> GetRoundsAsync(string gameId)
    {
        try
        {
            var storedRounds = await _context.StoredRounds
                .Where(r => r.GameId == gameId)
                .ToListAsync();

            var rounds = new List<RoundDetail>();
            foreach (var storedRound in storedRounds)
            {
                var round = JsonSerializer.Deserialize<RoundDetail>(storedRound.RoundJson);
                if (round != null)
                {
                    round.GameId = gameId;
                    rounds.Add(round);
                }
            }

            return rounds;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving rounds for game {GameId} from database", gameId);
            throw;
        }
    }

    public async Task<Dictionary<string, List<RoundDetail>>> GetAllRoundsAsync()
    {
        try
        {
            var storedRounds = await _context.StoredRounds.ToListAsync();
            var roundsDict = new Dictionary<string, List<RoundDetail>>();

            foreach (var storedRound in storedRounds)
            {
                var round = JsonSerializer.Deserialize<RoundDetail>(storedRound.RoundJson);
                if (round != null)
                {
                    round.GameId = storedRound.GameId;

                    if (!roundsDict.ContainsKey(storedRound.GameId))
                    {
                        roundsDict[storedRound.GameId] = new List<RoundDetail>();
                    }

                    roundsDict[storedRound.GameId].Add(round);
                }
            }

            return roundsDict;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all rounds from database");
            throw;
        }
    }

    public async Task<List<MahjongGame>> GetAllGamesAsync()
    {
        try
        {
            var storedGames = await _context.StoredGames.ToListAsync();
            var games = new List<MahjongGame>();

            foreach (var storedGame in storedGames)
            {
                var pointsJson = JsonSerializer.Deserialize<List<List<int>>>(storedGame.PointsJson) ?? new List<List<int>>();
                var playerNames = storedGame.Players.Split(",");

                var game = new MahjongGame
                {
                    Id = storedGame.GameId,
                    Players = playerNames.Select(name => new Player { Name = name }).ToList(),
                    Points = pointsJson,
                    CreatedAt = (long)(storedGame.CreatedDateTime - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds
                };

                games.Add(game);
            }

            return games;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all games from database");
            throw;
        }
    }

    public async Task<List<string>> GetAllGameIdsAsync()
    {
        try
        {
            return await _context.StoredGames
                .Select(g => g.GameId)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving game IDs from database");
            throw;
        }
    }

    public async Task<List<string>> GetGamesWithRoundsAsync()
    {
        try
        {
            // Get all games that have at least one round stored
            return await _context.StoredRounds
                .Select(r => r.GameId)
                .Distinct()
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving game IDs with rounds from database");
            throw;
        }
    }

    public async Task DeleteGameAsync(string gameId)
    {
        try
        {
            var game = await _context.StoredGames.FirstOrDefaultAsync(g => g.GameId == gameId);
            if (game != null)
            {
                _context.StoredGames.Remove(game);
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting game {GameId} from database", gameId);
            throw;
        }
    }

    public async Task DeleteGamesCreatedAfterAsync(DateTime date)
    {
        try
        {
            // Ensure date is UTC for PostgreSQL
            var utcDate = date.Kind == DateTimeKind.Utc ? date : DateTime.SpecifyKind(date, DateTimeKind.Utc);
            
            // Get games created after the specified date
            var gamesToDelete = await _context.StoredGames
                .Where(g => g.CreatedDateTime > utcDate)
                .ToListAsync();

            if (gamesToDelete.Any())
            {
                // Get the game IDs
                var gameIdsToDelete = gamesToDelete.Select(g => g.GameId).ToList();

                // Delete associated rounds
                var roundsToDelete = await _context.StoredRounds
                    .Where(r => gameIdsToDelete.Contains(r.GameId))
                    .ToListAsync();

                _context.StoredRounds.RemoveRange(roundsToDelete);

                // Delete the games
                _context.StoredGames.RemoveRange(gamesToDelete);

                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting games created after {Date} from database", date);
            throw;
        }
    }
}
