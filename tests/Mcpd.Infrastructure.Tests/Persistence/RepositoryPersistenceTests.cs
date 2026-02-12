using FluentAssertions;
using Mcpd.Domain.Entities;
using Mcpd.Infrastructure.Persistence;
using Mcpd.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Mcpd.Infrastructure.Tests.Persistence;

public sealed class RepositoryPersistenceTests
{
    [Fact]
    public async Task ClientRegistrationRepository_AddAndGetByClientId_RoundTrips()
    {
        await using var db = CreateDbContext();
        var repository = new ClientRegistrationRepository(db);

        var registration = new ClientRegistration(
            clientId: "client-123",
            clientSecretHash: "secret-hash",
            clientName: "test-client",
            tokenEndpointAuthMethod: "client_secret_post",
            grantTypes: ["client_credentials"],
            redirectUris: ["https://example.com/callback"],
            registrationAccessTokenHash: "rat-hash",
            scope: ["read"]);

        await repository.AddAsync(registration, CancellationToken.None);
        var loaded = await repository.GetByClientIdAsync("client-123", CancellationToken.None);

        loaded.Should().NotBeNull();
        loaded!.ClientName.Should().Be("test-client");
        loaded.Scope.Should().ContainSingle().Which.Should().Be("read");
    }

    [Fact]
    public async Task AuditLogRepository_Add_PersistsEntry()
    {
        await using var db = CreateDbContext();
        var repository = new AuditLogRepository(db);

        var entry = new AuditLogEntry("ClientRegistered", "client-123", Guid.NewGuid(), null, "created");

        await repository.AddAsync(entry, CancellationToken.None);

        var persisted = await db.AuditLog.SingleAsync();
        persisted.Action.Should().Be("ClientRegistered");
        persisted.ActorId.Should().Be("client-123");
    }

    private static McpdDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<McpdDbContext>()
            .UseInMemoryDatabase($"mcpd-infra-tests-{Guid.NewGuid()}")
            .Options;

        return new McpdDbContext(options);
    }
}
