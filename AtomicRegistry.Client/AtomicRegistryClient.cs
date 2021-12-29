using AtomicRegistry.Common;
using AtomicRegistry.Dto;
using Vostok.Clusterclient.Core;
using Vostok.Clusterclient.Core.Model;
using Vostok.Clusterclient.Core.Retry;
using Vostok.Clusterclient.Core.Topology;
using Vostok.Clusterclient.Transport;
using Vostok.Logging.Abstractions;

namespace AtomicRegistry.Client;

public class AtomicRegistryClient
{
    private const int QuorumReplicaCount = 2;
    private readonly IClusterClient client;
    private readonly string clientId;

    public AtomicRegistryClient(IClusterProvider nodeClusterProvider, ILog log, string clientId)
    {
        this.clientId = clientId;
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

    public async Task Set(string value)
    {
        var currentValue = await GetInternal(false);
        await SetInternal(new ValueDto((currentValue.Timestamp ?? 0) + 1, value, clientId), null);
    }

    //todo: some clever way for exception throwing
    //todo: one and forever way to .ConfigureAwait(false) everywhere its needed
    public async Task<string?> Get()
    {
        var mostRecentValue = await GetInternal(true);

        return mostRecentValue.Value;
    }

    private async Task<ValueDto> GetInternal(bool shouldGetRepair)
    {
        var request = Request.Get(new Uri("api/", UriKind.Relative));
        var result = await client.SendAsync(request);
        if (result.Response.Code != ResponseCode.Ok)
            throw new Exception($"Get result not 200. {result.Status}");

        var clusterResults = result.ReplicaResults
            .Where(x => x.Response.Code == ResponseCode.Ok)
            .Select(x => (x.Replica, x.Response.Content.ToString().FromJson<ValueDto>() ?? ValueDto.Empty))
            .OrderByDescending(x => x.Item2.Timestamp)
            .ToArray();

        var mostRecentValue = clusterResults.First().Item2;

        if (shouldGetRepair)
        {
            var laggingReplicas = clusterResults
                .Where(x => x.Item2.Timestamp != clusterResults[0].Item2.Timestamp || (x.Item2.ClientId != clientId &&
                    x.Item2.Timestamp == clusterResults[0].Item2.Timestamp)).ToArray();

            foreach (var laggingReplica in laggingReplicas)
                await SetInternal(mostRecentValue, laggingReplica.Replica);
        }

        return mostRecentValue;
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

    private async Task SetInternal(ValueDto value, Uri? replica)
    {
        var content = value.ToJson();
        var request = Request.Post(new Uri($"api/set", UriKind.Relative))
            .WithContent(content)
            .WithContentTypeHeader("application/json");
        var requestParameters = new RequestParameters();
        if (replica != null)
            requestParameters = requestParameters.WithStrategy(new SelectedReplicaStrategy(replica));

        //todo: some clever way to handle error message from server
        var result = await client.SendAsync(request, requestParameters);
        if (result.Response.Code != ResponseCode.Ok && result.Response.Code != ResponseCode.Conflict)
            throw new Exception(
                $"Set result not 200, but {result.Response.Code}. {result.ReplicaResults.Select(x => x.Response.Code).ToJson()}. Tried to set {value.ToJson()}");
    }
}