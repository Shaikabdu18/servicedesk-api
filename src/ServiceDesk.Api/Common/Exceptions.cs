namespace ServiceDesk.Api.Common;

/// <summary>Base type for expected application errors that map to HTTP problem responses.</summary>
public abstract class AppException(string message) : Exception(message);

/// <summary>A business rule was violated (maps to 400).</summary>
public sealed class DomainRuleException(string message) : AppException(message);

/// <summary>The requested resource does not exist or is not visible to the caller (maps to 404).</summary>
public sealed class NotFoundException(string resource, object key)
    : AppException($"{resource} '{key}' was not found.");

/// <summary>The caller is authenticated but not allowed to perform the action (maps to 403).</summary>
public sealed class ForbiddenException(string message = "You do not have access to this resource.")
    : AppException(message);

/// <summary>The request conflicts with existing state (maps to 409).</summary>
public sealed class ConflictException(string message) : AppException(message);

/// <summary>Login failed (maps to 401). The message is intentionally generic.</summary>
public sealed class AuthenticationFailedException() : AppException("Invalid email or password.");
