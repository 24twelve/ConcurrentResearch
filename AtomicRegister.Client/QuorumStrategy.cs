using Vostok.Clusterclient.Core.Model;
using Vostok.Clusterclient.Core.Sending;
using Vostok.Clusterclient.Core.Strategies;

namespace AtomicRegister.Client;

public class QuorumStrategy : IRequestStrategy
{
    private readonly int quorum;

    public QuorumStrategy(int quorumReplicaCount)
    {
        quorum = quorumReplicaCount;
    }

    public Task SendAsync(Request request, RequestParameters parameters, IRequestSender sender,
        IRequestTimeBudget budget,
        IEnumerable<Uri> replicas, int replicasCount, CancellationToken cancellationToken)
    {
        var successfulRequests = 0;
        var tasks = replicas.Select(async replica =>
        {
            ResponseCode replicaResponseCode = 0;
            while (replicaResponseCode != ResponseCode.Ok && replicaResponseCode != ResponseCode.Conflict)
            {
                var replicaResult =
                    await sender.SendToReplicaAsync(replica, request, null, budget.Remaining, cancellationToken);

                replicaResponseCode = replicaResult.Response.Code;
                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            }

            Interlocked.Increment(ref successfulRequests);
        }).ToArray();

        foreach (var task in tasks)
        {
#pragma warning disable CS4014
            Task.Run(() => task, cancellationToken);
#pragma warning restore CS4014
        }

        while (successfulRequests < quorum) Thread.Sleep(5);

        return Task.CompletedTask;
    }
}