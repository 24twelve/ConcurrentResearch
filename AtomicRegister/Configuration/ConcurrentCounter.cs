using JetBrains.Annotations;

namespace AtomicRegister.Configuration;

[UsedImplicitly]
public class ConcurrentCounter
{
    private int counter;
    public int LeaseCount => counter;
    public Lease TakeLease()
    {
        return new Lease(this);
    }

    public class Lease : IDisposable
    {
        private readonly ConcurrentCounter counter;

        public Lease(ConcurrentCounter counter)
        {
            this.counter = counter;
            Interlocked.Increment(ref counter.counter);
        }

        public void Dispose()
        {
            Interlocked.Decrement(ref counter.counter);
        }
    }
}