using IdentityServer4.AccessTokenValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sylas.RemoteTasks.App.Infrastructure;
using Sylas.RemoteTasks.App.RegexExp;
using Sylas.RemoteTasks.App.Repositories;
using Sylas.RemoteTasks.Database.SyncBase;
using Sylas.RemoteTasks.Utils;
using Sylas.RemoteTasks.Utils.Dto;
using System.Text.RegularExpressions;

namespace Sylas.RemoteTasks.App.Controllers
{
    public partial class ProjectController(DbConnectionInfoRepository dbConnectionInfoRepository, IConfiguration configuration) : Controller
    {
        private readonly DbConnectionInfoRepository _dbConnectionInfoRepository = dbConnectionInfoRepository;
        private readonly IConfiguration _configuration = configuration;

        public async Task<IActionResult> Index()
        {
            var connectionInfos = (await _dbConnectionInfoRepository.GetPageAsync(1, 10000, "Name", true, new DataFilter())).Data;
            List<DbConnectionDetial> connectionDetails = [];
            foreach (var connectionInfo in connectionInfos)
            {
                var connectionDetail = DatabaseInfo.GetDbConnectionDetial(connectionInfo.ConnectionString);
                if (!connectionDetails.Any(x => x.Host == connectionDetail.Host && x.DatabaseType == connectionDetail.DatabaseType))
                {
                    connectionDetails.Add(connectionDetail);
                }
            }
            ViewBag.ConnectionDetails = connectionDetails;
            return View();
        }

        public async Task<IActionResult> GeneratProjectAsync()
        {
            string BaseDir = @"D:/.NET/my/is4/Skoruba.IdentityServer4";
            string CompanyName = "";
            string ProjectName = "BlogDemo";
            GeneratorContext generatorContext = new(BaseDir, CompanyName, ProjectName);
            string generatingInfo = await generatorContext.InitialProjectAsync();
            return Content(generatingInfo);
            //return Json(new JsonResultModel { Code = 1, Data = null, Message = generatingInfo });
        }




        public async Task<IActionResult> ChangeConnectionString(string slnDir, string host, DatabaseType databaseType)
        {
            if (string.IsNullOrWhiteSpace(slnDir))
            {
                return Ok(new OperationResult(false, "解决方案/项目目录不能为空"));
            }
            if (string.IsNullOrWhiteSpace(host))
            {
                return Ok(new OperationResult(false, "数据库地址不能为空"));
            }
            var connectionInfosPage = await _dbConnectionInfoRepository.GetPageAsync(1, 10000, "Name", true, new DataFilter());
            var connectionInfos = connectionInfosPage.Data;
            var regex = databaseType switch
            {
                DatabaseType.MySql => RegexConst.ConnectionStringMySql,
                DatabaseType.SqlServer => RegexConst.ConnectionStringSqlServer,
                DatabaseType.Oracle => RegexConst.ConnectionStringOracle,
                DatabaseType.Sqlite => RegexConst.ConnectionStringSqlite,
                DatabaseType.Dm => RegexConst.ConnectionStringDm,
                _ => null,
            };
            if (regex is null)
            {
                return Ok(new OperationResult(false, $"未知的数据库类型: {databaseType}"));
            }

            connectionInfos = connectionInfos.Where(x => regex.IsMatch(x.ConnectionString));

            var appsettingsList = FileHelper.FindSourceFiles(slnDir, SourceFileType.Appsettings);
            foreach (var appsettingsFile in appsettingsList)
            {
                string appsettingsContent = System.IO.File.ReadAllText(appsettingsFile);

                #region 获取所有的数据库连接字符串
                foreach (var regexGetter in RegexConst.AllConnectionStringPatterns)
                {
                    var connectionStringLineRegex = new Regex($"(?<=\\n)(?<indent>\\s*)\"(?<connectionStringName>\\w+)\":\\s*\"(?<connectionString>{regexGetter})\".*", RegexOptions.IgnoreCase);
                    var matches = connectionStringLineRegex.Matches(appsettingsContent);
                    foreach (Match match in matches.Cast<Match>())
                    {
                        if (match.Success)
                        {
                            // 匹配一整行, 包括末尾换行符
                            var matchedConnectionStringLine = match.Value; // .TrimEnd('\r', '\n', ',', '"')

                            // 根据数据库找到对应要切换的数据库
                            var dbName = RegexConst.ConnectionStringDbName.Match(matchedConnectionStringLine).Groups["database"].Value;
                            var newConnInfo = connectionInfos.FirstOrDefault(x => x.ConnectionString.Contains(dbName, StringComparison.OrdinalIgnoreCase) || x.Alias.Contains(dbName, StringComparison.OrdinalIgnoreCase));
                            if (newConnInfo is not null)
                            {
                                string connectionString = match.Groups["connectionString"].Value;
                                string indent = match.Groups["indent"].Value;
                                string connectionStringName = match.Groups["connectionStringName"].Value;
                                string newConnectionStringLine = matchedConnectionStringLine.Replace(connectionString, newConnInfo.ConnectionString); //$"{indent}\"{connectionStringName}\": \"{newConnInfo.ConnectionString}\",";

                                // 添加注释
                                string newLine = matchedConnectionStringLine.Replace($@"""{connectionStringName}", $@"//""{connectionStringName}");
                                // 添加新的数据库连接字符串
                                newLine += newConnectionStringLine;
                                appsettingsContent = appsettingsContent.Replace(matchedConnectionStringLine, newLine);
                            }
                        }
                    }
                }
                #endregion

                await FileHelper.WriteAsync(appsettingsFile, appsettingsContent, false);
            }

            return Json(new OperationResult(true, ""));
        }

        [Authorize(AuthenticationSchemes = IdentityServerAuthenticationDefaults.AuthenticationScheme, Policy = "sfapi.2")]
        public IActionResult GetChildDirectories(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                var slnDirs = _configuration["SlnDirs"]?.Split(';', StringSplitOptions.RemoveEmptyEntries) ?? throw new Exception("没有找到配置SlnDirs");
                if (slnDirs.Length > 1)
                {
                    return Ok(new OperationResult(true, slnDirs));
                }
                path = slnDirs[0];
            }

            var result = new List<string> { path };
            result.AddRange(Directory.GetDirectories(path).Select(x => x.Replace('\\', '/')));
            return Ok(new OperationResult(true, result));
        }
        [Authorize(AuthenticationSchemes = IdentityServerAuthenticationDefaults.AuthenticationScheme, Policy = "sfapi.3")]
        public IActionResult TestApiScope3()
        {
            return Ok();
        }
    }
}
