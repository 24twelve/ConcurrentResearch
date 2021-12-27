using Vostok.Clusterclient.Core.Topology;

namespace AtomicRegistry.Client;

public class AtomicRegistryNodeClusterProvider : IClusterProvider
{
    public IList<Uri> GetCluster()
    {
        return InstancesTopology().Values.ToList();
    }

    public static Dictionary<string, Uri> InstancesTopology()
    {
        return new Dictionary<string, Uri>()
        {
            ["Instance1"] = new("https://localhost:6001"),
            ["Instance2"] = new("https://localhost:6002"),
            ["Instance3"] = new("https://localhost:6003"),
        };
    }
}