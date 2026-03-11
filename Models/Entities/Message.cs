namespace MyHotel.Web.Models.Entities;

public class Message
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public string Subject { get; set; } = ""; // Used for announcements only
    public string Body { get; set; } = "";
    public string FromUserId { get; set; } = "";
    public string? ToUserId { get; set; } // null for announcements
    public bool IsAnnouncement { get; set; }
    public bool IsRead { get; set; } // For DMs only
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Hotel Hotel { get; set; } = null!;
    public ApplicationUser FromUser { get; set; } = null!;
    public ApplicationUser? ToUser { get; set; }

    public List<AnnouncementDepartment> AnnouncementDepartments { get; set; } = new();
    public List<AnnouncementReadReceipt> AnnouncementReadReceipts { get; set; } = new();
}

public class AnnouncementDepartment
{
    public int Id { get; set; }
    public int MessageId { get; set; }
    public int DepartmentId { get; set; }

    public Message Message { get; set; } = null!;
    public Department Department { get; set; } = null!;
}

public class AnnouncementReadReceipt
{
    public int Id { get; set; }
    public int MessageId { get; set; }
    public string UserId { get; set; } = "";
    public DateTime ReadAt { get; set; } = DateTime.UtcNow;

    public Message Message { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
}
