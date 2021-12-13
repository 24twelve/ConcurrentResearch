using System;
using System.Collections.Generic;
using System.Linq;
using Vostok.Clusterclient.Core.Topology;

namespace AtomicRegistry.Client
{
    public class AtomicRegistryNodeClusterProvider : IClusterProvider
    {
        public IList<Uri> GetCluster()
        {
            return new[]
            {
                new Uri("http://localhost:5001"),
                new Uri("http://localhost:5002"),
                new Uri("http://localhost:5003"),
            }.ToList();
        }
    }
}