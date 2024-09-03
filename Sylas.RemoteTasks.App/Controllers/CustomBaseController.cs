using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Sylas.RemoteTasks.App.Infrastructure;
using Sylas.RemoteTasks.App.Study;
using Sylas.RemoteTasks.Utils.Constants;
using Sylas.RemoteTasks.Utils.Dto;
using System.Text;

namespace Sylas.RemoteTasks.App.Controllers
{
    [Authorize(Policy = AuthorizationConstants.AdministrationPolicy)]
    [ServiceFilter<MvcParameterFilter>]

    public class CustomBaseController : Controller
    {
        protected async Task<OperationResult> SaveUploadedFilesAsync(IWebHostEnvironment env)
        {
            IFormFileCollection files = Request.Form.Files;
            if (files.Count == 0)
            {
                return new OperationResult(false);
            }

            StringBuilder imgPathBuilder = new();
            foreach (var file in files)
            {
                using MemoryStream memoryStream = new();
                file.CopyTo(memoryStream);
                var bytes = memoryStream.ToArray();

                var filePathInfo = GetFilePathInfo(env, file.FileName);
                (string fileAbsolutePath, string relativePath) = filePathInfo;
                imgPathBuilder.Append(relativePath);

                //创建本地文件写入流
                using FileStream fileStream = new(fileAbsolutePath, FileMode.Create);
                byte[] bArr = new byte[1024];
                memoryStream.Seek(0, SeekOrigin.Begin);
                int size = 0;
                while ((size = await memoryStream.ReadAsync(bArr)) > 0)
                {
                    await fileStream.WriteAsync(bArr.AsMemory(0, size));
                }
            }
            return new OperationResult(true, [imgPathBuilder.ToString().TrimEnd(';')]);
        }

        /// <summary>
        /// 删除静态文件
        /// </summary>
        /// <param name="env">Web服务环境对象</param>
        /// <param name="files">需要删除的文件的相对路径集合</param>
        protected void DeleteStaticFiles(IWebHostEnvironment env, IEnumerable<string> files)
        {
            if (files is null || !files.Any())
            {
                return;
            }

            foreach (var f in files)
            {
                if (string.IsNullOrWhiteSpace(f))
                {
                    continue;
                }

                string file = Path.Combine(env.WebRootPath, f);
                if (System.IO.File.Exists(file))
                {
                    System.IO.File.Delete(file);
                }
            }
        }

        /// <summary>
        /// 上传文件, 返回最终的文件列表
        /// </summary>
        /// <param name="originFiles">原始的文件列表</param>
        /// <param name="currentFiles">执行删除操作之后的文件列表</param>
        /// <param name="env"></param>
        /// <returns></returns>

        protected async Task<List<string>> HandleUploadedFilesAsync(List<string> originFiles, List<string> currentFiles, IWebHostEnvironment env)
        {
            #region 检查图片是否被删除
            //List<string> recordImageUrls = string.IsNullOrWhiteSpace(record.ImageUrl) ? [] : [.. record.ImageUrl.Split(';')];
            //List<string> imageUrls = string.IsNullOrWhiteSpace(question.ImageUrl) ? [] : [.. question.ImageUrl.Split(';')];
            var deletedImageUrls = originFiles.Except(currentFiles);
            DeleteStaticFiles(env, deletedImageUrls);
            #endregion

            #region 检查是否新增(上传)图片
            var operationResult = await SaveUploadedFilesAsync(env);
            if (operationResult.IsSuccess && operationResult.Data is not null)
            {
                currentFiles.AddRange(operationResult.Data.First().Split(';'));
            }
            #endregion

            return currentFiles;
        }

        /// <summary>
        /// 上传文件, 返回最终的文件列表
        /// </summary>
        /// <param name="originFiles">原始的文件列表</param>
        /// <param name="currentFiles">执行删除操作之后的文件列表</param>
        /// <param name="env"></param>
        /// <returns></returns>
        protected async Task<List<string>> HandleUploadedFilesAsync(string? originFiles, string? currentFiles, IWebHostEnvironment env)
        {
            List<string> originFileList = string.IsNullOrWhiteSpace(originFiles) ? [] : [.. originFiles.Split(';', StringSplitOptions.RemoveEmptyEntries)];
            List<string> currentFileList = string.IsNullOrWhiteSpace(currentFiles) ? [] : [.. currentFiles.Split(';', StringSplitOptions.RemoveEmptyEntries)];
            return await HandleUploadedFilesAsync(originFileList, currentFileList, env);
        }

        /// <summary>
        /// 获取文件的绝对路径和相对路径
        /// </summary>
        /// <param name="env"></param>
        /// <param name="filename"></param>
        /// <returns></returns>
        Tuple<string, string> GetFilePathInfo(IWebHostEnvironment env, string filename)
        {
            string controllerName = GetType().Name.Replace("Controller", string.Empty);
            string staticDirName = "Static";
            string moduleStaticDir = Path.Combine(env.WebRootPath, staticDirName, controllerName);
            if (!Directory.Exists(moduleStaticDir))
            {
                Directory.CreateDirectory(moduleStaticDir);
            }
            var filePath = Path.Combine(moduleStaticDir, filename);
            return Tuple.Create(filePath, $"{staticDirName}/{controllerName}/{filename};");
        }
    }
}
