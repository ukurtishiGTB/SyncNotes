using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using SyncNotes.Shared.Models.ViewModels;

namespace SyncNotes.Client.Services
{
    public class CustomAuthStateProvider : AuthenticationStateProvider
    {
        private UserResponseViewmodel? _currentUser;
        private readonly IJSRuntime _js;

        public CustomAuthStateProvider(IJSRuntime js)
        {
            _js = js;
        }

        public void SetUser(UserResponseViewmodel? user)
        {
            _currentUser = user;
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            if (_currentUser != null)
            {
                var identity = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, _currentUser.Name),
                    new Claim(ClaimTypes.NameIdentifier, _currentUser.Id)
                }, "CustomAuth");

                var user = new ClaimsPrincipal(identity);
                return Task.FromResult(new AuthenticationState(user));
            }
            var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
            return Task.FromResult(new AuthenticationState(anonymous));
        }

        public void NotifyUserChanged()
        {
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }

        public async Task LoadUserFromStorageAsync()
        {
            var userJson = await _js.InvokeAsync<string>("localStorage.getItem", "user");
            if (!string.IsNullOrEmpty(userJson))
            {
                var user = JsonSerializer.Deserialize<UserResponseViewmodel>(userJson);
                SetUser(user);
            }
            else
            {
                SetUser(null);
            }
        }
    }
}