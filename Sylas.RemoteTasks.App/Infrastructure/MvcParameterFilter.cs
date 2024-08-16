using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Sylas.RemoteTasks.Utils.Dto;

namespace Sylas.RemoteTasks.App.Infrastructure
{
    public class MvcParameterFilter : IActionFilter
    {
        public void OnActionExecuted(ActionExecutedContext context)
        {
            
        }

        public void OnActionExecuting(ActionExecutingContext context)
        {
            if (!context.ModelState.IsValid)
            {
                // 创建一个包含错误信息的字典
                List<string> errors = [];
                foreach (var item in context.ModelState)
                {
                    if (item.Value.ValidationState == Microsoft.AspNetCore.Mvc.ModelBinding.ModelValidationState.Invalid)
                    {
                        errors.Add($"{item.Key}: {string.Join(';', item.Value.Errors.Select(x => x.ErrorMessage))}");
                    }
                }

                // 返回一个包含错误信息的 JSON 响应
                context.Result = new JsonResult(new RequestResult<List<string>>(errors) { Code = 400 })
                {
                    StatusCode = 400 // Bad Request
                };
            }
        }
    }
}
