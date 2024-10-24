using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sylas.RemoteTasks.Database.SyncBase;
using Xunit.Abstractions;

namespace Sylas.RemoteTasks.Test.Remote
{
    public class DataCompareTest(ITestOutputHelper outputHelper, TestFixture fixture) : IClassFixture<TestFixture>
    {
        private readonly ITestOutputHelper _outputHelper = outputHelper;
        private readonly DatabaseInfo _databaseInfo = fixture.ServiceProvider.GetRequiredService<DatabaseInfo>();
        private readonly IConfiguration _configuration = fixture.ServiceProvider.GetRequiredService<IConfiguration>();

        /// <summary>
        /// 数据库 数据脱敏
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task CompareDataTest()
        {
            #region 准备测试数据
            var prepareStart = DateTime.Now;
            List<dynamic> source = [];
            List<dynamic> target = [];
            dynamic? sourceFirst = null;
            dynamic? sourceLast = null;
            int sourceCount = 50000;
            int targetCount = 1000;
            for (int i = 0; i < sourceCount; i++)
            {
                var id = new Random().Next(100000000, 999999999);
                var current = new
                {
                    Id = id,
                    UserName = id,
                    NormalizedUserName = id,
                    Email = $"{id}@xxx.cn",
                    NormalizedEmail = $"{id}@XXX.CN",
                    EmailConfirmed = 1,
                    PasswordHash = "AQAAAAEAACcQAAAAEKjse4NTGr/9hAGWqxgyeH2urhZ7XEUzIperQ2o0+7vbFL3L5H2rTjfkyUHQ2M2niQ==",
                    SecurityStamp = "88cdb6ae-7d4a-4537-8a07-f862c5355481",
                    PhoneNumber = $"1375862{new Random().Next(1000, 9999)}",
                    PhoneNumberConfirmed = 1,
                    TwoFactorEnabled = 0,
                    LockoutEnd = 0,
                    LockoutEnabled = 1,
                    AccessFailedCount = 0,
                    UserId = 0,
                    NickName = "陈**",
                    EnName = 0,
                    Lang = "zh-cn",
                    Avatar = 0,
                    OrderNo = 10,
                    ADName = "ZGUC",
                    CreatedTime = "2023-08-02 10:00:00",
                    LastUpDateTime = "2023-08-10 10:00:00",
                    LastUpDateUserId = 0,
                    IsAsync = 1,
                    IsDisabled = 0,
                    UserSpell = "CY",
                    ConcurrencyStamp = "933e12bb-0af2-4091-b8e0-13011dd8f4f8"
                };
                source.Add(current);
                if (i == 0)
                {
                    sourceFirst = current;
                }
                if (i == sourceCount - 1)
                {
                    sourceLast = current;
                }
            }
            for (int i = 0; i < targetCount - 2; i++)
            {
                var id = new Random().Next(100000000, 999999999);
                target.Add(new
                {
                    Id = id,
                    UserName = id,
                    NormalizedUserName = id,
                    Email = $"{id}@xxx.cn",
                    NormalizedEmail = $"{id}@XXX.CN",
                    EmailConfirmed = 1,
                    PasswordHash = "AQAAAAEAACcQAAAAEKjse4NTGr/9hAGWqxgyeH2urhZ7XEUzIperQ2o0+7vbFL3L5H2rTjfkyUHQ2M2niQ==",
                    SecurityStamp = "88cdb6ae-7d4a-4537-8a07-f862c5355481",
                    PhoneNumber = $"1375862{new Random().Next(1000, 9999)}",
                    PhoneNumberConfirmed = 1,
                    TwoFactorEnabled = 0,
                    LockoutEnd = 0,
                    LockoutEnabled = 1,
                    AccessFailedCount = 0,
                    UserId = 0,
                    NickName = "陈**",
                    EnName = 0,
                    Lang = "zh-cn",
                    Avatar = 0,
                    OrderNo = 10,
                    ADName = "ZGUC",
                    CreatedTime = "2023-08-02 10:00:00",
                    LastUpDateTime = "2023-08-10 10:00:00",
                    LastUpDateUserId = 0,
                    IsAsync = 1,
                    IsDisabled = 0,
                    UserSpell = "CY",
                    ConcurrencyStamp = "933e12bb-0af2-4091-b8e0-13011dd8f4f8"
                });
            }
            if (sourceFirst is null || sourceLast is null)
            {
                throw new Exception($"{nameof(sourceFirst)}和{nameof(sourceLast)}不能为空");
            }
            // sourceFirst和sourceLast是只读的, 所以我重新new新对象重新赋值
            sourceFirst = new
            {
                sourceFirst.Id,
                UserName = sourceFirst.Id,
                NormalizedUserName = sourceFirst.Id,
                Email = $"{sourceFirst.Id}@xxx1111.cn",
                NormalizedEmail = $"{sourceFirst.Id} @xxx1111.cn",
                EmailConfirmed = 1,
                PasswordHash = "AQAAAAEAACcQAAAAEKjse4NTGr/9hAGWqxgyeH2urhZ7XEUzIperQ2o0+7vbFL3L5H2rTjfkyUHQ2M2niQ==",
                SecurityStamp = "88cdb6ae-7d4a-4537-8a07-f862c5355481",
                PhoneNumber = $"1375862{new Random().Next(1000, 9999)}",
                PhoneNumberConfirmed = 1,
                TwoFactorEnabled = 0,
                LockoutEnd = 0,
                LockoutEnabled = 1,
                AccessFailedCount = 0,
                UserId = 0,
                NickName = "陈**",
                EnName = 0,
                Lang = "zh-cn",
                Avatar = 0,
                OrderNo = 10,
                ADName = "ZGUC",
                CreatedTime = "2023-08-02 10:00:00",
                LastUpDateTime = "2023-08-10 10:00:00",
                LastUpDateUserId = 0,
                IsAsync = 1,
                IsDisabled = 0,
                UserSpell = "CY",
                ConcurrencyStamp = "933e12bb-0af2-4091-b8e0-13011dd8f4f8"
            };
            sourceLast = new
            {
                sourceLast.Id,
                UserName = sourceLast.Id,
                NormalizedUserName = sourceLast.Id,
                Email = $"{sourceLast.Id}@xxx1111.cn",
                NormalizedEmail = $"{sourceLast.Id} @xxx1111.cn",
                EmailConfirmed = 1,
                PasswordHash = "AQAAAAEAACcQAAAAEKjse4NTGr/9hAGWqxgyeH2urhZ7XEUzIperQ2o0+7vbFL3L5H2rTjfkyUHQ2M2niQ==",
                SecurityStamp = "88cdb6ae-7d4a-4537-8a07-f862c5355481",
                PhoneNumber = $"1375862{new Random().Next(1000, 9999)}",
                PhoneNumberConfirmed = 1,
                TwoFactorEnabled = 0,
                LockoutEnd = 0,
                LockoutEnabled = 1,
                AccessFailedCount = 0,
                UserId = 0,
                NickName = "陈**",
                EnName = 0,
                Lang = "zh-cn",
                Avatar = 0,
                OrderNo = 10,
                ADName = "ZGUC",
                CreatedTime = "2023-08-02 10:00:00",
                LastUpDateTime = "2023-08-10 10:00:00",
                LastUpDateUserId = 0,
                IsAsync = 1,
                IsDisabled = 0,
                UserSpell = "CY",
                ConcurrencyStamp = "933e12bb-0af2-4091-b8e0-13011dd8f4f8"
            };
            target.Add(sourceFirst);
            target.Add(sourceLast);
            var prepareEnd = DateTime.Now;
            _outputHelper.WriteLine($"准备数据完毕 {(prepareEnd - prepareStart).TotalSeconds}/s");
            #endregion

            var before = DateTime.Now;
            var result = await DatabaseInfo.CompareRecordsAsync(source, target, [], "Id");
            var end = DateTime.Now;
            // 老版本使用NewtonSoft的IEnumerable<JObject>: 20s
            // 新版本使用IEnumerable<IDictionary<string, obejct>>: 2s, 内存占用减少近一半
            _outputHelper.WriteLine("对比数据完毕" + (end - before).TotalSeconds.ToString() + "/s");
            _outputHelper.WriteLine($"ExistInSourceOnly:{result.ExistInSourceOnly.Count}; ExistInTargetOnly:{result.ExistInTargetOnly.Count}; Changed:{result.Intersection.Count}"); // SouceOnly:50000 TargetOnly:1000 Changed:0
        }
    }
}
