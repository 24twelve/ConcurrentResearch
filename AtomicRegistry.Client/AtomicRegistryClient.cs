using AtomicRegistry.Common;
using Vostok.Clusterclient.Core;
using Vostok.Clusterclient.Core.Model;
using Vostok.Clusterclient.Core.Retry;
using Vostok.Clusterclient.Core.Topology;
using Vostok.Clusterclient.Transport;
using Vostok.Logging.Abstractions;

namespace AtomicRegistry.Client
{
    public class AtomicRegistryClient
    {
        private const int QuorumReplicaCount = 2;
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
                //todo: rebuild for quorums
                configuration.DefaultRequestStrategy = new QuorumStrategy(QuorumReplicaCount);
                configuration.DefaultTimeout = TimeSpan.FromSeconds(60);
                configuration.Logging.LogReplicaRequests = true;
                configuration.Logging.LogReplicaResults = true;
                configuration.Logging.LogRequestDetails = true;
                configuration.Logging.LogResultDetails = true;
            });
        }

        public void Set(string value)
        {
            var content = new ValueDto(timestamp, value).ToJson();
            var request = Request.Post(new Uri($"api/set", UriKind.Relative))
                .WithContent(content)
                .WithContentTypeHeader("application/json");
            lock (locker)
            {
                var result = client.SendAsync(request).GetAwaiter().GetResult();
                if (result.Response.Code != ResponseCode.Ok)
                    throw new Exception("Post result not 200");
                timestamp++;
            }
        }

        public async Task<string?> Get()
        {
            var request = Request.Get(new Uri("api/", UriKind.Relative));
            var result = await client.SendAsync(request);
            if (result.Response.Code != ResponseCode.Ok)
                throw new Exception("Get result not 200");

            return result.Response.Content.ToString().FromJson<ValueDto>()?.Value;
        }

        //todo: предположим что атмомарность этой операции в задачу не входит :)
        public async Task Drop()
        {
            var request = Request.Delete(new Uri("api/drop", UriKind.Relative));
            var result = await client.SendAsync(request);
            if (result.Response.Code != ResponseCode.Ok)
                throw new Exception("Get result not 200");
        }
    }
}