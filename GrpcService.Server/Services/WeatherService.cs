using System.Text.Json;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using GrpcService.Server.Contracts;

namespace GrpcService.Server.Services;

public class WeatherService : Server.WeatherService.WeatherServiceBase
{
    private readonly ILogger<WeatherService> _logger;

    //
    // public WeatherService(ILogger<WeatherService> logger)
    // {
    //     _logger = logger;
    // }

    //     public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
    //     {
    //         return Task.FromResult(new HelloReply { Message = "Server: Hello " + request.Name });
    //     }

    private readonly IHttpClientFactory _httpClientFactory;

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
                }
            );
            await Task.Delay(1000);
        }
    }

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
}
