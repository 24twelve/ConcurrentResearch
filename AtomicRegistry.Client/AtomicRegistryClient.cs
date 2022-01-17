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
    public readonly string ClientId;

    public AtomicRegistryClient(IClusterProvider nodeClusterProvider, ILog log, string clientId)
    {
        ClientId = clientId;
        client = new ClusterClient(log, configuration =>
        {
            configuration.ClusterProvider = nodeClusterProvider;
            configuration.Transport = new UniversalTransport(log);
            configuration.ConnectionAttempts = 1;
            configuration.RetryStrategy = new ImmediateRetryStrategy(999);
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
        ClusterResult? clusterResult = null;
        while (clusterResult == null || clusterResult.Response.Code != ResponseCode.Ok)
        {
            var currentValue = await GetInternal(false);
            clusterResult = await SetInternal(new ValueDto((currentValue.Timestamp ?? 0) + 1, value, ClientId));
        }
    }

    public async Task<ValueDto?> Get()
    {
        return await GetInternal(true).ConfigureAwait(false);
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

        if (shouldGetRepair && mostRecentValue.Value != null)
            await SetInternal(mostRecentValue);

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
        var request = Request.Post(new Uri("api/faults/push", UriKind.Relative))
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
        var request = Request.Post(new Uri("api/faults/push", UriKind.Relative))
            .WithContent(FaultSettingsDto.EverythingOk.ToJson())
            .WithContentTypeHeader("application/json");
        var requestParameters = new RequestParameters()
            .WithStrategy(
                new SelectedReplicaStrategy(AtomicRegistryNodeClusterProvider.InstancesTopology()[instanceName]));
        var result = await client.SendAsync(request, requestParameters);
        if (result.Response.Code != ResponseCode.Ok)
            throw new Exception("Post result not 200");
    }

    private async Task<ClusterResult> SetInternal(ValueDto value)
    {
        var content = value.ToJson();
        var request = Request.Post(new Uri("api/set", UriKind.Relative))
            .WithContent(content)
            .WithContentTypeHeader("application/json");
        var requestParameters = new RequestParameters().WithStrategy(new QuorumStrategy(QuorumReplicaCount));
        var result = await client.SendAsync(request, requestParameters);
        if (result.Response.Code != ResponseCode.Ok && result.Response.Code != ResponseCode.Conflict)
            throw new Exception(
                $"Set result not 200 or 409, but {result.Response.Code}. {result.ReplicaResults.Select(x => x.Response.Code).ToJson()}. Tried to set {value.ToJson()}");
        return result;
    }
}