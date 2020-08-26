using System;
using System.Collections.Generic;
using System.Text;

namespace DemonFox.Tails.Infrastructure
{
    public class AspNetCoreOperations
    {
        public string GetMyDbContextContent(string dbContextNamespace)
        {
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
        
        public DbSet<Company> Companies {{ get; set; }}
        public DbSet<Employee> Employees {{ get; set; }}        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {{
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Company>()
                .Property(x => x.Name)
                .IsRequired().HasMaxLength(100);

            modelBuilder.Entity<Company>()
                .Property(x => x.Country).HasMaxLength(50);

            modelBuilder.Entity<Company>()
                .Property(x => x.Industry).HasMaxLength(50);

            modelBuilder.Entity<Company>()
                .Property(x => x.Product).HasMaxLength(100);

            modelBuilder.Entity<Company>()
                .Property(x => x.Introduction)
                .HasMaxLength(500);

            modelBuilder.Entity<Employee>()
                .Property(x => x.EmployeeNo)
                .IsRequired()
                .HasMaxLength(10);

            modelBuilder.Entity<Employee>()
                .Property(x => x.FirstName)
                .IsRequired()
                .HasMaxLength(50);

            modelBuilder.Entity<Employee>()
                .Property(x => x.LastName)
                .IsRequired()
                .HasMaxLength(50);

            modelBuilder.Entity<Employee>()
                .HasOne(x => x.Company)
                .WithMany(x => x.Employees)
                .HasForeignKey(x => x.CompanyId)
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
    }
}
