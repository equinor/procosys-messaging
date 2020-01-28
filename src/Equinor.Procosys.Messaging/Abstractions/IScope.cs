using System;

namespace Equinor.Procosys.Messaging.Abstractions
{
    public interface IScope : IDisposable
    {
        object GetHandler(Type handlerType);
    }
}
