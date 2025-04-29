using System.ComponentModel.DataAnnotations.Schema;

namespace McIntoshHotshots.Model;

public class PlayerModel
{
    public int Id { get; set; } // Maps to the "id" column
    public string Name { get; set; } // Maps to the "name" column
    public int Earnings { get; set; } // Maps to the "earnings" column
    
    [Column("elo_number")]
    public int EloNumber { get; set; } // Maps to the "elo_number" column

    [Column("preferences")]
    public string Preferences { get; set; } // Maps to the "preferences" column (stored as JSON)

    [Column("user_id")]
    public string UserId { get; set; } // Maps to the "user_id" column
}
