using Vostok.Clusterclient.Core.Topology;

namespace AtomicRegistry.Client
{
    public class AtomicRegistryNodeClusterProvider : IClusterProvider
    {
        public IList<Uri> GetCluster()
        {
            return new[]
            {
                new Uri("https://localhost:6001"),
                new Uri("https://localhost:6002"),
                new Uri("https://localhost:6003"),
            }.ToList();
        }
    }
}