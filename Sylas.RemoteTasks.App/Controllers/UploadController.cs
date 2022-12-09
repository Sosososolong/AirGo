using Microsoft.AspNetCore.Mvc;
using System.Net.Http;

namespace Sylas.RemoteTasks.App.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UploadController : Controller
    {
        private const string _boundary = "EAD567A8E8524B2FAC2E0628ABB6DF6E";
        private const string _uploadClient = "upload_client";
        private readonly IConfiguration _configuration;

        public UploadController(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        [HttpGet("Upload")]
        public async Task<IActionResult> UploadSend([FromServices] IHttpClientFactory httpClientFactory)
        {
            #region 设置请求的媒体类型, 这里模拟表单POST发送文件所以用的是MultipartFormDataContent, POST方式还可以用JsonContent
            var requestContent = new MultipartFormDataContent(_boundary);
            // 手动设置Content-Type： multipart/form-data; boundary=xxx, 上面已经指定了媒体类型和边界值, 这里就不需要了
            //requestContent.Headers.Remove("Content-Type");
            //requestContent.Headers.TryAddWithoutValidation("Content-Type", $"multipart/form-data; boundary={_boundary}");

            // 请求的几种媒体类型
            // 1. MultipartFormDataContent: body中内容字段之间用分隔符(如这里_boundary)分隔开
            // 2. FormUrlEncodedContent: body中的内容是name=zhagnsan&age=24
            // 3. JsonContent: body中的内容是{"name":"zhangsan","age":24}
            #endregion

            var dir = _configuration["Upload:ClientDir"];
            if (dir is null)
            {
                throw new Exception("没有配置要上传的目录 Upload:ClientDir");
            }
            var files = Directory.GetFiles(dir);
            foreach (var file in files)
            {
                var fileContent = await System.IO.File.ReadAllBytesAsync(file);
                var byteArrayContent = new ByteArrayContent(fileContent);
                //byteArrayContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");

                // 向MultipartFormDataContent插入准备好的文件表单域值， 注意MultipartFormDataContent是一个集合类型
                // "files"就是接口UploadReceive的参数名, 要一致才能对应上才能接收到参数
                var slashIndex = file.LastIndexOf('/');
                var backSlashIndex = file.LastIndexOf('\\');
                var index = slashIndex > backSlashIndex ? slashIndex : backSlashIndex;
                var fileName = file.Substring(index + 1);
                requestContent.Add(byteArrayContent, "files", fileName);
            }
            var httpClient = httpClientFactory.CreateClient(_uploadClient);
            // 给请求添加Authorization请求头
            //httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "xxxtokenvalue");
            var postResponse = await httpClient.PostAsync($"{_configuration["Upload:Host"]}/api/upload/receive", requestContent);
            return Ok();
        }

        [HttpPost("receive")]
        public async Task<IActionResult> UploadReceive(IFormFileCollection files)
        {
            // 也可以从表单中获取文件: var files = Request.Form.Files;
            foreach (var file in files)
            {
                using MemoryStream memoryStream = new();
                file.CopyTo(memoryStream);
                var bytes = memoryStream.ToArray();

                var serverSaveDir = _configuration["Upload:SaveDir"];
                if (serverSaveDir is null)
                {
                    throw new Exception("服务端没有配置保存文件的位置 Upload:SaveDir");
                }
                //创建本地文件写入流
                var filePath = Path.Combine(serverSaveDir, file.FileName);
                using FileStream fileStream = new(filePath, FileMode.Create);
                byte[] bArr = new byte[1024];
                memoryStream.Seek(0, SeekOrigin.Begin);
                int size = 0;
                while ((size = memoryStream.Read(bArr, 0, bArr.Length)) > 0)
                {
                    fileStream.Write(bArr, 0, size);
                }
            }

            await Task.CompletedTask;
            return Ok();
        }
    }
}
