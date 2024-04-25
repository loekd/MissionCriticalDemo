using Dapr.Client;
using MissionCriticalDemo.Shared;
using System.Text.Json;
using MissionCriticalDemo.Messages;

namespace MissionCriticalDemo.DispatchApi.Services
{
    /// <summary>
    /// Gas storage as seen from the customer perspective
    /// </summary>
    public interface IGasStorage
    {
        Task SetGasInStore(Guid customerId, int amount);
        Task<int> GetGasInStore(Guid customerId);
        Task ProcessRequest(Guid customerId, Shared.Contracts.Request request);
    }

    public class GasStorage(DaprClient daprClient, IMappers mappers) : IGasStorage
    {
        private const string _gasInStoreStateStoreName = "gasinstorestate";
        private const string _outboxStateStoreName = "dispatchstate";


        public async Task<int> GetGasInStore(Guid customerId)
        {
            //return 100;
            int amount = await daprClient.GetStateAsync<int>(_gasInStoreStateStoreName, customerId.ToGuidString());
            return amount;
        }

        public async Task SetGasInStore(Guid customerId, int amount)
        {
            await daprClient.SaveStateAsync(_gasInStoreStateStoreName, customerId.ToGuidString(), amount);
        }

        public async Task ProcessRequest(Guid customerId, Shared.Contracts.Request request)
        {
            //we don't directly call the Plant API, but instead put a message in the outbox
            //this way we can retry if the Plant API is down
            var message = mappers.ToMessage(request, customerId);
            var requests = new List<StateTransactionRequest>()
            {
                new(request.RequestId.ToGuidString(), JsonSerializer.SerializeToUtf8Bytes(message), StateOperationType.Upsert),
                //TODO: add additional state changes in same transaction if needed
            };

            //save changes in transaction
            await daprClient.ExecuteStateTransactionAsync(_outboxStateStoreName, requests, cancellationToken: CancellationToken.None);
        }
    }
}
