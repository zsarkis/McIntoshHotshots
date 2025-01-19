using McIntoshHotshots.Model;
using McIntoshHotshots.Repo;

namespace McIntoshHotshots.Services;

public interface ITournamentService
{
    Task<int> CreateTournamentAsync(TournamentModel tournament);
    Task<List<TournamentModel>> GetTournamentsAsync();
    Task<int> UpdateTournamentAsync(TournamentModel tournament);
}

public class TournamentService : ITournamentService
{
    ITournamentRepo _tournamentRepo;
    
    public TournamentService(ITournamentRepo tournamentRepo)
    {
        _tournamentRepo = tournamentRepo;
    }
    
    public async Task<int> CreateTournamentAsync(TournamentModel tournament)
    {
        var tournamentId =  await _tournamentRepo.Insert(tournament);
        
        return tournamentId;
    }
    
    public async Task<int> UpdateTournamentAsync(TournamentModel tournament)
    {
        var tournamentId =  await _tournamentRepo.UpdateTournamentAsync(tournament);
        
        return tournamentId;
    }
    
    public async Task<TournamentModel> GetTournamentAsync(int tournamentId)
    {
        var tournament =  await _tournamentRepo.GetTournamentByIdAsync(tournamentId);
        
        return tournament;
    }
    
    public async Task<List<TournamentModel>> GetTournamentsAsync()
    {
        var tournaments = await _tournamentRepo.GetTournamentsAsync();
        
        return tournaments.OrderByDescending(tourney => tourney.Date).ToList();
    }
    
    //TODO: Update tournament
    //TODO: Delete tournament
}