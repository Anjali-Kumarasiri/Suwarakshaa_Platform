using System.Linq.Expressions;
namespace Application.Abstractions;

// Repository pattern
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(long id, CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    IQueryable<T> Query();
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task ExecuteInTransactionAsync(Func<Task> work, CancellationToken ct = default);
}

public record AiResult(string Category, int Urgency, decimal Confidence, string Rationale, bool IsFallback);

// External AI (Polly + fallback live in the Infrastructure implementation)
public interface IAiClient
{
    Task<AiResult> ClassifyAsync(string description, CancellationToken ct = default);
}

public record GeoPoint(decimal Latitude, decimal Longitude, string? FormattedAddress);

/// Map/geocoding integration (OpenStreetMap Nominatim). Returns null if unavailable: callers must cope.
public interface IGeocodingService
{
    Task<GeoPoint?> GeocodeAsync(string address, CancellationToken ct = default);
    Task<GeoPoint?> ReverseAsync(decimal latitude, decimal longitude, CancellationToken ct = default);
}

/// Notification channel sender (Factory pattern picks the implementation per channel).
public interface INotificationSender
{
    string Provider { get; }
    Task SendAsync(string to, string subject, string body, CancellationToken ct = default);
}
public interface INotificationSenderFactory
{
    INotificationSender Create(string channel);
}

public record RouteInfo(double DistanceKm, double DurationMinutes, bool Estimated);

/// Distance / route calculation through an external routing service (OSRM). Falls back to an estimate if it is down.
public interface IRoutingService
{
    Task<RouteInfo> RouteAsync(double fromLat, double fromLon, double toLat, double toLon, CancellationToken ct = default);
}

public record AiText(string Text, bool IsFallback);

/// Citizen assistant, coordinator summaries and translation (all with graceful fallback).
public interface IAiAssistantClient
{
    Task<AiText> AskAsync(string message, string language, CancellationToken ct = default);
    Task<AiText> SummarizeAsync(string statsJson, string language, CancellationToken ct = default);
    Task<string?> TranslateAsync(string text, string targetLanguage, CancellationToken ct = default);   // null when unavailable
}
