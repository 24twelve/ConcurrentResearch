using JetBrains.Annotations;

namespace AtomicRegister.Configuration;

[UsedImplicitly]
public class ConcurrentCounter
{
    private int counter;

    public int CurrentCount => counter;

    public void Increment()
    {
        Interlocked.Increment(ref counter);
    }

    public void Decrement()
    {
        Interlocked.Decrement(ref counter);
    }
}