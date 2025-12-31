using Microsoft.Extensions.Logging;
using Orleans.Runtime;
using Orleans.Storage;

namespace Orleans.Persistence.Couchbase.Core;

/// <summary>
/// High-performance Couchbase grain storage implementation.
/// Serialization is handled by the data manager's transcoder.
/// </summary>
public sealed class CouchbaseGrainStorage : IGrainStorage
{
    private readonly string _name;
    private readonly ICouchbaseDataManager _dataManager;
    private readonly ILogger<CouchbaseGrainStorage> _logger;

    public CouchbaseGrainStorage(
        string name,
        ICouchbaseDataManager dataManager,
        ILogger<CouchbaseGrainStorage> logger)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _dataManager = dataManager ?? throw new ArgumentNullException(nameof(dataManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ReadStateAsync<T>(string stateName, GrainId grainId, IGrainState<T> grainState)
    {
        try
        {
            var grainType = stateName;
            var grainIdString = grainId.ToString();

            var (state, cas) = await _dataManager.ReadAsync<T>(grainType, grainIdString);

            if (cas != 0)
            {
                grainState.State = state ?? Activator.CreateInstance<T>();
                grainState.ETag = cas.ToString();
                grainState.RecordExists = true;
            }
            else
            {
                grainState.State = Activator.CreateInstance<T>();
                grainState.ETag = null;
                grainState.RecordExists = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading grain state for {GrainType}:{GrainId}", stateName, grainId);
            throw;
        }
    }

    public async Task WriteStateAsync<T>(string stateName, GrainId grainId, IGrainState<T> grainState)
    {
        try
        {
            var grainType = stateName;
            var grainIdString = grainId.ToString();

            var cas = ulong.TryParse(grainState.ETag, out var parsedCas) ? parsedCas : 0;

            var newCas = await _dataManager.WriteAsync(grainType, grainIdString, grainState.State, cas);

            grainState.ETag = newCas.ToString();
            grainState.RecordExists = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing grain state for {GrainType}:{GrainId}", stateName, grainId);
            throw;
        }
    }

    public async Task ClearStateAsync<T>(string stateName, GrainId grainId, IGrainState<T> grainState)
    {
        try
        {
            var grainType = stateName;
            var grainIdString = grainId.ToString();

            var cas = ulong.TryParse(grainState.ETag, out var parsedCas) ? parsedCas : 0;

            await _dataManager.DeleteAsync(grainType, grainIdString, cas);

            grainState.State = Activator.CreateInstance<T>();
            grainState.ETag = null;
            grainState.RecordExists = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing grain state for {GrainType}:{GrainId}", stateName, grainId);
            throw;
        }
    }
}
