using Sylas.RemoteTasks.Database.SyncBase;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sylas.RemoteTasks.Database.CodeGenerator
{
    /// <summary>
    /// 代码生成器所需参数
    /// </summary>
    public class CodeGeneratorInDto
    {
        private string _serviceFieldInController = string.Empty;
        private string _controllerName = string.Empty;

        /// <summary>
        /// 标题
        /// </summary>
        public string Title { get; set; } = string.Empty;
        /// <summary>
        /// 模板Id
        /// </summary>
        public int TemplateId { get; set; }

        /// <summary>
        /// 主表信息
        /// </summary>
        public CurdTableInfo MainTable { get; set; } = new();

        /// <summary>
        /// 表代表的数据含义, 用于接口注释, 如"users"表的注释为"用户", 那么添加用户的接口注释为"添加用户", 查询用户的接口注释为"查询用户"
        /// </summary>
        public string TableComment { get; set; } = string.Empty;

        /// <summary>
        /// 接口/视图所在控制器
        /// </summary>
        public string ControllerName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_controllerName))
                {
                    return $"{TableModelName}Controller";
                }
                return _controllerName;
            }
            set
            {
                _controllerName = string.IsNullOrWhiteSpace(value) ? string.Empty : value;
            } 
        }
        /// <summary>
        /// 目标数据表的模型名称, 用于指定模型的类名
        /// </summary>
        public string TableModelName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(MainTable.Name))
                {
                    return string.Empty;
                }
                var modelName = MainTable.Name.EndsWith('s') ? MainTable.Name[..^1] : MainTable.Name;
                var modelNameArr = modelName.Split('_', StringSplitOptions.RemoveEmptyEntries);
                modelName = string.Join(string.Empty, modelNameArr.Select(x => $"{x[0].ToString().ToUpper()}{x[1..]}"));
                return modelName;
            }
        }

        /// <summary>
        /// 目标表相关对象的前缀, 比如"users"表仓储类的字段名为"_userRepository"
        /// </summary>
        public string TableRelatedObjectPrefix => string.IsNullOrWhiteSpace(TableModelName) ? string.Empty : $"_{TableModelName[0].ToString().ToLower()}{TableModelName[1..]}";

        /// <summary>
        /// 控制器中的业务字段名称
        /// </summary>
        public string ServiceFieldInController
        {
            get
            {
                return string.IsNullOrWhiteSpace(_serviceFieldInController) && !string.IsNullOrWhiteSpace(TableRelatedObjectPrefix) ? $"{TableRelatedObjectPrefix}Service" : _serviceFieldInController;
            }
            set
            {
                _serviceFieldInController = string.IsNullOrWhiteSpace(value) ? string.Empty : value;
            } 
        }
        /// <summary>
        /// 关联表
        /// </summary>
        public List<LeftJoinTable> LeftJoinTables { get; set; } = [];
        /// <summary>
        /// 可能需要用作数据查询过滤条件的字段
        /// </summary>
        public List<ShowingColumn> SearchColumns
        {
            get
            {
                var result = MainTable.ShowingColumns.Where(x => !string.IsNullOrWhiteSpace(x?.ColumnCode) && (x.ColumnCode.Contains("name", StringComparison.OrdinalIgnoreCase) || x.ColumnCode.Equals("id", StringComparison.CurrentCultureIgnoreCase))).ToList();
                var relatedNameColumns = LeftJoinTables.SelectMany(x => x.ShowingColumns).Where(x => !string.IsNullOrWhiteSpace(x?.ColumnCode) && x.ColumnCode.Contains("name", StringComparison.OrdinalIgnoreCase)).ToList();
                result.AddRange(relatedNameColumns);
                return result;
            }
        }

        /// <summary>
        /// 分组字段
        /// </summary>
        public string GroupBy { get; set; } = string.Empty;

        /// <summary>
        /// 排序语句
        /// </summary>
        public string Order { get; set; } = string.Empty;

        /// <summary>
        /// 需要格式化的整型字段
        /// </summary>
        public List<string> NumberFormatFields { get; set; } = [];

        /// <summary>
        /// 查询字段表达式
        /// </summary>
        public string SelectFields
        {
            get
            {
                var fields = MainTable.ShowingColumns.Select(x => $"{MainTable.Alias}.{x.ColumnCode}").ToList();
                foreach (var item in LeftJoinTables)
                {
                     var itemFields = item.ShowingColumns.Select(x => $"{item.Alias}.{x.ColumnCode}").ToList();
                    fields.AddRange(itemFields);
                }
                return string.Join(',', fields);
            }
        }

        /// <summary>
        /// 是否启用时间过滤
        /// </summary>
        public bool EnableDatetimeFilter => MainTable.ShowingColumns.Any(x => x.ColumnCode.Contains("time", StringComparison.OrdinalIgnoreCase));
        /// <summary>
        /// 是否启用用户ID过滤
        /// </summary>
        public bool EnableUserIdFilter => MainTable.ShowingColumns.Any(x => x.ColumnCode.Contains("userid", StringComparison.OrdinalIgnoreCase));
    }
}
