using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services.Authentication;
using static Org.BouncyCastle.Math.EC.ECCurve;

namespace VideoWebPlayer.Controllers
{
    public class ApiBaseController: ControllerBase
    {
        private IAuthService _authService;
        private readonly ILogger _logger;

        public ApiBaseController(IAuthService authService, ILogger logger)
            :base()
        {
            _authService = authService;
            _logger = logger;
        }
        protected ILogger Logger => _logger;
        protected ApplicationUser? CurrentUser
        {
            get
            {
                return _authService.CurrentUser;
            }
        }

        protected void CheckLogedIn()
        {
            if (CurrentUser is null)
                throw new UnauthorizedAccessException("Sie sind nicht angemeldet.");
        }

        protected T Create<T>(object ms)
        {
            var sourceType = ms.GetType();
            var record = Activator.CreateInstance<T>();
            foreach (var prop in typeof(T).GetProperties().Where(p => !p.GetCustomAttributes(typeof(IgnoreAssignPropertyAttribute), false).Any()))
            {
                var sourceProp = sourceType.GetProperty(prop.Name);
                if (sourceProp != null && sourceProp.CanRead)
                {
                    var value = sourceProp.GetValue(ms);
                    prop.SetValue(record, value);
                }
            }
            return record;
        }

        internal async Task<AuthorizationToken> LoginAsync(AuthenticationRequest request)
        {
            return await _authService.LoginAsync(request);
        }

        internal async Task<AuthorizationToken> Impersonate(ImpersonateRequest request)
        {
            return await _authService.ImpersonateAsync(request);
        }
    }
}
