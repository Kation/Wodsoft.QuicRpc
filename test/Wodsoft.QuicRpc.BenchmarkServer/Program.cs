using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using System.Security.Authentication;
using Wodsoft.QuicRpc.BenchmarkServer;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(52300, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1AndHttp2AndHttp3;
        listenOptions.UseHttps(CertificateHelper.ServerCertificate, adapterOptions =>
        {
            adapterOptions.SslProtocols = SslProtocols.Tls13;
        });
    });
});
builder.Logging.SetMinimumLevel(LogLevel.Error);
builder.Services.AddGrpc();
builder.Services.AddHostedService<QuicRpcHostedService>();
var app = builder.Build();
app.MapGrpcService<BenchmarkGrpcService>();
app.Run();
