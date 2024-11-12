using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sylas.RemoteTasks.Database.SyncBase
{
    /// <summary>
    /// 过滤条件组 - SQL查询条件描述
    /// </summary>
    public class FilterGroup
    {
        /// <summary>
        /// 使用默认值初始化对象
        /// </summary>
        public FilterGroup() { }
        /// <summary>
        /// 使用指定参数初始化对象
        /// </summary>
        /// <param name="filterItems"></param>
        /// <param name="sqlLogic"></param>
        public FilterGroup(IEnumerable<object> filterItems, SqlLogic sqlLogic)
        {
            FilterItems = filterItems;
            FilterItemsLogicType = sqlLogic;
        }
        /// <summary>
        /// 多个条件
        /// </summary>
        public IEnumerable<object> FilterItems { get; set; } = [];
        /// <summary>
        /// 多个条件之间的逻辑关系
        /// </summary>
        public SqlLogic FilterItemsLogicType { get; set; } = SqlLogic.And;
        /// <summary>
        /// 添加关键字查询条件
        /// </summary>
        /// <param name="fields"></param>
        /// <param name="includeValue"></param>
        /// <returns></returns>

        public FilterGroup AddKeywordsQuerying(IEnumerable<string> fields, string includeValue)
        {
            if (fields is not null && fields.Any() && !string.IsNullOrWhiteSpace(includeValue))
            {
                var filterItems = fields.Select(item => new FilterItem(item, CompareTypeConsts.Include, includeValue));
                var keywordFilterGroup = new FilterGroup(filterItems.Cast<object>().ToList(), SqlLogic.Or);

                FilterGroup newFilterGroup = new()
                {
                    FilterItems = [this, keywordFilterGroup],
                    FilterItemsLogicType = SqlLogic.And
                };

                return newFilterGroup;
            }
            return this;
        }
        /// <summary>
        /// 构建查询条件
        /// </summary>
        /// <param name="dataBaseType"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public SqlInfo BuildConditions(DatabaseType dataBaseType = DatabaseType.MySql)
        {
            string varFlag = new DatabaseType[] { DatabaseType.Oracle, DatabaseType.Dm }.Contains(dataBaseType) ? ":" : "@";
            Dictionary<string, object> parameters = [];
            StringBuilder conditionBuilder = new();
            BuildConditionsRecursively(FilterItems, FilterItemsLogicType);
            string condition = conditionBuilder.ToString();
            return new SqlInfo(condition, parameters);

            void BuildConditionsRecursively(IEnumerable<object> filterItems, SqlLogic sqlLogic, SqlLogic parentSqlLogic = SqlLogic.None)
            {
                // 如果当前组是AND即"(a = 1 AND b = 2)", 并且上一级组也是AND, 即"[AND] (a = 1 AND b = 2) [AND]", 那么当前组的条件不需要括号, 即"[AND] a = 1 AND b = 2 [AND]";
                // parentSqlLogic为None, 表示没有上一层, 当前层为顶层, 顶级的组(最外一层where条件)不需要括号
                bool notNeedParentheses = (sqlLogic == SqlLogic.And && parentSqlLogic == SqlLogic.And) || parentSqlLogic == SqlLogic.None;
                if (!notNeedParentheses)
                {
                    conditionBuilder.Append('(');
                }

                int filterItemsCount = filterItems.Count();
                int i = -1;
                foreach (var item in filterItems)
                {
                    i++;
                    if (item is FilterItem filterItem)
                    {
                        // a = 1
                        conditionBuilder.Append(filterItem.BuildConditionStatement(varFlag, parameters));
                    }
                    else if (item is FilterGroup filterGroup)
                    {
                        // (a = 1 AND/OR b = 2)
                        BuildConditionsRecursively(filterGroup.FilterItems, filterGroup.FilterItemsLogicType, sqlLogic);
                    }
                    // object类型, Json反序列化过来将会是JObject类型
                    else if (item is JObject itemJOjb)
                    {
                        var itemJson = itemJOjb.ToString();
                        if (itemJson.Contains("filterItemsLogicType", StringComparison.OrdinalIgnoreCase))
                        {
                            filterGroup = JsonConvert.DeserializeObject<FilterGroup>(itemJson) ?? throw new Exception("条件对象FilterGroup类型的json字符串格式异常");
                            if (filterGroup is null)
                            {
                                Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} 条件json字符串无法转换为FilterGroup: {itemJson}");
                                throw new Exception("条件对象格式异常");
                            }
                            BuildConditionsRecursively(filterGroup.FilterItems, filterGroup.FilterItemsLogicType, sqlLogic);
                        }
                        else
                        {
                            filterItem = JsonConvert.DeserializeObject<FilterItem>(itemJson) ?? throw new Exception("条件对象FilterGroup类型的json字符串格式异常");
                            if (string.IsNullOrWhiteSpace(filterItem.FieldName))
                            {
                                continue;
                            }
                            if (filterItem is null)
                            {
                                Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} 条件json字符串无法转换为FilterItem: {itemJson}");
                                throw new Exception("条件对象格式异常");
                            }
                            conditionBuilder.Append(filterItem.BuildConditionStatement(varFlag, parameters));
                        }
                    }
                    else
                    {
                        throw new Exception("不支持的条件类型");
                    }
                    if (i < filterItemsCount - 1 && conditionBuilder.Length > 0)
                    {
                        conditionBuilder.Append($" {sqlLogic.ToString().ToUpper()} ");
                    }
                }
                if (!notNeedParentheses)
                {
                    conditionBuilder.Append(')');
                }
            }
        }
        /// <summary>
        /// 重写ToString方法, 用于生成Left Join的ON条件语句
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            StringBuilder statementBuilder = new();
            GetConditionStatementsRecursively(FilterItems, FilterItemsLogicType, SqlLogic.None);
            return statementBuilder.ToString();

            void GetConditionStatementsRecursively(IEnumerable<object> filterItems, SqlLogic sqlLogic, SqlLogic parentSqlLogic)
            {
                bool notNeedParentheses = (sqlLogic == SqlLogic.And && parentSqlLogic == SqlLogic.And) || parentSqlLogic == SqlLogic.None;
                if (!notNeedParentheses)
                {
                    statementBuilder.Append('(');
                }
                int filterItemsCount = filterItems.Count();
                int i = -1;
                foreach (var item in filterItems)
                {
                    i++;
                    if (item is FilterItem filterItem)
                    {
                        statementBuilder.Append(filterItem.ToString());
                    }
                    else if (item is FilterGroup filterGroup)
                    {
                        statementBuilder.Append(filterGroup.ToString());
                    }
                    else if (item is JObject itemJObj)
                    {
                        string itemJson = itemJObj.ToString();
                        if (itemJson.Contains("filterItemsLogicType", StringComparison.OrdinalIgnoreCase))
                        {
                            filterGroup = JsonConvert.DeserializeObject<FilterGroup>(itemJson) ?? throw new Exception("条件对象FilterGroup类型的json字符串格式异常");
                            statementBuilder.Append(filterGroup.ToString());
                        }
                        else
                        {
                            filterItem = JsonConvert.DeserializeObject<FilterItem>(itemJson) ?? throw new Exception("条件对象FilterGroup类型的json字符串格式异常");
                            statementBuilder.Append(filterItem.ToString());
                        }
                    }
                    if (i < filterItemsCount - 1)
                    {
                        statementBuilder.Append($" {sqlLogic} ");
                    }
                }
                if (!notNeedParentheses)
                {
                    statementBuilder.Append(')');
                }
            }
        }
    }
}
