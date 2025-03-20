using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace GrpcService.Server.Services;

public class ChatService : Chat.ChatBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ChatService> _logger;

    public ChatService(IHttpClientFactory httpClientFactory, ILogger<ChatService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public override async Task SendMessage(
        IAsyncStreamReader<ClientToServerMessage> requestStream,
        IServerStreamWriter<ServerToClientMessage> responseStream,
        ServerCallContext context
    )
    {
        var clientToServerTask = ClientToServerPingHandlingAsync(requestStream, context);

        var serverToClientTask = ServertoClientPingHandlingAsync(responseStream, context);

        await Task.WhenAll(clientToServerTask, serverToClientTask);
    }

    public async Task ClientToServerPingHandlingAsync(
        IAsyncStreamReader<ClientToServerMessage> requestStream,
        ServerCallContext context
    )
    {
        while (await requestStream.MoveNext() && !context.CancellationToken.IsCancellationRequested)
        {
            var message = requestStream.Current;
            _logger.LogInformation("Client said: {Message}", message.Message);
        }
    }

    public async Task ServertoClientPingHandlingAsync(
        IAsyncStreamWriter<ServerToClientMessage> responseStream,
        ServerCallContext context
    )
    {
        var pingCount = 0;
        while (!context.CancellationToken.IsCancellationRequested)
        {
            await responseStream.WriteAsync(
                new ServerToClientMessage
                {
                    Message = $"Server said high {++pingCount} times",
                    Timestamp = Timestamp.FromDateTime(DateTime.UtcNow),
                }
            );
            await Task.Delay(1000);
        }
    }
}
