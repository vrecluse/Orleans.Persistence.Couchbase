namespace Orleans.Persistence.Couchbase.Exceptions;

/// <summary>
/// Couchbase 持久化异常基类
/// </summary>
public class CouchbasePersistenceException : Exception
{
    public CouchbasePersistenceException() { }

    public CouchbasePersistenceException(string message) : base(message) { }

    public CouchbasePersistenceException(string message, Exception innerException)
        : base(message, innerException) { }
}
