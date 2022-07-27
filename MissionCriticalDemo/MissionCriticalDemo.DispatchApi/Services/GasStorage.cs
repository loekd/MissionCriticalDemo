using System.Collections.Concurrent;

namespace MissionCriticalDemo.DispatchApi.Services
{
    public interface IGasStorage
    {
        Task<int> AddGasInStore(Guid customerId, int amount);
        Task<int> GetGasInStore(Guid customerId);
    }

    public class GasStorage : IGasStorage
    {
        private ConcurrentDictionary<Guid, int> _store = new();

        public Task<int> GetGasInStore(Guid customerId)
        {
            _store.TryGetValue(customerId, out int amount);
            return Task.FromResult(amount);
        }

        public Task<int> AddGasInStore(Guid customerId, int amount)
        {
            _store.AddOrUpdate(customerId, c => amount, (c, a) => a + amount);
            return GetGasInStore(customerId);
        }
    }
}
