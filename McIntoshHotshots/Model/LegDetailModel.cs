using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace McIntoshHotshots.Model;

public class LegDetailModel
{
    public int Id { get; set; } 
    public int MatchId { get; set; }  
    public int LegId { get; set; }  
    public int TurnNumber { get; set; }  
    public int PlayerId { get; set; }  
    public int? ScoreRemainingBeforeThrow { get; set; }  
    public int Score { get; set; }  
    public int DartsUsed { get; set; } 
}