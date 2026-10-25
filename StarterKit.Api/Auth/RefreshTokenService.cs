using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StarterKit.Api.Data;
using StarterKit.Api.Domain;

namespace StarterKit.Api.Auth;

public sealed record IssuedRefreshToken(string UserId, string Token, DateTime ExpiresAt);

public sealed class RefreshTokenService(AppDbContext db, IOptions<JwtOptions> options, TimeProvider time, ILogger<RefreshTokenService> logger)
{
    private readonly JwtOptions _jwt = options.Value;

    /// <summary>Issues the first token of a new family (called on login).</summary>
    public async Task<IssuedRefreshToken> CreateAsync(string userId, CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var (plain, entity) = NewToken(userId, Guid.NewGuid(), now);

        db.RefreshTokens.Add(entity);
        await db.SaveChangesAsync(ct);

        return new IssuedRefreshToken(userId, plain, entity.ExpiresAt);
    }

    /// <summary>
    /// Consumes a refresh token and returns its replacement, or null if it is not valid.
    /// Presenting an already revoked token revokes its whole family (reuse detection).
    /// </summary>
    public async Task<IssuedRefreshToken?> RotateAsync(string token, CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var hash = Hash(token);

        var stored = await db.RefreshTokens.AsNoTracking()
            .SingleOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (stored is null)
            return null;

        if (stored.RevokedAt is not null)
        {
            var elapsed = now - stored.RevokedAt.Value;
            var withinLeeway = stored.ReplacedByTokenId is not null
                && elapsed < TimeSpan.FromSeconds(_jwt.RefreshTokenReuseLeewaySeconds);

            if (withinLeeway)
            {
                logger.LogInformation(
                    "Rotated refresh token reused after {ElapsedSeconds:F1}s, within the {LeewaySeconds}s leeway. Family {FamilyId} kept.",
                    elapsed.TotalSeconds, _jwt.RefreshTokenReuseLeewaySeconds, stored.FamilyId);
                return null;
            }

            logger.LogWarning(
                "Refresh token reuse detected after {ElapsedSeconds:F1}s. Revoking family {FamilyId}.",
                elapsed.TotalSeconds, stored.FamilyId);
            await RevokeFamilyAsync(stored.FamilyId, now, ct);
            return null;
        }

        if (stored.ExpiresAt <= now)
            return null;

        var (plain, replacement) = NewToken(stored.UserId, stored.FamilyId, now);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Conditional update: only one concurrent request can claim the old token.
        var claimed = await db.RefreshTokens
            .Where(t => t.Id == stored.Id && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.RevokedAt, now)
                .SetProperty(t => t.ReplacedByTokenId, replacement.Id), ct);

        if (claimed == 0)
            return null;

        db.RefreshTokens.Add(replacement);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new IssuedRefreshToken(stored.UserId, plain, replacement.ExpiresAt);
    }

    /// <summary>Revokes the family the token belongs to (logout). Unknown tokens are ignored.</summary>
    public async Task RevokeAsync(string token, CancellationToken ct)
    {
        var hash = Hash(token);
        var familyId = await db.RefreshTokens
            .Where(t => t.TokenHash == hash)
            .Select(t => (Guid?)t.FamilyId)
            .SingleOrDefaultAsync(ct);

        if (familyId is not null)
            await RevokeFamilyAsync(familyId.Value, time.GetUtcNow().UtcDateTime, ct);
    }

    /// <summary>Revokes every active token of the user (logout from all devices).</summary>
    public Task RevokeAllForUserAsync(string userId, CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        return db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
    }

    private Task RevokeFamilyAsync(Guid familyId, DateTime now, CancellationToken ct) =>
        db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);

    private (string Plain, RefreshToken Entity) NewToken(string userId, Guid familyId, DateTime now)
    {
        var plain = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(64));
        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = Hash(plain),
            FamilyId = familyId,
            CreatedAt = now,
            ExpiresAt = now.AddDays(_jwt.RefreshTokenDays)
        };
        return (plain, entity);
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}