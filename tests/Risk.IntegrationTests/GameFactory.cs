using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Risk.Server.Models;
namespace Risk.IntegrationTests;

public sealed class GameFactory : WebApplicationFactory<Server.Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Testing");

    public HubConnection Connection(Action<RoomSnapshot> receive = null)
    {
        var connection = new HubConnectionBuilder().WithUrl("http://localhost/play", options =>
        {
            options.Transports = HttpTransportType.LongPolling;
            options.HttpMessageHandlerFactory = _ => Server.CreateHandler();
        }).AddNewtonsoftJsonProtocol(options =>
        {
            options.PayloadSerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
            options.PayloadSerializerSettings.Converters.Add(new StringEnumConverter(new CamelCaseNamingStrategy()));
        }).Build();
        connection.On("Snapshot", receive ?? (_ => { }));
        return connection;
    }
}
