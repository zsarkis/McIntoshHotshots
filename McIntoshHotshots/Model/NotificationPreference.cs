namespace McIntoshHotshots.Model;

public class NotificationPreference
{
    public string Type { get; set; } // e.g., "email" or "sms"
    public bool OptedIn { get; set; } // e.g., true or false
    public List<string> Instances { get; set; } // e.g., ["event_reminders", "new_event_for_interest"]
}
