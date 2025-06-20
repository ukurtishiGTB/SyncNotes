using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using SyncNotes.Shared.Models.ViewModels;
using Microsoft.JSInterop;

namespace SyncNotes.Client.Services
{
    public class AuthService
    {
        private readonly HttpClient _http;
        private readonly IJSRuntime _js;
        private readonly CustomAuthStateProvider _authStateProvider;
        private readonly AuthenticationStateProvider _authProvider;
        private readonly NavigationManager _navigationManager;
        

        private const string TokenKey = "authToken";

        public string? Token { get; private set; }
        public UserResponseViewmodel? CurrentUser { get; private set; }

        public AuthService(HttpClient http, IJSRuntime js,CustomAuthStateProvider authStateProvider, AuthenticationStateProvider authProvide, NavigationManager navigationManager)
        {
            _http = http;
            _js = js;
            _authStateProvider = authStateProvider;
            _authProvider = authProvide;
            _navigationManager = navigationManager;
        }

        public async Task InitializeAsync()
        {
            // Load token from storage
            Token = await _js.InvokeAsync<string>("localStorage.getItem", TokenKey);
            
            // Also try to load user data
            var userJson = await _js.InvokeAsync<string>("localStorage.getItem", "user");
            
            if (!string.IsNullOrEmpty(Token))
            {
                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
                
                // If we have user data in localStorage, use it
                if (!string.IsNullOrEmpty(userJson))
                {
                    try {
                        CurrentUser = JsonSerializer.Deserialize<UserResponseViewmodel>(userJson);
                        _authStateProvider.SetUser(CurrentUser);
                        return; // User loaded successfully from storage
                    }
                    catch {}
                }
                
                // Otherwise try to extract from token
                try {
                    var payload = Token.Split('.')?[1];
                    if (payload != null)
                    {
                        var json = JsonSerializer.Deserialize<JsonElement>(Base64Decode(payload));
                        var name = json.GetProperty("name").GetString();
                        var id = json.GetProperty("nameid").GetString();
                        CurrentUser = new UserResponseViewmodel { Id = id!, Name = name! };
                        _authStateProvider.SetUser(CurrentUser);
                        
                        // Store the user object for future use
                        await _js.InvokeVoidAsync("localStorage.setItem", "user", JsonSerializer.Serialize(CurrentUser));
                    }
                }
                catch (Exception ex)
                {
                    // Handle token parsing errors
                    Console.WriteLine($"Error parsing token: {ex.Message}");
                    await Logout(); // Clear invalid token
                }
            }
        }

        public async Task<bool> Login(UserLoginViewmodel model)
        {
            var response = await _http.PostAsJsonAsync("api/users/login", model);
            if (!response.IsSuccessStatusCode) return false;
        
            var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
            Token = result!.Token;
            CurrentUser = result.User;
        
            await _js.InvokeVoidAsync("localStorage.setItem", TokenKey, Token);
            await _js.InvokeVoidAsync("localStorage.setItem", "user", JsonSerializer.Serialize(CurrentUser));
            
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
            _authStateProvider.SetUser(CurrentUser);
            return true;
        }
        public async Task<bool> Register(UserRegisterViewmodel model)
        {
            var response = await _http.PostAsJsonAsync("api/users/register", model);
            return response.IsSuccessStatusCode;
        }

        public async Task Logout()
        {
            Token = null;
            CurrentUser = null;
            _http.DefaultRequestHeaders.Authorization = null;
            await _js.InvokeVoidAsync("localStorage.removeItem", TokenKey);
            await _js.InvokeVoidAsync("localStorage.removeItem", "user");
            _authStateProvider.SetUser(null);
            
            // Navigate to home page
            _navigationManager.NavigateTo("/");;
        }

        private string Base64Decode(string payload)
        {
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            var bytes = Convert.FromBase64String(payload);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }

        private class LoginResponse
        {
            public string Token { get; set; }
            public UserResponseViewmodel User { get; set; }
        }
    }
}