using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using SyncNotes.Shared.Models;

namespace SyncNotes.Client.Services
{
    public class NoteHubService
    {
        private HubConnection _connection;
        private readonly ILogger<NoteHubService> _logger;
        private readonly string _hubUrl;
        private readonly NavigationManager _navigationManager;
        
        private readonly ILocalStorageService _localStorage;


        public event Action<List<Note>, List<WhiteboardElement>, List<string>>? OnInitialStateReceived; 
        public event Action<Note> OnNoteReceived;
        public event Action<string> OnNoteDeleted;
        public event Action<Note> OnNoteShared;
        public event Action<WhiteboardElement> OnWhiteboardElementReceived;
        public event Action<string> OnWhiteboardElementDeleted;
        public event Action<string> OnWhiteboardCleared;
        public event Action<User> OnUserJoined;
        public event Action<string> OnUserLeft;
        public event Action<User> OnUserUpdated;
        public event Action<string> OnConnectionError;
        public event Action OnReconnected;
        public event Action OnDisconnected;
        public event Action<List<WhiteboardElement>>? OnWhiteboardElementsReceived;
        public event Action<string>? OnWhiteboardCreated;
        public event Action<string>? OnWhiteboardDeleted;
        public event Action<string>? OnWhiteboardShared;
        public event Action<User>? OnFriendAdded;
        public event Action<string>? OnFriendRemoved;
        public event Action<List<User>>? OnFriendsReceived;

        public NoteHubService(IConfiguration configuration, ILogger<NoteHubService> logger, NavigationManager navigationManager, ILocalStorageService localStorage)
        {
            _hubUrl = configuration["HubUrl"] ?? "https://syncnotes-api-bpdbcghychh7hahg.westeurope-01.azurewebsites.net/notehub";
            _logger = logger;
            _navigationManager = navigationManager;
            _localStorage = localStorage;
            
        }

        public async Task StartAsync()
        {
            _connection = new HubConnectionBuilder()
                .WithUrl(_navigationManager.ToAbsoluteUri("https://syncnotes-api-bpdbcghychh7hahg.westeurope-01.azurewebsites.net/notehub"), options =>
                {
                    options.AccessTokenProvider = async () =>
                    {
                        var token = await _localStorage.GetItemAsync<string>("authToken");
                        return token;
                    };
                })
                .WithAutomaticReconnect()
                .Build();

            SetupEventHandlers();

            try
            {
                await _connection.StartAsync();
                _logger.LogInformation("Connected to SignalR hub");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error connecting to SignalR hub");
                OnConnectionError?.Invoke(ex.Message);
            }
        }

        private void SetupEventHandlers()
        {
            _connection.On<List<Note>, List<WhiteboardElement>, List<string>>("ReceiveInitialState", (notes, elements, whiteboardNames) =>
            {
                OnInitialStateReceived?.Invoke(notes, elements, whiteboardNames);
            });

            _connection.On<Note>("ReceiveNoteUpdate", note =>
            {
                OnNoteReceived?.Invoke(note);
            });

            _connection.On<Note>("NoteShared", note =>
            {
                OnNoteShared?.Invoke(note);
                OnNoteReceived?.Invoke(note);
            });

            _connection.On<string>("NoteDeleted", 
                noteId => OnNoteDeleted?.Invoke(noteId));

            
            _connection.On<string>("WhiteboardCreated", whiteboardName =>
            {
                OnWhiteboardCreated?.Invoke(whiteboardName);
            });

            _connection.On<string>("WhiteboardShared", whiteboardName =>
            {
                OnWhiteboardShared?.Invoke(whiteboardName);
            });

            _connection.On<List<WhiteboardElement>>("ReceiveWhiteboardElements", elements =>
            {
                OnWhiteboardElementsReceived?.Invoke(elements);
            });

            _connection.On<WhiteboardElement>("ReceiveWhiteboardElement", element =>
            {
                OnWhiteboardElementReceived?.Invoke(element);
            });

            _connection.On<string>("WhiteboardElementDeleted", 
                elementId => OnWhiteboardElementDeleted?.Invoke(elementId));

            _connection.On<string>("WhiteboardCleared", whiteboardName =>
            {
                OnWhiteboardCleared?.Invoke(whiteboardName);
            });

            _connection.On<string>("WhiteboardDeleted", whiteboardName =>
            {
                OnWhiteboardDeleted?.Invoke(whiteboardName);
            });

            _connection.On<User>("UserJoined", 
                user => OnUserJoined?.Invoke(user));

            _connection.On<string>("UserLeft", 
                userId => OnUserLeft?.Invoke(userId));

            _connection.On<User>("UserUpdated", 
                user => OnUserUpdated?.Invoke(user));

            _connection.On<User>("FriendAdded", friend =>
            {
                OnFriendAdded?.Invoke(friend);
            });

            _connection.On<string>("FriendRemoved", friendId =>
            {
                OnFriendRemoved?.Invoke(friendId);
            });

            _connection.On<List<User>>("FriendsReceived", friends =>
            {
                OnFriendsReceived?.Invoke(friends);
            });

            _connection.Reconnecting += error =>
            {
                _logger.LogWarning(error, "Reconnecting to SignalR hub");
                return Task.CompletedTask;
            };

            _connection.Reconnected += connectionId =>
            {
                _logger.LogInformation("Reconnected to SignalR hub");
                OnReconnected?.Invoke();
                return Task.CompletedTask;
            };

            _connection.Closed += error =>
            {
                _logger.LogWarning(error, "Connection to SignalR hub closed");
                OnDisconnected?.Invoke();
                return Task.CompletedTask;
            };
        }

        public async Task UpdateUser(User user)
        {
            try
            {
                await _connection.InvokeAsync("UpdateUser", user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user");
                OnConnectionError?.Invoke(ex.Message);
            }
        }

        public async Task SendNote(Note note)
        {
            try
            {
                await _connection.InvokeAsync("SendNoteUpdate", note);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending note");
                OnConnectionError?.Invoke(ex.Message);
            }
        }

        public async Task DeleteNote(string noteId)
        {
            try
            {
                await _connection.InvokeAsync("DeleteNote", noteId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting note");
                OnConnectionError?.Invoke(ex.Message);
            }
        }
        public async Task RequestWhiteboardElements(string whiteboardName)
        {
            await _connection.InvokeAsync("RequestWhiteboardElements", whiteboardName);
        }

        public async Task SendWhiteboardElement(WhiteboardElement element)
        {
            try
            {
                await _connection.InvokeAsync("SendWhiteboardElement", element);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending whiteboard element");
                OnConnectionError?.Invoke(ex.Message);
            }
        }

        public async Task DeleteWhiteboardElement(string elementId)
        {
            try
            {
                await _connection.InvokeAsync("DeleteWhiteboardElement", elementId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting whiteboard element");
                OnConnectionError?.Invoke(ex.Message);
            }
        }

        public async Task ClearWhiteboard(string whiteboardName)
        {
            await _connection.InvokeAsync("ClearWhiteboard", whiteboardName);
        }

        public async Task GetFriends()
        {
            try
            {
                await _connection.InvokeAsync("GetFriends");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting friends");
                OnConnectionError?.Invoke(ex.Message);
            }
        }

        public async Task AddFriend(string email)
        {
            try
            {
                await _connection.InvokeAsync("AddFriend", email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding friend");
                OnConnectionError?.Invoke(ex.Message);
            }
        }

        public async Task RemoveFriend(string friendId)
        {
            try
            {
                await _connection.InvokeAsync("RemoveFriend", friendId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing friend");
                OnConnectionError?.Invoke(ex.Message);
            }
        }

        public async Task ShareNote(string noteId, string friendId)
        {
            try
            {
                await _connection.InvokeAsync("ShareNote", noteId, friendId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sharing note");
                OnConnectionError?.Invoke(ex.Message);
            }
        }

        public async Task ShareWhiteboard(string whiteboardName, string friendId)
        {
            try
            {
                await _connection.InvokeAsync("ShareWhiteboard", whiteboardName, friendId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sharing whiteboard");
                OnConnectionError?.Invoke(ex.Message);
            }
        }

        public async Task StopAsync()
        {
            if (_connection != null)
            {
                await _connection.StopAsync();
                await _connection.DisposeAsync();
            }
        }
    }
}