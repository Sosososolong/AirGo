using Sylas.RemoteTasks.App.DataHandlers;
using Sylas.RemoteTasks.App.Models.HttpRequestProcessor.Dtos;
using System.Diagnostics;

namespace Sylas.RemoteTasks.App.Models.HttpRequestProcessor
{
    public static class HttpRequestProcessorExtensions
    {
        public static HttpRequestProcessorStepCreateDto ToCreateDto(this HttpRequestProcessorStep source)
        {
            return new HttpRequestProcessorStepCreateDto
            {
                ProcessorId = source.ProcessorId,
                DataContextBuilder = source.DataContextBuilder,
                RequestBody = source.RequestBody,
                Parameters = source.Parameters,
                PresetDataContext = source.PresetDataContext,
                Remark = source.Remark
            };
        }
        
        public static HttpRequestProcessorStepDataHandlerCreateDto ToCreateDto(this HttpRequestProcessorStepDataHandler source)
        {
            return new HttpRequestProcessorStepDataHandlerCreateDto
            {
                StepId = source.StepId,
                OrderNo = source.OrderNo,
                DataHandler = source.DataHandler,
                Enabled = source.Enabled,
                ParametersInput = source.ParametersInput,
                Remark = source.Remark
            };
        }
        
        public static HttpRequestProcessorCreateDto ToCreateDto(this HttpRequestProcessor source)
        {
            return new HttpRequestProcessorCreateDto {
                Headers = source.Headers,
                Name = source.Name,
                Title = source.Title,
                Url = source.Url,
                Remark = source.Remark,
                StepCirleRunningWhenLastStepHasData = source.StepCirleRunningWhenLastStepHasData
            };
        }
    }
}
