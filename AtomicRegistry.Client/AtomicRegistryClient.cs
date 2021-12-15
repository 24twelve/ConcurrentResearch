using System.Text;
using Vostok.Clusterclient.Core;
using Vostok.Clusterclient.Core.Model;
using Vostok.Clusterclient.Core.Retry;
using Vostok.Clusterclient.Core.Sending;
using Vostok.Clusterclient.Core.Strategies;
using Vostok.Clusterclient.Core.Topology;
using Vostok.Clusterclient.Transport;
using Vostok.Logging.Abstractions;

namespace AtomicRegistry.Client
{
    public class AtomicRegistryClient
    {
        private readonly IClusterClient client;
        private readonly object locker = new();
        private int timestamp = 0;

        public AtomicRegistryClient(IClusterProvider nodeClusterProvider, ILog log)
        {
            client = new ClusterClient(log, configuration =>
            {
                configuration.ClusterProvider = nodeClusterProvider;
                configuration.Transport = new UniversalTransport(log);
                configuration.ConnectionAttempts = 1;
                configuration.RetryStrategy = new ImmediateRetryStrategy(999);
                configuration.DefaultRequestStrategy = new AllReplica200RequestStrategy();
                configuration.DefaultTimeout = TimeSpan.FromSeconds(60);
                configuration.Logging.LogReplicaRequests = true;
                configuration.Logging.LogReplicaResults = true;
                configuration.Logging.LogRequestDetails = true;
                configuration.Logging.LogResultDetails = true;
            });
        }

        public void Set(string value)
        {
            var request = Request.Post(new Uri($"api/set", UriKind.Relative))
                .WithAdditionalQueryParameter("value", value);
            lock (locker)
            {
                var result = client.SendAsync(request).GetAwaiter().GetResult();
                if (result.Response.Code != ResponseCode.Ok)
                    throw new Exception("Get result not 200");
                timestamp++;
            }
        }

        public async Task<string> Get()
        {
            var request = Request.Get(new Uri("api/", UriKind.Relative));
            var result = await client.SendAsync(request);
            if (result.Response.Code != ResponseCode.Ok)
                throw new Exception("Get result not 200");

            var response = result.Response;
            return response.Content.ToString();
        }
    }

    //todo: rebuild for quorums
    public class AllReplica200RequestStrategy : IRequestStrategy
    {
        public async Task SendAsync(Request request, RequestParameters parameters, IRequestSender sender,
            IRequestTimeBudget budget,
            IEnumerable<Uri> replicas, int replicasCount, CancellationToken cancellationToken)
        {
            var tasks = replicas.Select(async replica =>
            {
                ResponseCode replicaResponseCode = 0;
                while (replicaResponseCode != ResponseCode.Ok)
                {
                    var replicaResult =
                        await sender.SendToReplicaAsync(replica, request, null, budget.Remaining, cancellationToken);

                    replicaResponseCode = replicaResult.Response.Code;
                    await Task.Delay(50, cancellationToken);
                }
            }).ToArray();
            await Task.WhenAll(tasks);
        }
    }
}