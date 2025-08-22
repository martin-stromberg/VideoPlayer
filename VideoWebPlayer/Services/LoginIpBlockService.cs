using System.Collections.Concurrent;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services
{
    public interface ILoginIpBlockService
    {
        bool IsBlocked(IPAddress? ip);
        void RegisterFailure(IPAddress? ip);
        void RegisterSuccess(IPAddress? ip);
        int GetFailureCount(IPAddress? ip);
        IEnumerable<BlockedLoginIp> GetBlockedIps();
        bool Unblock(string ip);
    }

    internal sealed class LoginIpBlockService : ILoginIpBlockService
    {
        private sealed class IpInfo
        {
            public int Failures;
            public bool Blocked;
            public DateTime FirstFailureUtc;
            public DateTime LastFailureUtc;
        }

        private readonly ConcurrentDictionary<string, IpInfo> _cache = new();
        private readonly IServiceScopeFactory _scopeFactory;
        private const int Threshold = 5;
        private int _initialized;

        public LoginIpBlockService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
            LoadBlockedIps();
        }

        private void LoadBlockedIps()
        {
            if (Interlocked.Exchange(ref _initialized, 1) == 1) return;
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            foreach (var blocked in db.BlockedLoginIps.AsNoTracking())
            {
                _cache[blocked.Ip] = new IpInfo
                {
                    Failures = blocked.Failures,
                    Blocked = true,
                    FirstFailureUtc = blocked.BlockedAtUtc,
                    LastFailureUtc = blocked.BlockedAtUtc
                };
            }
        }

        public bool IsBlocked(IPAddress? ip)
        {
            if (ip == null) return false;
            var key = Normalize(ip);
            if (_cache.TryGetValue(key, out var info))
                return info.Blocked;

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var dbBlocked = db.BlockedLoginIps.Find(key);
            if (dbBlocked != null)
            {
                _cache[key] = new IpInfo
                {
                    Failures = dbBlocked.Failures,
                    Blocked = true,
                    FirstFailureUtc = dbBlocked.BlockedAtUtc,
                    LastFailureUtc = dbBlocked.BlockedAtUtc
                };
                return true;
            }
            return false;
        }

        public int GetFailureCount(IPAddress? ip)
        {
            if (ip == null) return 0;
            return _cache.TryGetValue(Normalize(ip), out var info) ? info.Failures : 0;
        }

        public void RegisterFailure(IPAddress? ip)
        {
            if (ip == null) return;
            var key = Normalize(ip);
            var info = _cache.AddOrUpdate(key,
                _ => new IpInfo
                {
                    Failures = 1,
                    Blocked = false,
                    FirstFailureUtc = DateTime.UtcNow,
                    LastFailureUtc = DateTime.UtcNow
                },
                (_, existing) =>
                {
                    existing.Failures++;
                    existing.LastFailureUtc = DateTime.UtcNow;
                    return existing;
                });

            if (!info.Blocked && info.Failures >= Threshold)
            {
                info.Blocked = true;
                PersistBlock(key, info.Failures);
            }
        }

        public void RegisterSuccess(IPAddress? ip)
        {
            if (ip == null) return;
            var key = Normalize(ip);
            if (_cache.TryGetValue(key, out var info) && info.Blocked)
                return; // bleibt gesperrt
            _cache.TryRemove(key, out _);
        }

        private void PersistBlock(string ip, int failures)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            if (db.BlockedLoginIps.Find(ip) != null) return;
            db.BlockedLoginIps.Add(new BlockedLoginIp
            {
                Ip = ip,
                BlockedAtUtc = DateTime.UtcNow,
                Failures = failures
            });
            db.SaveChanges();
        }

        public IEnumerable<BlockedLoginIp> GetBlockedIps()
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return db.BlockedLoginIps
                .AsNoTracking()
                .OrderByDescending(b => b.BlockedAtUtc)
                .ToList();
        }

        public bool Unblock(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip)) return false;
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var entry = db.BlockedLoginIps.Find(ip);
            if (entry == null) return false;
            db.BlockedLoginIps.Remove(entry);
            db.SaveChanges();
            _cache.TryRemove(ip, out _);
            return true;
        }

        private static string Normalize(IPAddress ip)
        {
            if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
            return ip.ToString();
        }
    }
}