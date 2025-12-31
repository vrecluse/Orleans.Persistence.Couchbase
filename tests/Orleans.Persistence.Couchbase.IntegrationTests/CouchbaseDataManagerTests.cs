using FluentAssertions;
using Microsoft.Extensions.Logging;
using Orleans.Persistence.Couchbase.Configuration;
using Orleans.Persistence.Couchbase.Core;
using Orleans.Persistence.Couchbase.Serialization;
using System;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Orleans.Persistence.Couchbase.IntegrationTests;

/// <summary>
/// Integration tests for CouchbaseDataManager using real Couchbase container
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

        var serializer = new JsonGrainStateSerializer();
        var logger = new LoggerFactory().CreateLogger<CouchbaseDataManager>();

        _dataManager = new CouchbaseDataManager(
            _fixture.Cluster!,
            options,
            serializer,
            logger);

        await _dataManager.InitializeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        // Don't dispose the cluster - it's managed by the fixture
        await ValueTask.CompletedTask;
    }

    [Fact]
    public async Task WriteAndReadAsync_ShouldPersistData()
    {
        // Arrange
        var grainType = "TestGrain";
        var grainId = Guid.NewGuid().ToString();
        var testData = Encoding.UTF8.GetBytes("{\"Name\":\"Test\",\"Value\":42}");

        // Act - Write
        var cas = await _dataManager!.WriteAsync(grainType, grainId, testData, 0);

        // Assert - Write
        cas.Should().BeGreaterThan(0);

        // Act - Read
        var (data, readCas) = await _dataManager.ReadAsync(grainType, grainId);

        // Assert - Read
        readCas.Should().Be(cas);
        data.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ReadAsync_WhenDocumentNotExists_ShouldReturnEmpty()
    {
        // Arrange
        var grainType = "NonExistentGrain";
        var grainId = Guid.NewGuid().ToString();

        // Act
        var (data, cas) = await _dataManager!.ReadAsync(grainType, grainId);

        // Assert
        data.Length.Should().Be(0);
        cas.Should().Be(0);
    }

    [Fact]
    public async Task WriteAsync_WithCas_ShouldUpdateDocument()
    {
        // Arrange
        var grainType = "TestGrain";
        var grainId = Guid.NewGuid().ToString();
        var initialData = Encoding.UTF8.GetBytes("{\"Version\":1}");
        var updatedData = Encoding.UTF8.GetBytes("{\"Version\":2}");

        // Act - Initial write
        var initialCas = await _dataManager!.WriteAsync(grainType, grainId, initialData, 0);

        // Act - Update with CAS
        var updatedCas = await _dataManager.WriteAsync(grainType, grainId, updatedData, initialCas);

        // Assert
        updatedCas.Should().BeGreaterThan(initialCas);

        // Verify the update
        var (data, _) = await _dataManager.ReadAsync(grainType, grainId);
        data.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task WriteAsync_WithWrongCas_ShouldThrowInconsistentStateException()
    {
        // Arrange
        var grainType = "TestGrain";
        var grainId = Guid.NewGuid().ToString();
        var initialData = Encoding.UTF8.GetBytes("{\"Version\":1}");
        var updatedData = Encoding.UTF8.GetBytes("{\"Version\":2}");

        // Write initial data
        await _dataManager!.WriteAsync(grainType, grainId, initialData, 0);

        // Act & Assert - Try to update with wrong CAS
        var wrongCas = 12345UL;
        var act = async () => await _dataManager.WriteAsync(grainType, grainId, updatedData, wrongCas);

        await act.Should().ThrowAsync<Orleans.Storage.InconsistentStateException>();
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveDocument()
    {
        // Arrange
        var grainType = "TestGrain";
        var grainId = Guid.NewGuid().ToString();
        var testData = Encoding.UTF8.GetBytes("{\"ToDelete\":true}");

        // Write first
        var cas = await _dataManager!.WriteAsync(grainType, grainId, testData, 0);

        // Act - Delete
        await _dataManager.DeleteAsync(grainType, grainId, cas);

        // Assert - Should not exist
        var (data, readCas) = await _dataManager.ReadAsync(grainType, grainId);
        data.Length.Should().Be(0);
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
}
