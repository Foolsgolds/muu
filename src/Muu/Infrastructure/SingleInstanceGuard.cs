namespace Muu.Infrastructure;

public sealed class SingleInstanceGuard : IDisposable
{
    private readonly Mutex _mutex;
    public bool IsFirstInstance { get; }

    public SingleInstanceGuard()
    {
        _mutex = new Mutex(true, "Global\\MuuLauncher_SingleInstance", out bool createdNew);
        IsFirstInstance = createdNew;
    }

    public void Dispose() => _mutex.Dispose();
}
