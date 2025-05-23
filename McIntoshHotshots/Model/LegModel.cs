using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace McIntoshHotshots.Model;

public class LegModel
{
    public int Id { get; set; }
    public int MatchId { get; set; }
    public int LegNumber { get; set; }
    public int HomePlayerDartsThrown { get; set; }
    public int AwayPlayerDartsThrown { get; set; }
    public int LoserScoreRemaining { get; set; }
    public int WinnerId { get; set; }
    public string TimeElapsed { get; set; }
}