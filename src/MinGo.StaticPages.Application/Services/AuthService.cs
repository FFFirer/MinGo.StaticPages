using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using MinGo.StaticPages.Application.Abstractions;
using MinGo.StaticPages.Application.Dtos;
using MinGo.StaticPages.Domain.Entities;

namespace MinGo.StaticPages.Application.Services;

public sealed class AuthService(IAppDbContext db, IPasswordHasher hasher)
{
    public async Task<User?> ValidateCredentialsAsync(string username, string password, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username, ct);
        if (user is null)
            return null;
        return hasher.Verify(password, user.PasswordHash) ? user : null;
    }

    public async Task<ApiKeyCreatedDto> CreateApiKeyAsync(CreateApiKeyRequest request, string actor, CancellationToken ct = default)
    {
        if (!RoleNames.All.Contains(request.Role))
            throw new InvalidOperationException("Invalid role.");

        var plaintext = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var prefix = plaintext[..8];
        var hash = HashKey(plaintext);

        var key = new ApiKey
        {
            Name = request.Name.Trim(),
            KeyPrefix = prefix,
            KeyHash = hash,
            Role = request.Role
        };
        db.Add(key);
        await db.SaveChangesAsync(ct);

        return new ApiKeyCreatedDto(key.Id, key.Name, key.Role, prefix, plaintext, key.CreatedAt);
    }

    public async Task<IReadOnlyList<ApiKeyDto>> ListApiKeysAsync(CancellationToken ct = default)
    {
        var keys = await db.ApiKeys.ToListAsync(ct);
        return keys.OrderByDescending(k => k.CreatedAt).Select(k => new ApiKeyDto(k.Id, k.Name, k.Role, k.KeyPrefix, k.CreatedAt, k.RevokedAt)).ToList();
    }

    public async Task RevokeApiKeyAsync(Guid id, CancellationToken ct = default)
    {
        var key = await db.ApiKeys.FirstOrDefaultAsync(k => k.Id == id, ct)
            ?? throw new InvalidOperationException("API key not found.");
        key.RevokedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<(bool Success, string Role)> ValidateApiKeyAsync(string plaintext, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(plaintext))
            return (false, string.Empty);

        var hash = HashKey(plaintext);
        var key = await db.ApiKeys.FirstOrDefaultAsync(k => k.KeyHash == hash && k.RevokedAt == null, ct);
        return key is null ? (false, string.Empty) : (true, key.Role);
    }

    public static string HashKey(string plaintext)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(plaintext));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

public sealed class PasswordHasher : IPasswordHasher
{
    public string Hash(string password)
    {
        return BCryptLikeHash(password);
    }

    public bool Verify(string password, string hash)
    {
        return hash == BCryptLikeHash(password);
    }

    // PBKDF2 - no extra package dependency for MVP
    private static string BCryptLikeHash(string password)
    {
        var salt = "mingo.static.v1"u8.ToArray();
        var bytes = Rfc2898DeriveBytes.Pbkdf2(password, salt, 50_000, HashAlgorithmName.SHA256, 32);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
