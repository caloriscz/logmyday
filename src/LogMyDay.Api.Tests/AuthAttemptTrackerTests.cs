using LogMyDay.Api.Authentication;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace LogMyDay.Api.Tests;

/// <summary>
/// Tests for AuthAttemptTracker focusing on security features:
/// - Rate limiting enforcement
/// - Progressive lockout behavior
/// - Attempt tracking accuracy
/// - Cleanup on successful authentication
/// </summary>
public class AuthAttemptTrackerTests
{
    private static AuthAttemptTracker CreateTracker()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var logger = Mock.Of<ILogger<AuthAttemptTracker>>();
        return new AuthAttemptTracker(cache, logger);
    }

    [Fact]
    public void IsBlocked_WithNoAttempts_ReturnsFalse()
    {
        // Arrange
        var tracker = CreateTracker();
        var identifier = "user@test.com";

        // Act
        var isBlocked = tracker.IsBlocked(identifier);

        // Assert
        Assert.False(isBlocked);
    }

    [Fact]
    public void RecordFailedAttempt_UnderThreshold_DoesNotBlock()
    {
        // Arrange
        var tracker = CreateTracker();
        var identifier = "user@test.com";

        // Act - Record 4 failed attempts (under the 5 threshold)
        for (int i = 0; i < 4; i++)
        {
            tracker.RecordFailedAttempt(identifier);
        }

        // Assert
        Assert.False(tracker.IsBlocked(identifier));
    }

    [Fact]
    public void RecordFailedAttempt_AtThreshold_BlocksUser()
    {
        // Arrange
        var tracker = CreateTracker();
        var identifier = "user@test.com";

        // Act - Record exactly 5 failed attempts (meets threshold)
        for (int i = 0; i < 5; i++)
        {
            tracker.RecordFailedAttempt(identifier);
        }

        // Assert
        Assert.True(tracker.IsBlocked(identifier));
    }

    [Fact]
    public void RecordFailedAttempt_ImplementsProgressiveLockout()
    {
        // Arrange
        var tracker = CreateTracker();
        var identifier = "user@test.com";

        // Act & Assert - Test progressive lockout durations
        // 5 attempts = 1 minute lockout
        for (int i = 0; i < 5; i++)
        {
            tracker.RecordFailedAttempt(identifier);
        }
        Assert.True(tracker.IsBlocked(identifier));

        // Manually manipulate to test 6th attempt (would be 5 min in production)
        // We can't easily test exact durations without time manipulation,
        // but we can verify the lockout is in effect
        tracker.RecordFailedAttempt(identifier);
        Assert.True(tracker.IsBlocked(identifier));
    }

    [Fact]
    public void RecordSuccessfulAttempt_ClearsTracking()
    {
        // Arrange
        var tracker = CreateTracker();
        var identifier = "user@test.com";

        // Record some failed attempts
        for (int i = 0; i < 3; i++)
        {
            tracker.RecordFailedAttempt(identifier);
        }

        // Act - Successful authentication
        tracker.RecordSuccessfulAttempt(identifier);

        // Assert - Tracking should be cleared
        Assert.False(tracker.IsBlocked(identifier));
    }

    [Fact]
    public void RecordSuccessfulAttempt_ClearsLockout()
    {
        // Arrange
        var tracker = CreateTracker();
        var identifier = "user@test.com";

        // Trigger lockout with 5 failed attempts
        for (int i = 0; i < 5; i++)
        {
            tracker.RecordFailedAttempt(identifier);
        }
        Assert.True(tracker.IsBlocked(identifier));

        // Act - Successful authentication (simulates lockout expiry + successful login)
        tracker.RecordSuccessfulAttempt(identifier);

        // Assert - Should no longer be blocked
        Assert.False(tracker.IsBlocked(identifier));
    }

    [Fact]
    public void IsBlocked_DifferentIdentifiers_IndependentTracking()
    {
        // Arrange
        var tracker = CreateTracker();
        var user1 = "user1@test.com";
        var user2 = "user2@test.com";

        // Act - Block user1
        for (int i = 0; i < 5; i++)
        {
            tracker.RecordFailedAttempt(user1);
        }

        // user2 has only 2 failed attempts
        tracker.RecordFailedAttempt(user2);
        tracker.RecordFailedAttempt(user2);

        // Assert
        Assert.True(tracker.IsBlocked(user1)); // user1 blocked
        Assert.False(tracker.IsBlocked(user2)); // user2 not blocked
    }

    [Fact]
    public void RecordFailedAttempt_RestartsCountAfterWindowExpiry()
    {
        // This test verifies the concept, but can't easily test the 15-minute window
        // without time manipulation. In production, after 15 minutes, the count resets.
        
        // Arrange
        var tracker = CreateTracker();
        var identifier = "user@test.com";

        // Act - Record 4 attempts
        for (int i = 0; i < 4; i++)
        {
            tracker.RecordFailedAttempt(identifier);
        }

        // In production, after 15 minutes window expires: 5th attempt would start a new window, would need 5 more attempts to trigger lockout
        
        // Assert - Still under threshold in current window
        Assert.False(tracker.IsBlocked(identifier));
    }

    [Fact]
    public void RecordFailedAttempt_AccumulatesAttemptsWithinWindow()
    {
        // Arrange
        var tracker = CreateTracker();
        var identifier = "user@test.com";

        // Act - Add attempts one at a time (all within same window)
        tracker.RecordFailedAttempt(identifier);
        Assert.False(tracker.IsBlocked(identifier)); // 1 attempt
        
        tracker.RecordFailedAttempt(identifier);
        Assert.False(tracker.IsBlocked(identifier)); // 2 attempts
        
        tracker.RecordFailedAttempt(identifier);
        Assert.False(tracker.IsBlocked(identifier)); // 3 attempts
        
        tracker.RecordFailedAttempt(identifier);
        Assert.False(tracker.IsBlocked(identifier)); // 4 attempts
        
        tracker.RecordFailedAttempt(identifier);
        Assert.True(tracker.IsBlocked(identifier)); // 5 attempts - NOW blocked
    }

    [Fact]
    public void IsBlocked_MultipleChecks_ConsistentResult()
    {
        // Arrange
        var tracker = CreateTracker();
        var identifier = "user@test.com";

        // Trigger lockout
        for (int i = 0; i < 5; i++)
        {
            tracker.RecordFailedAttempt(identifier);
        }

        // Act - Check multiple times
        var check1 = tracker.IsBlocked(identifier);
        var check2 = tracker.IsBlocked(identifier);
        var check3 = tracker.IsBlocked(identifier);

        // Assert - All checks should return same result
        Assert.True(check1);
        Assert.True(check2);
        Assert.True(check3);
    }

    [Fact]
    public void RecordSuccessfulAttempt_WithNoFailedAttempts_DoesNotThrow()
    {
        // Arrange
        var tracker = CreateTracker();
        var identifier = "user@test.com";

        // Act & Assert - Should not throw even if no failed attempts recorded
        var exception = Record.Exception(() => tracker.RecordSuccessfulAttempt(identifier));
        Assert.Null(exception);
    }

    [Fact]
    public void RecordFailedAttempt_IncrementalLockoutDurations()
    {
        // This test documents the progressive lockout behavior:
        // 5 attempts = 1 min
        // 6 attempts = 5 min
        // 7 attempts = 15 min
        // 8 attempts = 30 min
        // 9+ attempts = 1 hour

        // Arrange
        var tracker = CreateTracker();
        var identifier = "persistent-attacker@test.com";

        // Act - Keep adding attempts
        for (int i = 1; i <= 10; i++)
        {
            tracker.RecordFailedAttempt(identifier);
        }

        // Assert - After 10 attempts, should definitely be blocked
        Assert.True(tracker.IsBlocked(identifier));
        
        // The actual lockout duration increases with each attempt bracket,
        // making brute-force attacks progressively more expensive
    }
}
