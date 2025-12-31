namespace Orleans.Persistence.Couchbase.Exceptions;

/// <summary>
/// 缺少 Bucket 配置异常
/// </summary>
public class BucketConfigMissingFromConfigurationException : CouchbasePersistenceException
{
    public BucketConfigMissingFromConfigurationException()
    {
    }

    public BucketConfigMissingFromConfigurationException(string message) : base(message)
    {
    }

    public BucketConfigMissingFromConfigurationException(string message, Exception innerException) 
        : base(message, innerException)
    {
    }
}
