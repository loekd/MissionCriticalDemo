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

        Task<int> GetMaximumFillLevel();
    }

    public class GasStorage(DaprClient daprClient) : IGasStorage
    {
        private const string _gasInStoreStateStoreName = "plant_state";
        private const string _gasInStoreStateStoreKey = "overall_gas_in_store";

        public async Task<int> GetGasInStore()
        {
            int amount = await daprClient.GetStateAsync<int>(_gasInStoreStateStoreName, _gasInStoreStateStoreKey);
            return amount;
        }

        public async Task<int> InjectGas(int amount)
        {
            int newAmount = await GetGasInStore() + amount;
            int maxFillLevel = await GetMaximumFillLevel();
            if (newAmount > maxFillLevel)
            {
                throw new InvalidOperationException("Maximum capacity would be exceeded.");
            }

            await Task.Delay(500); //fake some processing time
            await daprClient.SaveStateAsync(_gasInStoreStateStoreName, _gasInStoreStateStoreKey, newAmount);
            return newAmount;
        }

        public async Task<int> WithdrawGas(int amount)
        {
            int newAmount = await GetGasInStore() - amount;
            if (newAmount < 0)
            {
                throw new InvalidOperationException("Not enough gas in store to complete this operation.");
            }

            await Task.Delay(500); //fake some processing time
            await daprClient.SaveStateAsync(_gasInStoreStateStoreName, _gasInStoreStateStoreKey, newAmount);
            return newAmount;
        }

        public Task<int> GetMaximumFillLevel()
        {
            return Task.FromResult(100);
        }
    }
}
