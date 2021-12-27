using AtomicRegistry.Common;
using AtomicRegistry.Dto;
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
        private int timestamp;

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
                configuration.DefaultTimeout = TimeSpan.FromMinutes(60);
                configuration.Logging.LogReplicaRequests = true;
                configuration.Logging.LogReplicaResults = true;
                configuration.Logging.LogRequestDetails = true;
                configuration.Logging.LogResultDetails = true;
            });
        }

        public Task Set(string value)
        {
            lock (locker)
            {
                SetInternal(new ValueDto(timestamp, value), null);
                timestamp++;
            }

            return Task.CompletedTask;
        }

        //todo: some clever way for exception throwing
        public async Task<string?> Get()
        {
            var request = Request.Get(new Uri("api/", UriKind.Relative));
            var result = await client.SendAsync(request);
            if (result.Response.Code != ResponseCode.Ok)
                throw new Exception($"Get result not 200. {result.Status}");

            var clusterResults = result.ReplicaResults
                .Where(x => x.Response.Code == ResponseCode.Ok)
                .Select(x => (x.Replica, x.Response.Content.ToString().FromJson<ValueDto>() ?? ValueDto.Empty))
                .OrderByDescending(x => x.Item2.Version)
                .ToArray();

            var laggingReplicas = clusterResults
                .Where(x => x.Item2.Version != clusterResults[0].Item2.Version)
                .ToArray();

            var mostRecentValue = clusterResults.First().Item2;

            foreach (var laggingReplica in laggingReplicas)
                SetInternal(mostRecentValue, laggingReplica.Replica);

            return mostRecentValue.Value;
        }

        public async Task Drop()
        {
            var request = Request.Delete(new Uri("api/drop", UriKind.Relative));
            var requestParameters = new RequestParameters()
                .WithStrategy(
                    new AllReplicas200Strategy());
            var result = await client.SendAsync(request, requestParameters);

            if (result.Response.Code != ResponseCode.Ok)
                throw new Exception("Drop result not 200");
        }

        public async Task InduceFault(string instanceName, FaultSettingsDto faultSettingsDto)
        {
            var request = Request.Post(new Uri($"api/faults/push", UriKind.Relative))
                .WithContent(faultSettingsDto.ToJson())
                .WithContentTypeHeader("application/json");
            var requestParameters = new RequestParameters()
                .WithStrategy(
                    new SelectedReplicaStrategy(AtomicRegistryNodeClusterProvider.InstancesTopology()[instanceName]));
            var result = await client.SendAsync(request, requestParameters);
            if (result.Response.Code != ResponseCode.Ok)
                throw new Exception("Post result not 200");
        }

        public async Task ResetFault(string instanceName)
        {
            var request = Request.Post(new Uri($"api/faults/push", UriKind.Relative))
                .WithContent(FaultSettingsDto.EverythingOk.ToJson())
                .WithContentTypeHeader("application/json");
            var requestParameters = new RequestParameters()
                .WithStrategy(
                    new SelectedReplicaStrategy(AtomicRegistryNodeClusterProvider.InstancesTopology()[instanceName]));
            var result = await client.SendAsync(request, requestParameters);
            if (result.Response.Code != ResponseCode.Ok)
                throw new Exception("Post result not 200");
        }

        private void SetInternal(ValueDto value, Uri? replica)
        {
            var content = value.ToJson();
            var request = Request.Post(new Uri($"api/set", UriKind.Relative))
                .WithContent(content)
                .WithContentTypeHeader("application/json");
            var requestParameters = new RequestParameters();
            if (replica != null)
                requestParameters = requestParameters.WithStrategy(new SelectedReplicaStrategy(replica));

            //todo: some clever way to handle error message from server
            var result = client.SendAsync(request, requestParameters).GetAwaiter().GetResult();
            if (result.Response.Code != ResponseCode.Ok)
                throw new Exception($"Set result not 200. {result.ReplicaResults.Select(x => x.Response.Code).ToJson()}");
        }
    }
}