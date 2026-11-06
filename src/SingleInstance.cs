using System;
using System.Security.Principal;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

public sealed class SingleInstance : IDisposable
{
    readonly Mutex mutex;
    readonly EventWaitHandle activate;
    RegisteredWaitHandle registration;

    SingleInstance(Mutex mutex, EventWaitHandle activate)
    {
        this.mutex = mutex;
        this.activate = activate;
    }

    public static SingleInstance Start(string name)
    {
        string scope;
        using (var user = WindowsIdentity.GetCurrent())
            scope = @"Local\" + name + "." + user.User.Value;

        var activate = new EventWaitHandle(false, EventResetMode.AutoReset, scope + ".Activate");
        var mutex = new Mutex(false, scope + ".Mutex");
        bool ownsMutex;
        try { ownsMutex = mutex.WaitOne(0); }
        catch (AbandonedMutexException) { ownsMutex = true; }
        if (ownsMutex) return new SingleInstance(mutex, activate);

        activate.Set();
        activate.Dispose();
        mutex.Dispose();
        return null;
    }

    public void Listen(Dispatcher dispatcher, Action onActivate)
    {
        registration = ThreadPool.RegisterWaitForSingleObject(activate,
            (state, timedOut) => dispatcher.BeginInvoke(onActivate),
            null, Timeout.Infinite, false);
    }

    public void Dispose()
    {
        if (registration != null) registration.Unregister(null);
        activate.Dispose();
        mutex.ReleaseMutex();
        mutex.Dispose();
    }
}
