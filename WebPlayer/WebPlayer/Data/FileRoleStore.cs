using Microsoft.AspNetCore.Identity;
using System.Text.Json;

namespace WebPlayer.Data
{
    public class FileRoleStore : IRoleStore<ApplicationRole>
    {
        private readonly string _file = "roles.json";
        private List<ApplicationRole> _roles;
        private const string roleNameAdmin = "admin";


        public FileRoleStore()
        {
            _roles = File.Exists(_file)
                ? JsonSerializer.Deserialize<List<ApplicationRole>>(File.ReadAllText(_file))
                : new List<ApplicationRole>();
            if (!_roles.Any())
                _ = CreateAsync(new ApplicationRole() { Name = roleNameAdmin, NormalizedName = roleNameAdmin.ToUpper(), Id = Guid.NewGuid().ToString()  }, CancellationToken.None);
        }

        public Task<IdentityResult> CreateAsync(ApplicationRole role, CancellationToken cancellationToken)
        {
            _roles.Add(role);
            Save();
            return Task.FromResult(IdentityResult.Success);
        }

        public Task<IdentityResult> DeleteAsync(ApplicationRole role, CancellationToken cancellationToken)
        {
            _roles.RemoveAll(r => r.Id == role.Id);
            Save();
            return Task.FromResult(IdentityResult.Success);
        }

        public Task<IdentityResult> UpdateAsync(ApplicationRole role, CancellationToken cancellationToken)
        {
            var index = _roles.FindIndex(r => r.Id == role.Id);
            if (index >= 0)
            {
                _roles[index] = role;
                Save();
                return Task.FromResult(IdentityResult.Success);
            }
            return Task.FromResult(IdentityResult.Failed());
        }

        public Task<ApplicationRole> FindByIdAsync(string roleId, CancellationToken cancellationToken)
            => Task.FromResult(_roles.FirstOrDefault(r => r.Id == roleId));

        public Task<ApplicationRole> FindByNameAsync(string normalizedRoleName, CancellationToken cancellationToken)
            => Task.FromResult(_roles.FirstOrDefault(r => r.NormalizedName == normalizedRoleName));

        public Task<string> GetNormalizedRoleNameAsync(ApplicationRole role, CancellationToken cancellationToken)
            => Task.FromResult(role.NormalizedName);

        public Task<string> GetRoleIdAsync(ApplicationRole role, CancellationToken cancellationToken)
            => Task.FromResult(role.Id);

        public Task<string> GetRoleNameAsync(ApplicationRole role, CancellationToken cancellationToken)
            => Task.FromResult(role.Name);

        public Task SetNormalizedRoleNameAsync(ApplicationRole role, string normalizedName, CancellationToken cancellationToken)
        {
            role.NormalizedName = normalizedName;
            return Task.CompletedTask;
        }

        public Task SetRoleNameAsync(ApplicationRole role, string roleName, CancellationToken cancellationToken)
        {
            role.Name = roleName;
            return Task.CompletedTask;
        }

        public void Dispose() { }

        private void Save() => File.WriteAllText(_file, JsonSerializer.Serialize(_roles));

    }
}
