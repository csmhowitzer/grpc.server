using System.Text.Json;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using GrpcService.Server.Contracts;

namespace GrpcService.Server.Services;

public class WeatherService : Server.WeatherService.WeatherServiceBase
{
    private readonly ILogger<WeatherService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    private static async Task<Temperatures> GetCurrentTemperaturesAsync(
        GetCurrentWeatherForCityRequest request,
        HttpClient httpClient
    )
    {
        var responseText = await httpClient.GetStringAsync(
            $"https://api.openweathermap.org/data/2.5/weather?q={request.City}&APPID=98d06264d6aaf62941254419334e88dd&units={request.Units}"
        );
        return JsonSerializer.Deserialize<Temperatures>(responseText);
    }

    public WeatherService(IHttpClientFactory httpClientFactory, ILogger<WeatherService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public override async Task<WeatherResponse> GetCurrentWeather(
        GetCurrentWeatherForCityRequest request,
        ServerCallContext context
    )
    {
        var httpClient = _httpClientFactory.CreateClient();
        var temperatures = await GetCurrentTemperaturesAsync(request, httpClient);

        return new WeatherResponse
        {
            Temperature = temperatures!.Main.Temp,
            FeelsLike = temperatures.Main.FeelsLike,
            Timestamp = Timestamp.FromDateTime(DateTime.UtcNow),
            City = request.City,
            Units = request.Units,
        };
    }

    public override async Task GetCurrentWeatherStream(
        GetCurrentWeatherForCityRequest request,
        IServerStreamWriter<WeatherResponse> responseStream,
        ServerCallContext context
    )
    {
        var httpClient = _httpClientFactory.CreateClient();
        for (int i = 0; i < 30; i++)
        {
            if (context.CancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Request was cancelled");
                break;
            }
            var temperatures = await GetCurrentTemperaturesAsync(request, httpClient);
            await responseStream.WriteAsync(
                new WeatherResponse
                {
                    Temperature = temperatures!.Main.Temp,
                    FeelsLike = temperatures.Main.FeelsLike,
                    Timestamp = Timestamp.FromDateTime(DateTime.UtcNow),
                    City = request.City,
                    Units = request.Units,
                }
            );
            await Task.Delay(1000);
        }
    }

    public override async Task<MultiWeatherResponse> GetMultiCurrentWeatherStream(
        IAsyncStreamReader<GetCurrentWeatherForCityRequest> requestStream,
        ServerCallContext context
    )
    {
        var httpClient = _httpClientFactory.CreateClient();
        var response = new MultiWeatherResponse { Weather = { } };

        await foreach (var request in requestStream.ReadAllAsync())
        {
            var temperatures = await GetCurrentTemperaturesAsync(request, httpClient);
            response.Weather.Add(
                new WeatherResponse
                {
                    Temperature = temperatures!.Main.Temp,
                    FeelsLike = temperatures.Main.FeelsLike,
                    Timestamp = Timestamp.FromDateTime(DateTime.UtcNow),
                    City = request.City,
                    Units = request.Units,
                }
            );
        }
        return response;
    }

    public override async Task<Empty> PrintStream(
        IAsyncStreamReader<PrintRequest> requestStream,
        ServerCallContext context
    )
    {
        await foreach (var request in requestStream.ReadAllAsync())
        {
            _logger.LogInformation($"Client said: {request.Message}");
        }
        return new();
    }
}
