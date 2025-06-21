using train2.Models;

namespace train4.Services.Interfaces
{
    public interface IAuthenticationService
    {
        Task<(bool Success, string ErrorMessage)> ConnectAsync(string login, string password, string email = null);
        Task<(string Username, string Role)> GetCurrentUserInfoAsync();
        Task<bool> ValidateUserCredentialsAsync(string login, string email);
        Task LogoutAsync();
        Task<Client> GetClientByLoginAsync(string login);
        //Task<(bool Success, string ErrorMessage)> UpdateClientAsync(Client client);
        Task<(bool Success, string ErrorMessage)> UpdateClientAsync(Client client);
        Task<(bool Success, string ErrorMessage)> CreateClientAsync(Client client, string password);
        Task<List<Client>> SearchClientsAsync(string query);
    }
}
