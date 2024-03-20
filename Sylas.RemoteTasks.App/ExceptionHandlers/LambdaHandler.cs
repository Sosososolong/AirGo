using Microsoft.AspNetCore.Diagnostics;
using static System.Net.Mime.MediaTypeNames;
using Sylas.RemoteTasks.Utils.Dto;

namespace Sylas.RemoteTasks.App.ExceptionHandlers
{
    public class LambdaHandler
    {
        public static Action<IApplicationBuilder> ReturnOperationResultAction = exceptionHandlerApp =>
        {
            exceptionHandlerApp.Run(async context =>
            {
                context.Response.StatusCode = StatusCodes.Status200OK;

                // using static System.Net.Mime.MediaTypeNames;
                context.Response.ContentType = Application.Json; //Text.Plain;

                var exceptionHandlerPathFeature =
                    context.Features.Get<IExceptionHandlerPathFeature>();

                var exception = exceptionHandlerPathFeature?.Error;
                var errMsg = exception?.Message ?? "未知的异常";
                await context.Response.WriteAsJsonAsync(new OperationResult(false, errMsg));
            });
        };
    }
}
