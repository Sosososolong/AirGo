﻿namespace Sylas.RemoteTasks.App.RequestProcessor.Models.Dtos
{
    public class HttpRequestProcessorCreateDto
    {
        public string Title { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Headers { get; set; } = "{}";
        public string Remark { get; set; } = string.Empty;
        public bool StepCirleRunningWhenLastStepHasData { get; set; } = false;
    }
}
