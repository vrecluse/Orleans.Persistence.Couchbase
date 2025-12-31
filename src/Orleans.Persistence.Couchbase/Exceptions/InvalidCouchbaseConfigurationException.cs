namespace Orleans.Persistence.Couchbase.Exceptions;

/// <summary>
/// 无效的 Couchbase 配置异常
/// </summary>
public class InvalidCouchbaseConfigurationException : CouchbasePersistenceException
{
    public InvalidCouchbaseConfigurationException()
    {
    }

    public InvalidCouchbaseConfigurationException(string message) : base(message)
    {
    }

    public InvalidCouchbaseConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
