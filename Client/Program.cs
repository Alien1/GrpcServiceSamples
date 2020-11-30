using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using TokenRefreshGrpcService;

namespace Client
{
    static class Program
    {
        private const string BaseAddress = "https://localhost:5007";
        private static string name;
        static async Task Main(string[] args)
        {
            IHost host = CreateHostBuilder(args).Build();

            Console.WriteLine("Your name?");
            name = Console.ReadLine();

            var cts = new CancellationTokenSource();
            var cancellationToken = cts.Token;

            var retryPolicy = Policy
                .Handle<RpcException>(x => x.StatusCode == StatusCode.Cancelled)
                .RetryAsync(
                    retryCount: 1,
                    onRetryAsync: async (outcome, retryNumber, context) =>
                    {
                        await host.Services.GetService<AuthHttpClient>()!.Authenticate("Foo", cancellationToken);
                    });

            var exiting = false;
            while (!exiting)
            {
                await GreetSample(host.Services, cancellationToken);
                await GreetStreamSample(host.Services, cancellationToken);
                await retryPolicy.ExecuteAsync(async () => await GreetClientStreamSample(host.Services, cancellationToken));
                await retryPolicy.ExecuteAsync(async () => await GreetDuplexSample(host.Services, cancellationToken));
                Console.WriteLine("Retry? 1 - Yes, 2 - No.");
                var consoleKeyInfo = Console.ReadKey(intercept: true);
                switch (consoleKeyInfo.KeyChar)
                {
                    case '1':
                        break;
                    case '2':
                        exiting = true;
                        break;
                }
            }

            await host.RunAsync();
        }

        static async Task GreetSample(IServiceProvider services, CancellationToken cancellationToken)
        {
            using IServiceScope serviceScope = services.CreateScope();
            IServiceProvider provider = serviceScope.ServiceProvider;

            var client = provider.GetService<Greeter.GreeterClient>();
            var reply = await client!.SayHelloAsync(new HelloRequest { Name = name }, cancellationToken: cancellationToken);
            Console.WriteLine(reply.Message);
        }
        static async Task GreetStreamSample(IServiceProvider services, CancellationToken cancellationToken)
        {
            using IServiceScope serviceScope = services.CreateScope();
            IServiceProvider provider = serviceScope.ServiceProvider;

            var client = provider.GetService<Greeter.GreeterClient>();

            var call = client!.SayHelloStream(new HelloRequest { Name = name }, cancellationToken: cancellationToken);
            await foreach (var reply in call.ResponseStream.ReadAllAsync(cancellationToken))
            {
                Console.WriteLine(reply.Message);
            }
        }

        static async Task GreetClientStreamSample(IServiceProvider services, CancellationToken cancellationToken)
        {
            using IServiceScope serviceScope = services.CreateScope();
            IServiceProvider provider = serviceScope.ServiceProvider;

            var client = provider.GetService<Greeter.GreeterClient>();
            
            var call = client!.SayHelloClienStream(cancellationToken: cancellationToken);
            foreach (var symbol in Enumerable.Range(0, 100)
                                             .Select(x => "Hello {0}\n"))
            {
                await call.RequestStream.WriteAsync(new HelloRequest
                {
                    Name = $"{symbol}"
                });
                await Task.Delay(100);
            }

            await call.RequestStream.CompleteAsync();
            var res = await call;
            Console.WriteLine(res.Message);
        }

        static async Task GreetDuplexSample(IServiceProvider services, CancellationToken cancellationToken)
        {
            using IServiceScope serviceScope = services.CreateScope();
            IServiceProvider provider = serviceScope.ServiceProvider;

            var client = provider.GetService<Greeter.GreeterClient>();

            var call = client!.SayHelloDuplex(cancellationToken: cancellationToken);

            var readTask = Task.Run(async () =>
            {
                await foreach (var response in call.ResponseStream.ReadAllAsync(cancellationToken))
                {
                    Console.WriteLine(response.Message);
                }
            });

            foreach (var item in Enumerable.Range(0, 100)
                                             .Select(x => name))
            {
                await call.RequestStream.WriteAsync(new HelloRequest
                {
                    Name = $"{item}"
                });
                await Task.Delay(100);
            }
            await call.RequestStream.CompleteAsync();
            await readTask;
        }

        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddTransient<AuthDelegatingHandler>();
                    services.AddSingleton<ITokenStorage, TokenStorage>();
                    services.AddHttpClient<AuthHttpClient>(x =>
                    {
                        x.BaseAddress = new Uri(BaseAddress);
                    });

                    services
                        .AddGrpcClient<Greeter.GreeterClient>(o =>
                        {
                            o.Address = new Uri(BaseAddress);
                        })
                        .ConfigureChannel(o =>
                        {
                            o.Credentials = new SslCredentials();
                        })
                        .ConfigurePrimaryHttpMessageHandler(() =>
                        {
                            var handler = new HttpClientHandler
                            {
                                ServerCertificateCustomValidationCallback =
                                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                            };
                            return handler;
                        })
                        .AddHttpMessageHandler<AuthDelegatingHandler>()
                        ;
                })
                .ConfigureLogging(x => x.SetMinimumLevel(LogLevel.Warning));
    }
}
