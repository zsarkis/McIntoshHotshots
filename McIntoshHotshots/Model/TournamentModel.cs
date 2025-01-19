using System.ComponentModel.DataAnnotations.Schema;

namespace McIntoshHotshots.Model;

public class TournamentModel
{
    public int Id { get; set; }

    public DateTime Date { get; set; }

    [Column("pool_count")]
    public int PoolCount { get; set; }

    [Column("max_attendees")]
    public int MaxAttendees { get; set; }
}