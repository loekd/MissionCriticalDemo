using System.Diagnostics;
using Dapr.Client;

namespace MissionCriticalDemo.PlantApi.Services
{
    /// <summary>
    /// Gas storage as seen from the plant perspective
    /// </summary>
    public interface IGasStorage
    {
        Task<int> InjectGas(int amount);

        Task<int> WithdrawGas(int amount);

        Task<int> GetGasInStore();

        Task SetGasInStore(int amount);

        Task<int> GetMaximumFillLevel();
    }

    public class GasStorage(DaprClient daprClient, ActivitySource activitySource) : IGasStorage
    {
        private const string _gasInStoreStateStoreName = "plantstate";
        private const string _gasInStoreStateStoreKey = "overall_gas_in_store";

        public async Task<int> GetGasInStore()
        {
            int amount = await daprClient.GetStateAsync<int>(_gasInStoreStateStoreName, _gasInStoreStateStoreKey);
            return amount;
        }

        public async Task<int> InjectGas(int amount)
        {
            using var activity = activitySource.StartActivity("Plant Processing");
            activity?.SetTag("operation", "inject");
            activity?.SetTag("amount", amount);
            
            int newAmount = await GetGasInStore() + amount;
            int maxFillLevel = await GetMaximumFillLevel();
            if (newAmount > maxFillLevel)
            {
                activity?.SetTag("error", "Maximum capacity exceeded");
                throw new InvalidOperationException("Maximum capacity would be exceeded.");
            }

            using var delayActivity = activitySource.StartActivity("Processing Lead Time");
            await Task.Delay(500); //fake some processing time
            delayActivity?.Stop();

            await daprClient.SaveStateAsync(_gasInStoreStateStoreName, _gasInStoreStateStoreKey, newAmount);
            activity?.SetTag("new_amount", newAmount);
            return newAmount;
        }

        public async Task<int> WithdrawGas(int amount)
        {
            using var activity = activitySource.StartActivity("Plant Processing");
            activity?.SetTag("operation", "withdraw");
            activity?.SetTag("amount", amount);

            int newAmount = await GetGasInStore() - amount;
            if (newAmount < 0)
            {
                activity?.SetTag("error", "Insufficient gas in store");
                throw new InvalidOperationException("Not enough gas in store to complete this operation.");
            }

            using var delayActivity = activitySource.StartActivity("Processing Lead Time");
            await Task.Delay(500); //fake some processing time
            delayActivity?.Stop();

            await daprClient.SaveStateAsync(_gasInStoreStateStoreName, _gasInStoreStateStoreKey, newAmount);
            activity?.SetTag("new_amount", newAmount);
            return newAmount;
        }

        public Task<int> GetMaximumFillLevel()
        {
            return Task.FromResult(100);
        }

        public Task SetGasInStore(int amount)
        {
            return daprClient.SaveStateAsync(_gasInStoreStateStoreName, _gasInStoreStateStoreKey, amount);
        }
    }
}
