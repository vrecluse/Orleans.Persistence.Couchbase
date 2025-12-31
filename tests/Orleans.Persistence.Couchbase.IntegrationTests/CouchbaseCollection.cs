using Xunit;

namespace Orleans.Persistence.Couchbase.IntegrationTests;

/// <summary>
/// Collection definition for Couchbase integration tests.
/// All tests in this collection share the same Couchbase container.
/// </summary>
[CollectionDefinition(Name)]
public class CouchbaseCollection : ICollectionFixture<CouchbaseFixture>
{
    public const string Name = "Couchbase";
}
