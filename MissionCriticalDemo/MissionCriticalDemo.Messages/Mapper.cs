using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using DataRequest = MissionCriticalDemo.Shared.Contracts.Request;
using MessageRequest = MissionCriticalDemo.Messages.Request;

using DataResponse = MissionCriticalDemo.Shared.Contracts.Response;
using MessageResponse = MissionCriticalDemo.Messages.Response;


using DataCustomerRequest = MissionCriticalDemo.Shared.Contracts.CustomerRequest;


namespace MissionCriticalDemo.Messages
{
    public interface IMappers
    {
        DataRequest ToContract(MessageRequest input);
        DataResponse ToContract(DataCustomerRequest input, int customerTotal, bool success = true);
        DataCustomerRequest ToCustomerContract(MessageResponse input);
        MessageRequest ToMessage(DataRequest input, Guid customerId);
        MessageResponse ToMessage(DataResponse input, Guid customerId);
        MessageResponse ToResponse(MessageRequest input, Guid flowResponseId, bool success, DateTimeOffset timestamp, int currentFillLevel, int maxFillLevel);
    }

    public class Mappers : IMappers
    {
        public DataRequest ToContract(MessageRequest input)
        {
            var configuration = new MapperConfiguration(cfg => cfg
                .CreateMap<MessageRequest, DataRequest>()
                .ForCtorParam(nameof(DataRequest.AmountInGWh), x => x.MapFrom(i => i.AmountInGWh))
                .ForCtorParam(nameof(DataRequest.Direction), x => x.MapFrom(i => i.Direction))
                .ForCtorParam(nameof(DataRequest.RequestId), x => x.MapFrom(i => i.RequestId))
                .ForCtorParam(nameof(DataRequest.Timestamp), x => x.MapFrom(i => i.Timestamp))
                , NullLoggerFactory.Instance);
            Mapper mapper = new(configuration);
            return mapper.Map<DataRequest>(input);
        }

        public DataCustomerRequest ToCustomerContract(MessageResponse input)
        {
            var configuration = new MapperConfiguration(cfg => cfg
                .CreateMap<MessageResponse, DataCustomerRequest>()
                .ForCtorParam(nameof(DataCustomerRequest.AmountInGWh), x => x.MapFrom(i => i.AmountInGWh))
                .ForCtorParam(nameof(DataCustomerRequest.Direction), x => x.MapFrom(i => i.Direction))
                .ForCtorParam(nameof(DataCustomerRequest.RequestId), x => x.MapFrom(i => i.ResponseId))
                .ForCtorParam(nameof(DataCustomerRequest.Timestamp), x => x.MapFrom(i => i.Timestamp))
                .ForCtorParam(nameof(DataCustomerRequest.CustomerId), x => x.MapFrom(i => i.CustomerId))
                .ForCtorParam(nameof(DataCustomerRequest.CurrentFillLevel), x => x.MapFrom(i => i.CurrentFillLevel))
                .ForCtorParam(nameof(DataCustomerRequest.MaxFillLevel), x => x.MapFrom(i => i.MaxFillLevel))
                .ForCtorParam(nameof(DataCustomerRequest.Success), x => x.MapFrom(i => i.Success))
                , NullLoggerFactory.Instance);
            Mapper mapper = new(configuration);
            return mapper.Map<DataCustomerRequest>(input);
        }

        public MessageRequest ToMessage(DataRequest input, Guid customerId)
        {
            var configuration = new MapperConfiguration(cfg => cfg
            .CreateMap<DataRequest, MessageRequest>()
            .ForCtorParam(nameof(MessageRequest.AmountInGWh), x => x.MapFrom(i => i.AmountInGWh))
            .ForCtorParam(nameof(MessageRequest.Direction), x => x.MapFrom(i => i.Direction))
            .ForCtorParam(nameof(MessageRequest.RequestId), x => x.MapFrom(i => i.RequestId))
            .ForCtorParam(nameof(MessageRequest.Timestamp), x => x.MapFrom(i => i.Timestamp))
            .ForCtorParam(nameof(MessageRequest.CustomerId), x => x.MapFrom(i => customerId))
                , NullLoggerFactory.Instance);

            Mapper mapper = new(configuration);
            return mapper.Map<MessageRequest>(input);
        }

        public DataResponse ToContract(DataCustomerRequest input, int customerTotal, bool success = true)
        {
            var configuration = new MapperConfiguration(cfg => cfg.CreateMap<DataCustomerRequest, DataResponse>()
            .ForCtorParam(nameof(DataResponse.AmountInGWh), x => x.MapFrom(i => i.AmountInGWh))
            .ForCtorParam(nameof(DataResponse.Direction), x => x.MapFrom(i => i.Direction))
            .ForCtorParam(nameof(DataResponse.RequestId), x => x.MapFrom(i => i.RequestId))
            .ForCtorParam(nameof(DataResponse.Timestamp), x => x.MapFrom(i => i.Timestamp))
            .ForCtorParam(nameof(DataResponse.ResponseId), x => x.MapFrom(i => i.RequestId))
            .ForCtorParam(nameof(DataResponse.Success), x => x.MapFrom(i => success))
            .ForCtorParam(nameof(DataResponse.TotalAmountInGWh), x => x.MapFrom(i => customerTotal))
            .ForCtorParam(nameof(DataResponse.CurrentFillLevel), x => x.MapFrom(i => i.CurrentFillLevel))
                , NullLoggerFactory.Instance);

            Mapper mapper = new(configuration);
            return mapper.Map<DataResponse>(input);
        }

        public MessageResponse ToMessage(DataResponse input, Guid customerId)
        {
            var configuration = new MapperConfiguration(cfg => cfg.CreateMap<DataResponse, MessageResponse>()
            .ForCtorParam(nameof(MessageResponse.AmountInGWh), x => x.MapFrom(i => i.AmountInGWh))
            .ForCtorParam(nameof(MessageResponse.Direction), x => x.MapFrom(i => i.Direction))
            .ForCtorParam(nameof(MessageResponse.RequestId), x => x.MapFrom(i => i.RequestId))
            .ForCtorParam(nameof(MessageResponse.Timestamp), x => x.MapFrom(i => i.Timestamp))
            .ForCtorParam(nameof(MessageResponse.ResponseId), x => x.MapFrom(i => i.ResponseId))
            .ForCtorParam(nameof(MessageResponse.CustomerId), x => x.MapFrom(i => customerId))
                , NullLoggerFactory.Instance);
            Mapper mapper = new(configuration);
            return mapper.Map<MessageResponse>(input);
        }

        public MessageResponse ToResponse(MessageRequest input, Guid flowResponseId, bool success, DateTimeOffset timestamp, int currentFillLevel, int maxFillLevel)
        {
            var configuration = new MapperConfiguration(cfg => cfg.CreateMap<MessageRequest, MessageResponse>()
            .ForCtorParam(nameof(MessageResponse.AmountInGWh), x => x.MapFrom(i => i.AmountInGWh))
            .ForCtorParam(nameof(MessageResponse.Direction), x => x.MapFrom(i => i.Direction))
            .ForCtorParam(nameof(MessageResponse.RequestId), x => x.MapFrom(i => i.RequestId))
            .ForCtorParam(nameof(MessageResponse.CustomerId), x => x.MapFrom(i => i.CustomerId))

            .ForCtorParam(nameof(MessageResponse.ResponseId), x => x.MapFrom(i => flowResponseId))
            .ForCtorParam(nameof(MessageResponse.Success), x => x.MapFrom(i => success))
            .ForCtorParam(nameof(MessageResponse.Timestamp), x => x.MapFrom(i => timestamp))
            .ForCtorParam(nameof(MessageResponse.CurrentFillLevel), x => x.MapFrom(i => currentFillLevel))
            .ForCtorParam(nameof(MessageResponse.MaxFillLevel), x => x.MapFrom(i => maxFillLevel))
                , NullLoggerFactory.Instance);
            Mapper mapper = new(configuration);
            MessageResponse flowResponse = mapper.Map<MessageResponse>(input);
            return flowResponse;
        }
    }
}
