namespace RestaurantOS.Api.Models.Dto;

public sealed record SystemInfoResponse(
    string Service,
    string Status,
    string Version,
    DateTimeOffset TimestampUtc);
