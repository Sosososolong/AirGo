using IdentityModel.Client;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Sylas.RemoteTasks.App.Controllers
{
    public class OAuthController : Controller
    {
        /// <summary>
        /// 测试微信钉钉第三方登录
        /// </summary>
        /// <returns></returns>
        public IActionResult Index()
        {
            return View();
        }

        /// <summary>
        /// 密码模式
        /// </summary>
        /// <returns></returns>
        public IActionResult Password()
        {
            return View();
        }

        /// <summary>
        /// 从身份认证层获取用户信息
        /// </summary>
        /// <returns></returns>
        [Authorize]
        public async Task<IActionResult> UserInfoAsync([FromServices] IHttpClientFactory clientFactory, string authority)
        {
            var claims = User.Claims;
            var client = clientFactory.CreateClient();
            var token = HttpContext.Request.Headers.Authorization.FirstOrDefault()?.Replace("Bearer ", "");
            client.SetBearerToken(token);
            var authorityPars = authority.Split('/');
            var getUserInfoPost = await client.PostAsync($"{authorityPars[0]}//{authorityPars[2]}/connect/userinfo", null);
            var userInfoJson = await getUserInfoPost.Content.ReadAsStringAsync();
            return Ok(new { Claims = claims.Select(x => Tuple.Create(x.Type, x.Value)), UserInfo = userInfoJson });
        }
    }
}
