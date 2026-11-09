using AfterTab.DAL.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterTab.DAL.EF.Configurations;

public sealed class McpAccessTokenConfiguration : IEntityTypeConfiguration<McpAccessToken>
{
    public void Configure(EntityTypeBuilder<McpAccessToken> b)
    {
        b.ConfigureAudit("McpAccessTokens");
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => x.UserId);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class McpOperationConfiguration : IEntityTypeConfiguration<McpOperation>
{
    public void Configure(EntityTypeBuilder<McpOperation> b)
    {
        b.ConfigureAudit("McpOperations");
        b.Property(x => x.Tool).HasMaxLength(64).IsRequired();
        b.Property(x => x.Fingerprint).HasMaxLength(64).IsRequired();
        b.HasIndex(x => new { x.UserId, x.OperationId }).IsUnique();
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class McpOAuthClientConfiguration : IEntityTypeConfiguration<McpOAuthClient>
{
    public void Configure(EntityTypeBuilder<McpOAuthClient> b)
    {
        b.ToTable("McpOAuthClients");
        b.HasKey(x => x.Id);
        b.Property(x => x.ClientId).HasMaxLength(80).IsRequired();
        b.HasIndex(x => x.ClientId).IsUnique();
        b.Property(x => x.ClientName).HasMaxLength(100).IsRequired();
        b.Property(x => x.RedirectUrisJson).HasMaxLength(8000).IsRequired();
        b.Property(x => x.CreatedAtUtc).IsRequired();
    }
}

public sealed class McpOAuthRequestConfiguration : IEntityTypeConfiguration<McpOAuthRequest>
{
    public void Configure(EntityTypeBuilder<McpOAuthRequest> b)
    {
        b.ToTable("McpOAuthRequests");
        b.HasKey(x => x.Id);
        b.Property(x => x.PublicId).IsRequired();
        b.HasIndex(x => x.PublicId).IsUnique();
        b.Property(x => x.ClientId).HasMaxLength(80).IsRequired();
        b.Property(x => x.RedirectUri).HasMaxLength(1000).IsRequired();
        b.Property(x => x.State).HasMaxLength(512).IsRequired();
        b.Property(x => x.CodeChallenge).HasMaxLength(128).IsRequired();
        b.Property(x => x.Scope).HasMaxLength(200).IsRequired();
        b.Property(x => x.Resource).HasMaxLength(500).IsRequired();
        b.HasIndex(x => x.ExpiresAtUtc);
    }
}

public sealed class McpOAuthAuthorizationCodeConfiguration : IEntityTypeConfiguration<McpOAuthAuthorizationCode>
{
    public void Configure(EntityTypeBuilder<McpOAuthAuthorizationCode> b)
    {
        b.ToTable("McpOAuthAuthorizationCodes");
        b.HasKey(x => x.Id);
        b.Property(x => x.CodeHash).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.CodeHash).IsUnique();
        b.Property(x => x.ClientId).HasMaxLength(80).IsRequired();
        b.Property(x => x.RedirectUri).HasMaxLength(1000).IsRequired();
        b.Property(x => x.CodeChallenge).HasMaxLength(128).IsRequired();
        b.Property(x => x.Scope).HasMaxLength(200).IsRequired();
        b.Property(x => x.Resource).HasMaxLength(500).IsRequired();
        b.HasIndex(x => x.ExpiresAtUtc);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class McpOAuthTokenConfiguration : IEntityTypeConfiguration<McpOAuthToken>
{
    public void Configure(EntityTypeBuilder<McpOAuthToken> b)
    {
        b.ToTable("McpOAuthTokens");
        b.HasKey(x => x.Id);
        b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.Property(x => x.TokenType).HasMaxLength(16).IsRequired();
        b.Property(x => x.ClientId).HasMaxLength(80).IsRequired();
        b.Property(x => x.Scope).HasMaxLength(200).IsRequired();
        b.Property(x => x.Resource).HasMaxLength(500).IsRequired();
        b.HasIndex(x => new { x.FamilyId, x.TokenType });
        b.HasIndex(x => x.ExpiresAtUtc);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
