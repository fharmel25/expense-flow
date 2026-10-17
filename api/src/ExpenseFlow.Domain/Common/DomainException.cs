namespace ExpenseFlow.Domain.Common;

/// <summary>A business rule was broken (mapped to HTTP 422 by the API).</summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}

/// <summary>The action is not allowed in the current status (mapped to HTTP 409).</summary>
public class InvalidStatusTransitionException : DomainException
{
    public InvalidStatusTransitionException(string message) : base(message) { }
}

public static class UtcGuard
{
    public static void Ensure(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Dates must be in UTC.", nameof(value));
    }
}
