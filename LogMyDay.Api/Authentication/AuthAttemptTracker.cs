using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Authentication;

public class AuthAttemptTracker
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<AuthAttemptTracker> _logger;
    private readonly TimeSpan _windowDuration = TimeSpan.FromMinutes(15);
    private readonly int _maxAttempts = 5;

    public AuthAttemptTracker(IMemoryCache cache, ILogger<AuthAttemptTracker> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public class AttemptInfo
    {
        public int FailedAttempts { get; set; }
        public DateTime FirstFailure { get; set; }
        public DateTime LastFailure { get; set; }
        public DateTime? LockoutUntil { get; set; }
    }

    public bool IsBlocked(string identifier)
    {
        var key = $"auth_attempts_{identifier}";
        if (!_cache.TryGetValue(key, out AttemptInfo? attemptInfo) || attemptInfo == null)
        {
            return false;
        }

        // Check if lockout period has expired
        if (attemptInfo.LockoutUntil.HasValue && DateTime.UtcNow >= attemptInfo.LockoutUntil.Value)
        {
            _cache.Remove(key);
            _logger.LogInformation("[AuthTracker] Lockout expired for {Identifier}", identifier);
            return false;
        }

        // Check if current window has expired
        if (DateTime.UtcNow - attemptInfo.FirstFailure > _windowDuration)
        {
            _cache.Remove(key);
            return false;
        }

        return attemptInfo.LockoutUntil.HasValue && DateTime.UtcNow < attemptInfo.LockoutUntil.Value;
    }

    public void RecordFailedAttempt(string identifier)
    {
        var key = $"auth_attempts_{identifier}";
        var now = DateTime.UtcNow;

        if (_cache.TryGetValue(key, out AttemptInfo? existingInfo) && existingInfo != null)
        {
            // Check if we're still within the window
            if (now - existingInfo.FirstFailure <= _windowDuration)
            {
                existingInfo.FailedAttempts++;
                existingInfo.LastFailure = now;

                // Implement progressive lockout
                if (existingInfo.FailedAttempts >= _maxAttempts)
                {
                    var lockoutDuration = CalculateLockoutDuration(existingInfo.FailedAttempts);
                    existingInfo.LockoutUntil = now.Add(lockoutDuration);

                    _logger.LogWarning("[AuthTracker] Account locked for {Identifier}. Attempts: {Attempts}, Lockout until: {LockoutUntil}",
                        identifier, existingInfo.FailedAttempts, existingInfo.LockoutUntil);
                }
                else
                {
                    _logger.LogInformation("[AuthTracker] Failed attempt {Attempts}/{MaxAttempts} for {Identifier}",
                        existingInfo.FailedAttempts, _maxAttempts, identifier);
                }
            }
            else
            {
                // Window expired, start fresh
                existingInfo.FailedAttempts = 1;
                existingInfo.FirstFailure = now;
                existingInfo.LastFailure = now;
                existingInfo.LockoutUntil = null;
            }

            _cache.Set(key, existingInfo, TimeSpan.FromHours(1)); // Cache for 1 hour max
        }
        else
        {
            // First failed attempt
            var newInfo = new AttemptInfo
            {
                FailedAttempts = 1,
                FirstFailure = now,
                LastFailure = now,
                LockoutUntil = null
            };

            _cache.Set(key, newInfo, TimeSpan.FromHours(1));
            _logger.LogInformation("[AuthTracker] First failed attempt for {Identifier}", identifier);
        }
    }

    public void RecordSuccessfulAttempt(string identifier)
    {
        var key = $"auth_attempts_{identifier}";
        _cache.Remove(key);
        _logger.LogDebug("[AuthTracker] Successful authentication for {Identifier}, cleared tracking", identifier);
    }

    private static TimeSpan CalculateLockoutDuration(int attemptCount)
    {
        // Progressive lockout: 1min, 5min, 15min, 30min, 1hour
        return attemptCount switch
        {
            5 => TimeSpan.FromMinutes(1),
            6 => TimeSpan.FromMinutes(5),
            7 => TimeSpan.FromMinutes(15),
            8 => TimeSpan.FromMinutes(30),
            _ => TimeSpan.FromHours(1) // 9+ attempts
        };
    }
}
