using Grpc.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace TokenRefreshGrpcService
{
    public class GreeterService : Greeter.GreeterBase
    {
        private readonly ILogger<GreeterService> _logger;
        public GreeterService(ILogger<GreeterService> logger)
        {
            _logger = logger;
        }

        [Authorize]
        public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
        {
            return Task.FromResult(new HelloReply
            {
                Message = $"Hello {request.Name} or {context.GetHttpContext().User.Identity!.Name}"
            });
        }

        [Authorize]
        public override async Task SayHelloStream(HelloRequest request, IServerStreamWriter<HelloReply> responseStream, ServerCallContext context)
        {
            for (int i = 0; i < 5; i++)
            {
                await responseStream.WriteAsync(new HelloReply() { Message = $"Server streaming hello {request.Name} ¹{i}" });
            }
        }

        [Authorize]
        public override async Task<HelloReply> SayHelloClienStream(IAsyncStreamReader<HelloRequest> requestStream, ServerCallContext context)
        {
            var sb = new StringBuilder();
            await foreach (var request in requestStream.ReadAllAsync(context.CancellationToken))
            {
                sb.Append(request.Name);
            }

            return new HelloReply()
            {
                Message = string.Format(sb.ToString(), context.GetHttpContext().User.Identity!.Name)
            };
        }

        [Authorize]
        public override async Task SayHelloDuplex(IAsyncStreamReader<HelloRequest> requestStream, IServerStreamWriter<HelloReply> responseStream, ServerCallContext context)
        {
            await foreach (var request in requestStream.ReadAllAsync(context.CancellationToken))
            {
                await responseStream.WriteAsync(new HelloReply() { Message = $"Duplex hello {request.Name}" });
            }

            //await AwaitCancellation(context.CancellationToken);
        }

        private static Task AwaitCancellation(CancellationToken token)
        {
            var completion = new TaskCompletionSource<object>();
            token.Register(() => completion.SetResult(null));
            return completion.Task;
        }

    }
}
