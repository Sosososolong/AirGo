using Microsoft.AspNetCore.Mvc;
using Sylas.RemoteTasks.Common.Dtos;
using Sylas.RemoteTasks.Utils.Message;

namespace Sylas.RemoteTasks.App.Controllers
{
    public class MessageController(IConfiguration configuration) : CustomBaseController
    {
        public async Task<IActionResult> SendEmail(string to, string title, string content)
        {
            var sender = configuration.GetSection("Email:Sender").Get<EmailSender>() ?? throw new Exception("邮箱配置异常");
            var opResult = await EmailHelper.SendAsync(sender, to, title, content);
            var requestResult = opResult.IsSuccess ? RequestResult<bool>.Success(true) : RequestResult<bool>.Error(opResult.ErrMsg);
            return Ok(requestResult);
        }
    }
}
