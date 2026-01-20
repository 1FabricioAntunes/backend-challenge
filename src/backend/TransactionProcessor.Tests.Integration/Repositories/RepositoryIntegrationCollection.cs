using Xunit;

namespace TransactionProcessor.Tests.Integration.Repositories;

/// <summary>
/// Collection definition for repository integration tests.
/// Tests in this collection share the same test class fixture lifecycle
/// but each test class gets its own PostgreSQL container for isolation.
/// </summary>
[CollectionDefinition("RepositoryIntegration")]
public class RepositoryIntegrationCollection : ICollectionFixture<object>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
