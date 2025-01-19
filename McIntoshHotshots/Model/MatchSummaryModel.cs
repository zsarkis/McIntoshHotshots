using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace McIntoshHotshots.Model;

[Table("match_summary")]
public class MatchSummary
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("url_to_recap")]
    public string UrlToRecap { get; set; }

    [Column("home_player_id")]
    public int HomePlayerId { get; set; }

    [Column("away_player_id")]
    public int AwayPlayerId { get; set; }

    [Column("home_set_average")]
    public double HomeSetAverage { get; set; }

    [Column("away_set_average")]
    public double AwaySetAverage { get; set; }

    [Column("home_legs_won")]
    public int HomeLegsWon { get; set; }

    [Column("away_legs_won")]
    public int AwayLegsWon { get; set; }

    [Column("time_elapsed")]
    public string TimeElapsed { get; set; }

    [Column("cork_winner_player_id")]
    public int? CorkWinnerPlayerId { get; set; }
}

