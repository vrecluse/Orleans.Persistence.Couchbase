using FluentAssertions;
using MessagePack;
using Microsoft.Extensions.Logging;
using Orleans.Persistence.Couchbase.Configuration;
using Orleans.Persistence.Couchbase.Core;
using Orleans.Persistence.Couchbase.Infrastructure;
using Xunit;

namespace Orleans.Persistence.Couchbase.IntegrationTests;

/// <summary>
/// Integration tests for CouchbaseDataManager using real Couchbase container.
/// </summary>
[Collection(CouchbaseCollection.Name)]
public class CouchbaseDataManagerTests : IAsyncLifetime
{
    private readonly CouchbaseFixture _fixture;
    private CouchbaseDataManager? _dataManager;

    public CouchbaseDataManagerTests(CouchbaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        var options = new CouchbaseStorageOptions
        {
            ConnectionString = _fixture.ConnectionString,
            BucketName = CouchbaseFixture.BucketName,
            Username = CouchbaseFixture.Username,
            Password = CouchbaseFixture.Password
        };

        var transcoder = new OrleansCouchbaseTranscoder();
        var logger = new LoggerFactory().CreateLogger<CouchbaseDataManager>();

        _dataManager = new CouchbaseDataManager(
            _fixture.Cluster!,
            options,
            transcoder,
            logger);

        await _dataManager.InitializeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_dataManager != null)
        {
            await _dataManager.DisposeAsync();
        }
    }

    [Fact]
    public async Task WriteAndReadAsync_ShouldPersistData()
    {
        // Arrange
        var grainType = "TestGrain";
        var grainId = Guid.NewGuid().ToString();
        var testState = new TestState { Name = "Test", Value = 42 };

        // Act - Write
        var cas = await _dataManager!.WriteAsync(grainType, grainId, testState, 0);

        // Assert - Write
        cas.Should().BeGreaterThan(0);

        // Act - Read
        var (state, readCas) = await _dataManager.ReadAsync<TestState>(grainType, grainId);

        // Assert - Read
        readCas.Should().Be(cas);
        state.Should().NotBeNull();
        state!.Name.Should().Be("Test");
        state.Value.Should().Be(42);
    }

    [Fact]
    public async Task ReadAsync_WhenDocumentNotExists_ShouldReturnEmpty()
    {
        // Arrange
        var grainType = "NonExistentGrain";
        var grainId = Guid.NewGuid().ToString();

        // Act
        var (state, cas) = await _dataManager!.ReadAsync<TestState>(grainType, grainId);

        // Assert
        state.Should().BeNull();
        cas.Should().Be(0);
    }

    [Fact]
    public async Task WriteAsync_WithCas_ShouldUpdateDocument()
    {
        // Arrange
        var grainType = "TestGrain";
        var grainId = Guid.NewGuid().ToString();
        var initialState = new TestState { Name = "Version1", Value = 1 };
        var updatedState = new TestState { Name = "Version2", Value = 2 };

        // Act - Initial write
        var initialCas = await _dataManager!.WriteAsync(grainType, grainId, initialState, 0);

        // Act - Update with CAS
        var updatedCas = await _dataManager.WriteAsync(grainType, grainId, updatedState, initialCas);

        // Assert
        updatedCas.Should().BeGreaterThan(initialCas);

        // Verify the update
        var (state, _) = await _dataManager.ReadAsync<TestState>(grainType, grainId);
        state.Should().NotBeNull();
        state!.Name.Should().Be("Version2");
        state.Value.Should().Be(2);
    }

    [Fact]
    public async Task WriteAsync_WithWrongCas_ShouldThrowInconsistentStateException()
    {
        // Arrange
        var grainType = "TestGrain";
        var grainId = Guid.NewGuid().ToString();
        var initialState = new TestState { Name = "Version1", Value = 1 };
        var updatedState = new TestState { Name = "Version2", Value = 2 };

        // Write initial data
        await _dataManager!.WriteAsync(grainType, grainId, initialState, 0);

        // Act & Assert - Try to update with wrong CAS
        var wrongCas = 12345UL;
        var act = async () => await _dataManager.WriteAsync(grainType, grainId, updatedState, wrongCas);

        await act.Should().ThrowAsync<Orleans.Storage.InconsistentStateException>();
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveDocument()
    {
        // Arrange
        var grainType = "TestGrain";
        var grainId = Guid.NewGuid().ToString();
        var testState = new TestState { Name = "ToDelete", Value = 99 };

        // Write first
        var cas = await _dataManager!.WriteAsync(grainType, grainId, testState, 0);

        // Act - Delete
        await _dataManager.DeleteAsync(grainType, grainId, cas);

        // Assert - Should not exist
        var (state, readCas) = await _dataManager.ReadAsync<TestState>(grainType, grainId);
        state.Should().BeNull();
        readCas.Should().Be(0);
    }

    [Fact]
    public async Task DeleteAsync_WhenDocumentNotExists_ShouldNotThrow()
    {
        // Arrange
        var grainType = "NonExistentGrain";
        var grainId = Guid.NewGuid().ToString();

        // Act & Assert - Should not throw
        var act = async () => await _dataManager!.DeleteAsync(grainType, grainId, 0);

        await act.Should().NotThrowAsync();
    }

    [MessagePackObject]
    private class TestState
    {
        [Key(0)]
        public string Name { get; set; } = string.Empty;

        [Key(1)]
        public int Value { get; set; }
    }
}
