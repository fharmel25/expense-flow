namespace ExpenseFlow.Application.Common;

/// <summary>Mapped to HTTP 404.</summary>
public class NotFoundException : Exception
{
    public NotFoundException(string entity, object key) : base($"{entity} '{key}' was not found.") { }
}

/// <summary>Mapped to HTTP 401.</summary>
public class UnauthorizedException : Exception
{
    public UnauthorizedException(string message) : base(message) { }
}

/// <summary>Mapped to HTTP 403.</summary>
public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }
}

/// <summary>Mapped to HTTP 409.</summary>
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}
