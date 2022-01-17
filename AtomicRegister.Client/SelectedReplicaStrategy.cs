using Vostok.Clusterclient.Core.Model;
using Vostok.Clusterclient.Core.Sending;
using Vostok.Clusterclient.Core.Strategies;

namespace AtomicRegister.Client;

public class SelectedReplicaStrategy : IRequestStrategy
{
    private Uri replica;

    public SelectedReplicaStrategy(Uri replica)
    {
        this.replica = replica;
    }

    public Task SendAsync(Request request, RequestParameters parameters, IRequestSender sender,
        IRequestTimeBudget budget,
        IEnumerable<Uri> replicas, int replicasCount, CancellationToken cancellationToken)
    {
        return sender.SendToReplicaAsync(replica, request, null, budget.Remaining, cancellationToken);
    }
}