using Vostok.Clusterclient.Core.Model;
using Vostok.Clusterclient.Core.Sending;
using Vostok.Clusterclient.Core.Strategies;

namespace AtomicRegistry.Client;

public class AllReplicas200Strategy : IRequestStrategy
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
                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            }
        }).ToArray();

        await Task.WhenAll(tasks);
    }
}