using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace SyncNotes.Shared.Models
{
    public class Note
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string NoteName { get; set; } = "Untitled";
        public string Content { get; set; } = string.Empty;
        public DateTime LastModified { get; set; } = DateTime.UtcNow;
        public string LastModifiedBy { get; set; } = string.Empty;
        [NotMapped]
        public string? LastModifiedByName { get; set; }
        public string UserId { get; set; } = string.Empty; // FK
        [JsonIgnore]
        public User? User { get; set; }
        [Required]
        public string OwnerId { get; set; }  // User who created it
        public string SharedWithStr { get; set; } = string.Empty;  // Comma-separated list of friend user IDs

        [NotMapped]
        public List<string> SharedWith
        {
            get => string.IsNullOrEmpty(SharedWithStr) ? new List<string>() : SharedWithStr.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
            set => SharedWithStr = string.Join(",", value);
        }
    }

    public class WhiteboardElement
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string WhiteboardName { get; set; } = "Untitled";
        public string Type { get; set; } = string.Empty; // "line", "rectangle", "circle", "text"
        public string Color { get; set; } = "#000000";
        public double StrokeWidth { get; set; } = 2;
        public List<Point> Points { get; set; } = new();
        public string Text { get; set; } = string.Empty;
        public DateTime LastModified { get; set; } = DateTime.UtcNow;
        public string LastModifiedBy { get; set; } = string.Empty;
        [NotMapped]
        public string? LastModifiedByName { get; set; }
        
        public string UserId { get; set; } = string.Empty; // FK
        [JsonIgnore]
        public User? User { get; set; }
        [Required]
        public string OwnerId { get; set; }  // User who created it
        public string SharedWithStr { get; set; } = string.Empty;

        [NotMapped]
        public List<string> SharedWith
        {
            get => string.IsNullOrEmpty(SharedWithStr) ? new List<string>() : SharedWithStr.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
            set => SharedWithStr = string.Join(",", value);
        }
    }

    public class Point
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    public class User
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Color { get; set; } = "#000000";
        public string? ConnectionId { get; set; }
        [JsonIgnore]
        public ICollection<Note> Notes { get; set; } = new List<Note>();
        [JsonIgnore]
        public ICollection<WhiteboardElement> WhiteboardElements { get; set; } = new List<WhiteboardElement>();
        [JsonIgnore]
        public List<Friendship> Friends { get; set; } = new List<Friendship>();
    }
    public class Friendship
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string FriendId { get; set; }
    }
}
