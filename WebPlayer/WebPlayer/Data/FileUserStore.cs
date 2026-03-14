using Microsoft.AspNetCore.Identity;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.Json;
using System.Threading;
using WebPlayerApi.Models;

namespace WebPlayer.Data
{
    public class FileUserStore :
    IUserStore<ApplicationUser>,
    IUserPasswordStore<ApplicationUser>,
    IUserEmailStore<ApplicationUser>,
    IUserRoleStore<ApplicationUser>,
        IUserCollection
    {
        private readonly string _userFile = "users.json";
        private readonly string _userRolesFile = "userRoles.json";
        private List<ApplicationUser> _users;
        private Dictionary<string, List<string>> _userRoles;
        private const string roleNameAdmin = "admin";

        public FileUserStore()
        {
            _users = File.Exists(_userFile)
                ? JsonSerializer.Deserialize<List<ApplicationUser>>(File.ReadAllText(_userFile))
                : new List<ApplicationUser>();

            _userRoles = File.Exists(_userRolesFile)
                ? JsonSerializer.Deserialize<Dictionary<string, List<string>>>(File.ReadAllText(_userRolesFile))
                : new Dictionary<string, List<string>>();

            if (_users.Count == 1 && !_userRoles.Any())
                AddToRoleAsync(_users.FirstOrDefault(), roleNameAdmin, CancellationToken.None);

            //_users.Clear();
            //_userRoles.Clear();
            //SaveRoles();
            //SaveUsers();
        }

        public IEnumerable<IApplicationUser> GetAll()
        {
            return _users.Select(user =>
            {
                if (string.IsNullOrWhiteSpace(user.UserName))
                    user.UserName = user.NormalizedUserName;
                if (string.IsNullOrWhiteSpace(user.UserName))
                    user.UserName = user.Email;
                return user;
            }).Cast<IApplicationUser>().ToList();
        }
        public void ChangeAccess(MediaDirectory dir, IApplicationUser[] users)
        {
            foreach (var user in GetAll())
            {
                var access = users.Any(u => u.Id == user.Id);
                user.Sources = string.Join(';', (user.Sources ?? "")
                    .Split(';')
                    .Concat(new string[] { dir.Name})
                    .Distinct()
                    .Where(name => name != dir.Name || access));
            }
            SaveUsers();
        }
        public bool HasAccess(IApplicationUser user, MediaDirectory? source)
        {
            var currUser = GetAll().FirstOrDefault(u => u.Id == user.Id);
            return (currUser.Sources ?? "").Split(';').Contains(source?.Name);
        }

        public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            _users.Add(user);
            SaveUsers();
            if (_users.Count == 1)
                AddToRoleAsync(user, roleNameAdmin, cancellationToken);            
            return Task.FromResult(IdentityResult.Success);
        }

        public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            var index = GetAll().ToList().FindIndex(u => u.Id == user.Id);
            if (index >= 0)
            {
                _users[index] = user;
                SaveUsers();
                return Task.FromResult(IdentityResult.Success);
            }
            return Task.FromResult(IdentityResult.Failed());
        }

        public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            _users.RemoveAll(u => u.Id == user.Id);
            _userRoles.Remove(user.Id);
            SaveUsers();
            SaveRoles();
            return Task.FromResult(IdentityResult.Success);
        }

        public Task<ApplicationUser> FindByIdAsync(string userId, CancellationToken cancellationToken)
            => Task.FromResult(_users.FirstOrDefault(u => u.Id == userId));

        public Task<ApplicationUser> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
            => Task.FromResult(_users.FirstOrDefault(u => u.NormalizedUserName == normalizedUserName));

        public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken)
            => Task.FromResult(user.Id);

        public Task<string> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken)
            => Task.FromResult(user.UserName);

        public Task SetUserNameAsync(ApplicationUser user, string userName, CancellationToken cancellationToken)
        {
            user.UserName = userName;
            return Task.CompletedTask;
        }

        public Task<string> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken)
            => Task.FromResult(user.NormalizedUserName);

        public Task SetNormalizedUserNameAsync(ApplicationUser user, string normalizedName, CancellationToken cancellationToken)
        {
            user.NormalizedUserName = normalizedName;
            return Task.CompletedTask;
        }

        // Passwort
        public Task SetPasswordHashAsync(ApplicationUser user, string passwordHash, CancellationToken cancellationToken)
        {
            user.PasswordHash = passwordHash;
            return Task.CompletedTask;
        }

        public Task<string> GetPasswordHashAsync(ApplicationUser user, CancellationToken cancellationToken)
            => Task.FromResult(user.PasswordHash);

        public Task<bool> HasPasswordAsync(ApplicationUser user, CancellationToken cancellationToken)
            => Task.FromResult(!string.IsNullOrEmpty(user.PasswordHash));

        // E-Mail
        public Task SetEmailAsync(ApplicationUser user, string email, CancellationToken cancellationToken)
        {
            user.Email = email;
            return Task.CompletedTask;
        }

        public Task<string> GetEmailAsync(ApplicationUser user, CancellationToken cancellationToken)
            => Task.FromResult(user.Email);

        public Task<bool> GetEmailConfirmedAsync(ApplicationUser user, CancellationToken cancellationToken)
            => Task.FromResult(user.EmailConfirmed);

        public Task SetEmailConfirmedAsync(ApplicationUser user, bool confirmed, CancellationToken cancellationToken)
        {
            user.EmailConfirmed = confirmed;
            return Task.CompletedTask;
        }

        public Task<string> GetNormalizedEmailAsync(ApplicationUser user, CancellationToken cancellationToken)
            => Task.FromResult(user.NormalizedEmail);

        public Task SetNormalizedEmailAsync(ApplicationUser user, string normalizedEmail, CancellationToken cancellationToken)
        {
            user.NormalizedEmail = normalizedEmail;
            return Task.CompletedTask;
        }

        public Task<ApplicationUser> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
            => Task.FromResult(_users.FirstOrDefault(u => u.NormalizedEmail == normalizedEmail));

        // Rollen
        public Task AddToRoleAsync(ApplicationUser user, string roleName, CancellationToken cancellationToken)
        {
            if (!_userRoles.TryGetValue(user.Id, out var roles))
            {
                roles = new List<string>();
                _userRoles[user.Id] = roles;
            }

            if (!roles.Contains(roleName))
            {
                roles.Add(roleName);
                SaveRoles();
            }

            return Task.CompletedTask;
        }

        public Task RemoveFromRoleAsync(ApplicationUser user, string roleName, CancellationToken cancellationToken)
        {
            if (_userRoles.TryGetValue(user.Id, out var roles))
            {
                roles.Remove(roleName);
                SaveRoles();
            }

            return Task.CompletedTask;
        }

        public Task<IList<string>> GetRolesAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            if (_userRoles.TryGetValue(user.Id, out var roles))
                return Task.FromResult<IList<string>>(roles);
            return Task.FromResult<IList<string>>(new List<string>());
        }

        public Task<bool> IsInRoleAsync(ApplicationUser user, string roleName, CancellationToken cancellationToken)
        {
            return Task.FromResult(_userRoles.TryGetValue(user.Id, out var roles) && roles.Contains(roleName));
        }

        public Task<IList<ApplicationUser>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken)
        {
            var usersInRole = _users.Where(u => _userRoles.TryGetValue(u.Id, out var roles) && roles.Contains(roleName)).ToList();
            return Task.FromResult<IList<ApplicationUser>>(usersInRole);
        }

        public void Dispose() { }

        private void SaveUsers() => File.WriteAllText(_userFile, JsonSerializer.Serialize(_users));
        private void SaveRoles() => File.WriteAllText(_userRolesFile, JsonSerializer.Serialize(_userRoles));
    }
}
