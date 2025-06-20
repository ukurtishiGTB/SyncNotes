using Microsoft.AspNetCore.SignalR;
using SyncNotes.Shared.Models;
using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SyncNotes.Server.Data;

namespace SyncNotes.Server.Hubs
{
    public class NotesHub : Hub
    {
        private static readonly ConcurrentDictionary<string, Note> _notes = new();
        private static readonly ConcurrentDictionary<string, WhiteboardElement> _whiteboardElements = new();
        private static readonly ConcurrentDictionary<string, string> _userConnections = new(); // userId -> connectionId
        private AppDbContext _dbContext;

        public NotesHub(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new HubException("Unauthorized: No user ID found.");
            }

            // Store the connection
            _userConnections.AddOrUpdate(userId, Context.ConnectionId, (_, _) => Context.ConnectionId);

            // Load notes for the user
            var notes = await _dbContext.Notes
                .Where(n => n.OwnerId == userId || n.SharedWithStr.Contains(userId))
                .ToListAsync();

            // Set LastModifiedByName for each note
            foreach (var note in notes)
            {
                var modifiedByUser = await _dbContext.Users.FindAsync(note.LastModifiedBy);
                note.LastModifiedByName = modifiedByUser?.Name;
                note.User = null; // Clear navigation property
            }

            // Load only whiteboard elements that the user owns or has been shared with them
            var allWhiteboardElements = await _dbContext.WhiteboardElements
                .Where(e => e.OwnerId == userId || e.SharedWithStr.Contains(userId))
                .ToListAsync();

            // Clear navigation properties
            foreach (var element in allWhiteboardElements)
            {
                element.User = null;
            }

            // Get whiteboard names that the user has access to
            var whiteboardNames = allWhiteboardElements
                .Where(e => !string.IsNullOrEmpty(e.WhiteboardName))
                .Select(e => e.WhiteboardName)
                .Distinct()
                .ToList();

            // Send initial state
            await Clients.Caller.SendAsync("ReceiveInitialState", notes, allWhiteboardElements, whiteboardNames);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrWhiteSpace(userId))
            {
                _userConnections.TryRemove(userId, out _);
            }
            await base.OnDisconnectedAsync(exception);
        }

        public async Task UpdateUser(User user)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new HubException("Unauthorized: No user ID found in claims.");
            }

            var existingUser = await _dbContext.Users.FindAsync(userId);
            if (existingUser == null)
            {
                throw new HubException("User not found.");
            }

            existingUser.Name = user.Name;
            existingUser.Color = user.Color;
            await _dbContext.SaveChangesAsync();
        }

        public async Task SendNoteUpdate(Note note)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var user= await _dbContext.Users.FindAsync(userId);
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new HubException("Unauthorized: No user ID found in claims.");
            }

            var existingNote = await _dbContext.Notes
                .FirstOrDefaultAsync(n => n.Id == note.Id);

            if (existingNote == null)
            {
                // New note
                note.OwnerId = userId.Trim();
                note.LastModified = DateTime.UtcNow;
                note.LastModifiedBy = userId.Trim();
                note.LastModifiedByName = user.Name;
                _dbContext.Notes.Add(note);
                await _dbContext.SaveChangesAsync();

                // Clear navigation properties before sending
                note.User = null;
                await Clients.All.SendAsync("ReceiveNoteUpdate", note);
            }
            else
            {
                // Check if user has permission to update
                if (existingNote.OwnerId != userId && !existingNote.SharedWith.Contains(userId))
                {
                    throw new HubException("You don't have permission to update this note.");
                }

                existingNote.NoteName = note.NoteName;
                existingNote.Content = note.Content;
                existingNote.LastModified = DateTime.UtcNow;
                existingNote.LastModifiedBy = userId.Trim();
                existingNote.LastModifiedByName = user.Name;
                await _dbContext.SaveChangesAsync();

                // Clear navigation properties before sending
                existingNote.User = null;
                await Clients.All.SendAsync("ReceiveNoteUpdate", existingNote);
            }
        }

        public async Task DeleteNote(string noteId)
        {
            var userId = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new HubException("Unauthorized: No user ID found.");
            }

            var note = await _dbContext.Notes.FindAsync(noteId);
            if (note == null)
            {
                throw new HubException("Note not found.");
            }

            // Only owner can delete
            if (note.OwnerId != userId)
            {
                throw new HubException("Only the owner can delete this note.");
            }

            _dbContext.Notes.Remove(note);
            await _dbContext.SaveChangesAsync();

            // Get all users who had access to the note
            var accessList = new HashSet<string> { note.OwnerId };
            if (!string.IsNullOrEmpty(note.SharedWithStr))
            {
                accessList.UnionWith(note.SharedWithStr.Split(',', StringSplitOptions.RemoveEmptyEntries));
            }

            // Notify all users who had access
            foreach (var accessUserId in accessList)
            {
                if (_userConnections.TryGetValue(accessUserId, out var connectionId))
                {
                    await Clients.Client(connectionId).SendAsync("NoteDeleted", noteId);
                }
            }
        }

        public async Task RequestWhiteboardElements(string whiteboardName)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new HubException("Unauthorized: No user ID found.");
            }

            if (string.IsNullOrEmpty(whiteboardName))
            {
                await Clients.Caller.SendAsync("ReceiveWhiteboardElements", new List<WhiteboardElement>());
                return;
            }

            // Get elements that the user either owns or has been shared with them
            var elements = await _dbContext.WhiteboardElements
                .Where(e => e.WhiteboardName == whiteboardName && 
                            (e.OwnerId == userId || e.SharedWithStr.Contains(userId)))
                .ToListAsync();

            // Clear navigation properties
            foreach (var element in elements)
            {
                element.User = null;
            }

            await Clients.Caller.SendAsync("ReceiveWhiteboardElements", elements);
        }

        public async Task SendWhiteboardElement(WhiteboardElement element)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var user = await _dbContext.Users.FindAsync(userId);
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new HubException("Unauthorized: No user ID found in claims.");
            }

            // Get all elements for this whiteboard to check permissions and sharing
            var whiteboardElements = await _dbContext.WhiteboardElements
                .Where(e => e.WhiteboardName == element.WhiteboardName)
                .ToListAsync();

            // If there are no elements, allow the user to create the first one
            bool hasAccess = !whiteboardElements.Any() ||
                whiteboardElements.Any(e =>
                    e.OwnerId == userId ||
                    (!string.IsNullOrEmpty(e.SharedWithStr) && e.SharedWithStr.Split(',', StringSplitOptions.RemoveEmptyEntries).Contains(userId)));

            if (!hasAccess)
            {
                throw new HubException("You don't have permission to update this whiteboard.");
            }

            var existingElement = await _dbContext.WhiteboardElements
                .FirstOrDefaultAsync(e => e.Id == element.Id);

            if (existingElement == null)
            {
                // New element
                // Get the original owner and sharing info from existing elements
                var templateElement = whiteboardElements.FirstOrDefault();
                if (templateElement != null)
                {
                    // Use the original owner's ID and sharing info
                    element.OwnerId = templateElement.OwnerId;
                    element.SharedWithStr = templateElement.SharedWithStr;
                }
                else
                {
                    // If no elements exist yet, this user becomes the owner
                    element.OwnerId = userId;
                }

                element.LastModified = DateTime.UtcNow;
                element.LastModifiedBy = userId;
                element.LastModifiedByName = user?.Name;

                _dbContext.WhiteboardElements.Add(element);
                await _dbContext.SaveChangesAsync();

                // Clear navigation properties before sending
                element.User = null;

                // Get all users who should receive the update (owner and shared users)
                var accessList = new HashSet<string>();

                // Add all owners of elements in this whiteboard
                accessList.UnionWith(whiteboardElements.Select(e => e.OwnerId));

                // Add all users the whiteboard is shared with
                foreach (var e in whiteboardElements)
                {
                    if (!string.IsNullOrEmpty(e.SharedWithStr))
                    {
                        accessList.UnionWith(e.SharedWithStr.Split(',', StringSplitOptions.RemoveEmptyEntries));
                    }
                }
                // Always add the current user (in case they're the first/only one)
                accessList.Add(element.OwnerId);

                // Send to all users who have access
                foreach (var accessUserId in accessList)
                {
                    if (_userConnections.TryGetValue(accessUserId, out var connectionId))
                    {
                        await Clients.Client(connectionId).SendAsync("ReceiveWhiteboardElement", element);
                    }
                }
            }
            else
            {
                // Update existing element
                existingElement.Type = element.Type;
                existingElement.Color = element.Color;
                existingElement.StrokeWidth = element.StrokeWidth;
                existingElement.Points = element.Points;
                existingElement.Text = element.Text;
                existingElement.WhiteboardName = element.WhiteboardName;
                existingElement.LastModified = DateTime.UtcNow;
                existingElement.LastModifiedBy = userId;
                existingElement.LastModifiedByName = user?.Name;
                await _dbContext.SaveChangesAsync();

                // Clear navigation properties before sending
                existingElement.User = null;

                // Get all users who should receive the update (owner and shared users)
                var accessList = new HashSet<string>();
                accessList.UnionWith(whiteboardElements.Select(e => e.OwnerId));
                foreach (var e in whiteboardElements)
                {
                    if (!string.IsNullOrEmpty(e.SharedWithStr))
                    {
                        accessList.UnionWith(e.SharedWithStr.Split(',', StringSplitOptions.RemoveEmptyEntries));
                    }
                }
                accessList.Add(existingElement.OwnerId);

                foreach (var accessUserId in accessList)
                {
                    if (_userConnections.TryGetValue(accessUserId, out var connectionId))
                    {
                        await Clients.Client(connectionId).SendAsync("ReceiveWhiteboardElement", existingElement);
                    }
                }
            }
        }

        public async Task DeleteWhiteboardElement(string elementId)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new HubException("Unauthorized: No user ID found.");
            }

            var element = await _dbContext.WhiteboardElements.FindAsync(elementId);
            if (element != null)
            {
                // Check if user has permission to delete
                if (element.OwnerId != userId && !element.SharedWith.Contains(userId))
                {
                    throw new HubException("You don't have permission to delete this element.");
                }

                _dbContext.WhiteboardElements.Remove(element);
                await _dbContext.SaveChangesAsync();
                await Clients.All.SendAsync("WhiteboardElementDeleted", elementId);
            }
        }

        public async Task ClearWhiteboard(string whiteboardName)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new HubException("Unauthorized: No user ID found.");
            }

            if (string.IsNullOrEmpty(whiteboardName))
            {
                throw new HubException("Whiteboard name is required.");
            }

            // Get all elements for this whiteboard
            var elements = await _dbContext.WhiteboardElements
                .Where(e => e.WhiteboardName == whiteboardName)
                .ToListAsync();

            if (!elements.Any())
            {
                return; // Nothing to clear
            }

            // Check if user has permission (is owner or has shared access)
            bool hasAccess = elements.Any(e =>
                e.OwnerId == userId ||
                (!string.IsNullOrEmpty(e.SharedWithStr) && e.SharedWithStr.Split(',', StringSplitOptions.RemoveEmptyEntries).Contains(userId)));

            if (!hasAccess)
            {
                throw new HubException("You don't have permission to clear this whiteboard.");
            }

            // Get sharing info from the first element
            var templateElement = elements.First();
            var sharedWithStr = templateElement.SharedWithStr;
            var ownerId = templateElement.OwnerId;

            // Remove all elements
            _dbContext.WhiteboardElements.RemoveRange(elements);
            await _dbContext.SaveChangesAsync();

            // Create a placeholder element to preserve sharing/ownership
            var placeholder = new WhiteboardElement
            {
                Id = Guid.NewGuid().ToString(),
                WhiteboardName = whiteboardName,
                Type = "placeholder",
                OwnerId = ownerId,
                SharedWithStr = sharedWithStr,
                LastModified = DateTime.UtcNow,
                LastModifiedBy = userId
            };

            _dbContext.WhiteboardElements.Add(placeholder);
            await _dbContext.SaveChangesAsync();

            // Notify all users who had access
            var accessList = new HashSet<string>();
            foreach (var element in elements)
            {
                accessList.Add(element.OwnerId);
                if (!string.IsNullOrEmpty(element.SharedWithStr))
                {
                    accessList.UnionWith(element.SharedWithStr.Split(',', StringSplitOptions.RemoveEmptyEntries));
                }
            }
            accessList.Add(ownerId);

            foreach (var accessUserId in accessList)
            {
                if (_userConnections.TryGetValue(accessUserId, out var connectionId))
                {
                    await Clients.Client(connectionId).SendAsync("WhiteboardCleared", whiteboardName);
                }
            }
        }

        // New method to get all whiteboard names
        public async Task GetWhiteboardNames()
        {
            var whiteboardNames = await _dbContext.WhiteboardElements
                .Where(e => !string.IsNullOrEmpty(e.WhiteboardName))
                .Select(e => e.WhiteboardName)
                .Distinct()
                .ToListAsync();

            await Clients.Caller.SendAsync("ReceiveWhiteboardNames", whiteboardNames);
        }

        // New method to create a new whiteboard
        public async Task CreateWhiteboard(string whiteboardName)
        {
            if (string.IsNullOrEmpty(whiteboardName))
            {
                throw new HubException("Whiteboard name is required.");
            }

            // Check if whiteboard already exists
            var exists = await _dbContext.WhiteboardElements
                .AnyAsync(e => e.WhiteboardName == whiteboardName);

            if (!exists)
            {
                // Create a placeholder element to establish the whiteboard
                // This will be removed when the first real element is added
                var placeholderElement = new WhiteboardElement
                {
                    Id = Guid.NewGuid().ToString(),
                    WhiteboardName = whiteboardName,
                    Type = "placeholder",
                    Color = "#000000",
                    StrokeWidth = 1,
                    Points = new List<Point>(),
                    OwnerId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "system",
                    LastModified = DateTime.UtcNow,
                    LastModifiedBy = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "system"
                };

                _dbContext.WhiteboardElements.Add(placeholderElement);
                await _dbContext.SaveChangesAsync();
            }

            await Clients.All.SendAsync("WhiteboardCreated", whiteboardName);
        }

        // Method to delete entire whiteboard
        public async Task DeleteWhiteboard(string whiteboardName)
        {
            if (string.IsNullOrEmpty(whiteboardName))
            {
                throw new HubException("Whiteboard name is required.");
            }

            var elementsToRemove = await _dbContext.WhiteboardElements
                .Where(e => e.WhiteboardName == whiteboardName)
                .ToListAsync();

            if (elementsToRemove.Any())
            {
                _dbContext.WhiteboardElements.RemoveRange(elementsToRemove);
                await _dbContext.SaveChangesAsync();

                // Remove from in-memory cache
                foreach (var element in elementsToRemove)
                {
                    _whiteboardElements.TryRemove(element.Id, out _);
                }
            }

            await Clients.All.SendAsync("WhiteboardDeleted", whiteboardName);
        }

        public async Task GetFriends()
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new HubException("Unauthorized: No user ID found.");
            }

            // Get all friendships where the user is either UserId or FriendId
            var friendships = await _dbContext.Friendships
                .Where(f => f.UserId == userId || f.FriendId == userId)
                .ToListAsync();

            // Get all friend IDs
            var friendIds = friendships
                .SelectMany(f => new[] { f.UserId, f.FriendId })
                .Where(id => id != userId)
                .Distinct()
                .ToList();

            // Get friend details
            var friends = await _dbContext.Users
                .Where(u => friendIds.Contains(u.Id))
                .ToListAsync();

            await Clients.Caller.SendAsync("FriendsReceived", friends);
        }

        public async Task AddFriend(string email)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new HubException("Unauthorized: No user ID found.");
            }

            var friend = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Email == email);

            if (friend == null)
            {
                throw new HubException("User not found.");
            }

            if (friend.Id == userId)
            {
                throw new HubException("Cannot add yourself as a friend.");
            }

            // Check if friendship already exists
            var existingFriendship = await _dbContext.Friendships
                .FirstOrDefaultAsync(f => f.UserId == userId && f.FriendId == friend.Id);

            if (existingFriendship != null)
            {
                throw new HubException("Friendship already exists.");
            }

            var friendship = new Friendship
            {
                UserId = userId,
                FriendId = friend.Id
            };

            _dbContext.Friendships.Add(friendship);
            await _dbContext.SaveChangesAsync();

            await Clients.Caller.SendAsync("FriendAdded", friend);
        }

        public async Task RemoveFriend(string friendId)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new HubException("Unauthorized: No user ID found.");
            }

            var friendship = await _dbContext.Friendships
                .FirstOrDefaultAsync(f => f.UserId == userId && f.FriendId == friendId);

            if (friendship != null)
            {
                _dbContext.Friendships.Remove(friendship);
                await _dbContext.SaveChangesAsync();
            }

            await Clients.Caller.SendAsync("FriendRemoved", friendId);
        }

        public async Task ShareNote(string noteId, string friendId)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new HubException("Unauthorized: No user ID found.");
            }

            var note = await _dbContext.Notes
                .Include(n => n.User)
                .FirstOrDefaultAsync(n => n.Id == noteId);
            
            if (note == null)
            {
                throw new HubException("Note not found.");
            }

            if (note.OwnerId != userId)
            {
                throw new HubException("You don't have permission to share this note.");
            }

            // Check if friendship exists
            var friendship = await _dbContext.Friendships
                .FirstOrDefaultAsync(f => 
                    (f.UserId == userId && f.FriendId == friendId) || 
                    (f.UserId == friendId && f.FriendId == userId));

            if (friendship == null)
            {
                throw new HubException("Cannot share with non-friend.");
            }

            // Add to SharedWith if not already shared
            var sharedWith = note.SharedWith;
            if (!sharedWith.Contains(friendId))
            {
                sharedWith.Add(friendId);
                note.SharedWith = sharedWith; // This will update SharedWithStr
                await _dbContext.SaveChangesAsync();

                // Get all active connections for both the owner and the friend
                var relevantConnections = _userConnections.Values
                    .Where(u => u == userId || u == friendId)
                    .Select(u => u)
                    .Where(c => !string.IsNullOrEmpty(c));

                // Notify all relevant connections about the shared note
                foreach (var connectionId in relevantConnections)
                {
                    await Clients.Client(connectionId).SendAsync("NoteShared", note);
                }
            }
        }

        public async Task ShareWhiteboard(string whiteboardName, string friendId)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new HubException("Unauthorized: No user ID found.");
            }

            // Check if friendship exists
            var friendship = await _dbContext.Friendships
                .FirstOrDefaultAsync(f => 
                    (f.UserId == userId && f.FriendId == friendId) || 
                    (f.UserId == friendId && f.FriendId == userId));

            if (friendship == null)
            {
                throw new HubException("Cannot share with non-friend.");
            }

            var elements = await _dbContext.WhiteboardElements
                .Where(e => e.WhiteboardName == whiteboardName)
                .ToListAsync();

            if (!elements.Any())
            {
                // If no elements exist, create a placeholder to establish ownership
                var placeholderElement = new WhiteboardElement
                {
                    Id = Guid.NewGuid().ToString(),
                    WhiteboardName = whiteboardName,
                    Type = "placeholder",
                    OwnerId = userId,
                    LastModified = DateTime.UtcNow,
                    LastModifiedBy = userId,
                    SharedWithStr = friendId // Initialize sharing
                };
                _dbContext.WhiteboardElements.Add(placeholderElement);
                elements.Add(placeholderElement);
                await _dbContext.SaveChangesAsync();
            }
            else
            {
                // Check if user owns any elements in the whiteboard
                var hasPermission = elements.Any(e => e.OwnerId == userId);
                if (!hasPermission)
                {
                    throw new HubException("You don't have permission to share this whiteboard.");
                }

                // Update sharing for all elements
                foreach (var element in elements)
                {
                    var sharedWith = new HashSet<string>();
                    if (!string.IsNullOrEmpty(element.SharedWithStr))
                    {
                        sharedWith.UnionWith(element.SharedWithStr.Split(',', StringSplitOptions.RemoveEmptyEntries));
                    }
                    sharedWith.Add(friendId);
                    element.SharedWithStr = string.Join(",", sharedWith);
                }
                await _dbContext.SaveChangesAsync();
            }

            // Clear navigation properties before sending
            foreach (var element in elements)
            {
                element.User = null;
            }

            // Get friend's connection ID
            if (_userConnections.TryGetValue(friendId, out var friendConnectionId))
            {
                await Clients.Client(friendConnectionId).SendAsync("WhiteboardShared", whiteboardName);
                await Clients.Client(friendConnectionId).SendAsync("ReceiveWhiteboardElements", elements);
            }

            // Also notify the owner about successful sharing
            await Clients.Caller.SendAsync("WhiteboardShared", whiteboardName);
        }
    }
}