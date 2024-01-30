using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sylas.RemoteTasks.Utils;
using System.Text;
using System.Text.RegularExpressions;

namespace Sylas.RemoteTasks.App.Infrastructure;

public class DotNETOperation
{
    private readonly string OneTabSpace = "    ";
    private readonly string TwoTabsSpace = "        ";
    /// <summary>
    /// 单词变成单数形式
    /// </summary>
    /// <param name="word"></param>
    /// <returns></returns>
    public string ToSingular(string word)
    {
        Regex plural1 = new("(?<keep>[^aeiou])ies$");
        Regex plural2 = new("(?<keep>[aeiou]y)s$");
        Regex plural3 = new("(?<keep>[sxzh])es$");
        Regex plural4 = new("(?<keep>[^sxzhyu])s$");

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
            Regex plural1 = new("(?<keep>[^aeiou])ies$");
            Regex plural2 = new("(?<keep>[aeiou]y)s$");
            Regex plural3 = new("(?<keep>[sxzh])es$");
            Regex plural4 = new("(?<keep>[^sxzhyu])s$");

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
        Regex plural1 = new("(?<keep>[^aeiou])y$");
        Regex plural2 = new("(?<keep>[aeiou]y)$");
        Regex plural3 = new("(?<keep>[sxzh])$");
        Regex plural4 = new("(?<keep>[^sxzhy])$");

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
    /// <summary>
    /// 返回自定义DbContext中定义两个表之间的关联关系的代码
    /// </summary>
    /// <param name="type"></param>
    /// <param name="foreignKeyTableEntityName">外键表对应的实体名称</param>
    /// <param name="primaryKeyTableEntityName">主键表对应的实体名称</param>
    /// <param name="foreignKey"></param>
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
                .WithMany(x => x.{ToPlural(foreignKeyTableEntityName)})
                .HasForeignKey(x => x.{foreignKey}) // 外键表中的外键
                .OnDelete(DeleteBehavior.Restrict); // 不允许级联删除";
        }
        else
        {
            return "";
        }
    }

    #region 修改/验证代码的操作
    // Roslyn

    // CompilationUnit 
    //  UsingDirective
    //  UsingDirective
    //  NamespaceDeclaration
    //  EndOfFileToken

    // DotToken -> . | SemicolonToken -> , | OpenBraceToken -> { | CloseBraceToken -> } | OpenParenToken -> ( | CloseParenToken: )
    // EndOfLineTrivia 表示换行, WhitespaceTrivia 表示空格, EndOfFileToken 表示文件的末尾
    // EqualsValueClause 赋值语句
    // LocalDeclarationStatement 本地声明语句
    // ExpressionStatement 表达式语句

    // EqualsExpression 相等判断表达式，即 a == b。
    // InvocationExpression  调用表达式, 即 Class.Method(xxx) 或 instance.Method
    // SimpleMemberAccessExpression 是方法调用除去参数列表的部分，即 Class.Method 或 instance.Method
    // ParenthesizedLambdaExpression 带括号的 lambda 表达式 如: (a) => xxx
    // SimpleLambdaExpression 不带括号的 lambda 表达式 如: a => xxx

    public void InsertTwoTablesRelationshipCode(string customDbContextFile, string codes)
    {
        FileHelper.InsertContent(customDbContextFile, originCode =>
        {
            SyntaxTree tree = CSharpSyntaxTree.ParseText(originCode);
            SyntaxNode root = tree.GetRoot();
            MethodDeclarationSyntax method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m => m.Identifier.ValueText == "OnModelCreating").First();
            var mbody = method.Body ?? throw new Exception("Method Body异常");
            SyntaxList<StatementSyntax> methodStatements = mbody.Statements;
            string insertPosition = methodStatements.Where(statement => statement.ToString().Contains(".HasOne") || statement.ToString().Contains(".WithMany")).LastOrDefault()?.ToString() ?? "";
            if (string.IsNullOrEmpty(insertPosition))
            {
                insertPosition = methodStatements.LastOrDefault()?.ToString() ?? "";
            }

            string newCodes = insertPosition + Environment.NewLine + OneTabSpace + TwoTabsSpace + codes;
            return originCode.Replace(insertPosition, newCodes);
        });
    }
    #endregion


}
