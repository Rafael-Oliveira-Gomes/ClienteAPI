namespace GumpStudio.Application.Common;

public sealed class NotFoundException(string message) : Exception(message);
public sealed class ForbiddenException(string message) : Exception(message);
public sealed class UnauthorizedException(string message) : Exception(message);
public sealed class ConflictException(string message) : Exception(message);
public sealed class InvalidInputException(string message) : Exception(message);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
