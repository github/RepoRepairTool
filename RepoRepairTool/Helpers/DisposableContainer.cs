using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Text;
using System.Threading;

namespace RepoRepairTool.Helpers
{
    public static class DisposableContainer
    {
        public static DisposableContainer<T1> Create<T1>(T1 value, IDisposable disposable)
        {
            return new DisposableContainer<T1>(value, disposable);
        }

        public static DisposableContainer<T1> Create<T1>(T1 value, Action<T1> disposable)
        {
            return new DisposableContainer<T1>(value, Disposable.Create(() => disposable(value)));
        }
    }

    public sealed class DisposableContainer<T> : IDisposable
    {
        IDisposable _inner;

        public T Value { get; private set; }

        public DisposableContainer(T value, IDisposable disposable)
        {
            Value = value;
            _inner = disposable;
        }

        public void Dispose()
        {
            var disp = Interlocked.Exchange(ref _inner, null);
            if (disp != null)
            {
                disp.Dispose();
            }
        }
    }
}
