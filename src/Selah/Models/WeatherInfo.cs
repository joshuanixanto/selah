namespace Selah.Models;

public sealed record WeatherInfo(
    string City,
    double TemperatureCelsius,
    double FeelsLikeCelsius,
    string Condition,
    int HumidityPercent,
    IReadOnlyList<HourlyWeatherInfo> HourlyForecast);

public sealed record HourlyWeatherInfo(
    DateTime Time,
    string Condition,
    int PrecipitationChancePercent);
