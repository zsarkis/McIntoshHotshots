using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace McIntoshHotshots.Model;

public class LegDetailModel
{
    public int Id { get; set; }  // Auto-incremented primary key
    public int MatchId { get; set; }  // Foreign key to match_summary
    public int LegId { get; set; }  // Foreign key to leg table
    public int TurnNumber { get; set; }  // Turn number within the leg
    public int PlayerId { get; set; }  // Player identifier
    public int ScoreRemainingBeforeThrow { get; set; }  // Score before the throw
    public int Score { get; set; }  // Score achieved in this turn
    public int DartsUsed { get; set; }  // Number of darts used
}