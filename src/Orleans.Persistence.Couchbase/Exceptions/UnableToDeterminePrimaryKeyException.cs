namespace Orleans.Persistence.Couchbase.Exceptions;

/// <summary>
/// 无法确定主键异常
/// </summary>
public class UnableToDeterminePrimaryKeyException : CouchbasePersistenceException
{
    public UnableToDeterminePrimaryKeyException()
    {
    }

    public UnableToDeterminePrimaryKeyException(string message) : base(message)
    {
    }

    public UnableToDeterminePrimaryKeyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
