using Microsoft.AspNetCore.Mvc.TagHelpers.Cache;
using Vostok.Clusterclient.Core.Model;
using Vostok.Clusterclient.Core.Sending;
using Vostok.Clusterclient.Core.Strategies;

namespace AtomicRegistry.Client;

public class QuorumStrategy : IRequestStrategy
{
    private readonly int quorum;

    public QuorumStrategy(int quorumReplicaCount)
    {
        quorum = quorumReplicaCount;
    }

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

        //todo: написать нормальный код с тасочными кишками вместо этого :)
        var task12 = Task.WhenAll(tasks[0], tasks[1]);
        var task23 = Task.WhenAll(tasks[1], tasks[2]);
        var task31 = Task.WhenAll(tasks[2], tasks[0]);

        await Task.WhenAny(task12, task23, task31);
    }
}