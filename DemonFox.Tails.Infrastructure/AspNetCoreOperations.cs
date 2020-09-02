using DemonFox.Tails.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DemonFox.Tails.Infrastructure
{
    public class AspNetCoreOperations
    {
        private string OneTabSpace = "    ";
        private string TwoTabsSpace = "        ";
        private string FourTabsSpace = "                ";
        /// <summary>
        /// 单词变成单数形式
        /// </summary>
        /// <param name="word"></param>
        /// <returns></returns>
        public string ToSingular(string word)
        {
            Regex plural1 = new Regex("(?<keep>[^aeiou])ies$");
            Regex plural2 = new Regex("(?<keep>[aeiou]y)s$");
            Regex plural3 = new Regex("(?<keep>[sxzh])es$");
            Regex plural4 = new Regex("(?<keep>[^sxzhyu])s$");

            if (plural1.IsMatch(word))
                return plural1.Replace(word, "${keep}y");
            else if (plural2.IsMatch(word))
                return plural2.Replace(word, "${keep}");
            else if (plural3.IsMatch(word))
                return plural3.Replace(word, "${keep}");
            else if (plural4.IsMatch(word))
                return plural4.Replace(word, "${keep}");

            return word;
        }
        /// <summary>
        /// 将一个集合内的单词都转换为单数形式
        /// </summary>
        /// <param name="words"></param>
        /// <returns></returns>
        public List<string> ToSingulars(List<string> words)
        {
            return words.Select(word =>
            {
                Regex plural1 = new Regex("(?<keep>[^aeiou])ies$");
                Regex plural2 = new Regex("(?<keep>[aeiou]y)s$");
                Regex plural3 = new Regex("(?<keep>[sxzh])es$");
                Regex plural4 = new Regex("(?<keep>[^sxzhyu])s$");

                if (plural1.IsMatch(word))
                    return plural1.Replace(word, "${keep}y");
                else if (plural2.IsMatch(word))
                    return plural2.Replace(word, "${keep}");
                else if (plural3.IsMatch(word))
                    return plural3.Replace(word, "${keep}");
                else if (plural4.IsMatch(word))
                    return plural4.Replace(word, "${keep}");

                return word;
            }).ToList();            
        }
        /// <summary>
        /// 单词变成复数形式
        /// </summary>
        /// <param name="word"></param>
        /// <returns></returns>
        public string ToPlural(string word)
        {            
            Regex plural1 = new Regex("(?<keep>[^aeiou])y$");
            Regex plural2 = new Regex("(?<keep>[aeiou]y)$");
            Regex plural3 = new Regex("(?<keep>[sxzh])$");
            Regex plural4 = new Regex("(?<keep>[^sxzhy])$");            

            if (plural1.IsMatch(word))
                return plural1.Replace(word, "${keep}ies");
            else if (plural2.IsMatch(word))
                return plural2.Replace(word, "${keep}s");
            else if (plural3.IsMatch(word))
                return plural3.Replace(word, "${keep}es");
            else if (plural4.IsMatch(word))
                return plural4.Replace(word, "${keep}s");

            return word;
        }
        /// <summary>
        /// 获取初始化自定义MyDbContext类的代码字符串
        /// </summary>
        /// <param name="dbContextNamespace"></param>
        /// <param name="entityNames"></param>
        /// <returns></returns>
        public string GetMyDbContextContent(string dbContextNamespace, List<string> entityNames)
        {
            StringBuilder entitiySets = new StringBuilder();
            foreach (string e in entityNames)
            {
                entitiySets.Append($"public DbSet<{e}> {ToPlural(e)} {{ get; set; }}").Append(Environment.NewLine).Append(TwoTabsSpace);
            }
            string entitySetsCode = entitiySets.ToString().TrimEnd();
            //创建文件
            return $@"using Microsoft.EntityFrameworkCore;
using Routine.Entities;
using System;

namespace {dbContextNamespace}
{{
    public class MyDbContext : DbContext
    {{
        public MyDbContext(DbContextOptions<MyDbContext> options) : base(options)
        {{

        }}
        
        {entitySetsCode}      
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {{
            base.OnModelCreating(modelBuilder);            
            // 数据库的字段约束
            modelBuilder.Entity<Employee>()
                .Property(x => x.EmployeeNo)
                .IsRequired()
                .HasMaxLength(10);

            // 两个表之间""*对*""的关系表示
            modelBuilder.Entity<Employee>()
                .HasOne(x => x.Company) // *对一
                .WithMany(x => x.Employees) // *对多
                .HasForeignKey(x => x.CompanyId) // 外键表中的外键
                //.OnDelete(DeleteBehavior.Restrict); // 不允许级联删除
                .OnDelete(DeleteBehavior.Cascade); // 级联删除(删除父资源的时候, 子资源也会删除)

            
            // 种子数据
            modelBuilder.Entity<Company>().HasData(
                new Company
                {{
                    Id = Guid.Parse(""bbdee09c-089b-4d30-bece-44df5923716c""),
                    Name = ""Microsoft"",
                    Introduction = ""Great Company"",
                    Country = ""USA"",
                    Industry = ""Software"",
                    Product = ""Software""
                }});

            modelBuilder.Entity<Employee>().HasData(
                new Employee
                {{
                    Id = Guid.Parse(""4b501cb3-d168-4cc0-b375-48fb33f318a4""),
                    CompanyId = Guid.Parse(""bbdee09c-089b-4d30-bece-44df5923716c""),
                    DateOfBirth = new DateTime(1976, 1, 2),
                    EmployeeNo = ""MSFT231"",
                    FirstName = ""Nick"",
                    LastName = ""Carter"",
                    Gender = Gender.男
                }});
        }}
    }}
}}
";
        }

        #region 修改代码操作, 如果根项目结构相关就放这里, 如果够通用化可以考虑放到FileOp中
        public void InsertPropertyLevelCode(string classFile, string codes)
        {
            FileOp.InsertContent(classFile, originCode =>
            {
                if (originCode.Contains(codes))
                {
                    throw new Exception("要插入的代码已经存在");
                }
                // 找到最后一个属性
                Regex regex = new Regex(@"public\s+.+\s+\w+\s*{\s*get;\s*set;\s*}");
                Match lastProperty = regex.Matches(originCode).LastOrDefault();
                if (lastProperty != null)
                {
                    string lastPropertyString = lastProperty.Value; // "public DbSet<Employee> Employees { get; set; }"
                    string modifiedPropertyString = lastPropertyString + Environment.NewLine + TwoTabsSpace + codes;
                    return originCode.Replace(lastPropertyString, modifiedPropertyString); // 匹配的属性应该是独一无二的, 所以这里肯定只是替换一次
                }
                // 添加第一个属性
                regex = new Regex(@"\s{8}.+\(");
                // 找到文件中第一个方法的声明语句
                string firstMethodStatement = regex.Match(originCode).Value;
                if (string.IsNullOrEmpty(firstMethodStatement))
                {
                    regex = new Regex(@"\s{4}{");                    
                    
                    string classLeftBrackets = regex.Match(originCode).Value;
                    classLeftBrackets = classLeftBrackets + Environment.NewLine + TwoTabsSpace + codes;

                    return regex.Replace(originCode, classLeftBrackets, 1); // 只替换一次
                }
                string newCodeBeforeFirstMethodStatement = codes + firstMethodStatement;
                return originCode.Replace(firstMethodStatement, newCodeBeforeFirstMethodStatement);
            });
        }
        /// <summary>
        /// EF表示两个表之间的关联关系的代码
        /// </summary>
        /// <param name="type">"0"一对一, "1"一对多</param>
        /// <param name="twoTables"></param>
        /// <returns></returns>
        public string GetTwoTablesRelationshipCode(string type, string foreignKeyTableEntityName, string primaryKeyTableEntityName, string foreignKey)
        {
            if (type == "0") // 一对一
            {
                return $@"modelBuilder.Entity<{foreignKeyTableEntityName}>()
                .HasOne(x => x.{primaryKeyTableEntityName})
                .HasOne(x => x.{foreignKeyTableEntityName})
                .HasForeignKey(x => x.{foreignKey}) // 外键表中的外键
                .OnDelete(DeleteBehavior.Restrict); // 不允许级联删除";
            }
            else if (type == "1") // 一对多
            {
                return $@"modelBuilder.Entity<{foreignKeyTableEntityName}>()
                .HasOne(x => x.{primaryKeyTableEntityName})
                .WithMany(x => x.{foreignKeyTableEntityName})
                .HasForeignKey(x => x.{foreignKey}) // 外键表中的外键
                .OnDelete(DeleteBehavior.Restrict); // 不允许级联删除";
            }
            else
            {
                return "";
            }
        }
        #endregion

    }
}
