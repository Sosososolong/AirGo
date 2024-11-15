using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Sylas.RemoteTasks.Database.SyncBase
{
    /// <summary>
    /// 描述一个过滤条件
    /// </summary>
    public class FilterItem
    {
        /// <summary>
        /// 默认值初始化实例
        /// </summary>
        public FilterItem() { }
        /// <summary>
        /// 指定属性值初始化实例
        /// </summary>
        /// <param name="field"></param>
        /// <param name="compareType"></param>
        /// <param name="value"></param>
        public FilterItem(string field, string compareType, object value)
        {
            FieldName = field;
            CompareType = compareType;
            Value = value;
        }
        /// <summary>
        /// 字段名称
        /// </summary>
        public string FieldName { get; set; } = string.Empty;
        /// <summary>
        /// 比较类型 大于, 小于, =, 大于等于, 小于等于, !=, in, include
        /// </summary>
        public string CompareType { get; set; } = string.Empty;

        object? _value = null;
        /// <summary>
        /// 比较的值
        /// </summary>
        public object? Value
        {
            get
            {
                return _value;
            }
            set
            {
                if (value is JsonElement jeVal)
                {
                    if (jeVal.ValueKind == JsonValueKind.True)
                    {
                        _value = true;
                    }
                    else if (jeVal.ValueKind == JsonValueKind.False)
                    {
                        _value = false;
                    }
                    else if (jeVal.ValueKind == JsonValueKind.Number)
                    {
                        _value = FieldName.Equals("id", StringComparison.OrdinalIgnoreCase) ? jeVal.GetInt64() : jeVal.GetInt32();
                    }
                    else if (jeVal.ValueKind == JsonValueKind.Null || jeVal.ValueKind == JsonValueKind.Undefined)
                    {
                        _value = null;
                    }
                    else
                    {
                        _value = jeVal.GetString() ?? string.Empty;
                    }
                }
                else
                {
                    _value = value;
                }
            }
        }
        /// <summary>
        /// 构建SQL语句的条件语句
        /// </summary>
        /// <param name="varFlag"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public string BuildConditionStatement(string varFlag, Dictionary<string, object?> parameters)
        {
            if (Value is null)
            {
                return string.Empty;
            }
            string valueStr = string.Empty;
            if (Value is string)
            {
                valueStr = $"{Value}";
            }

            // 如果Value是动态参数, 如{name}, {age}, ...等, 那么就需要将其转换为参数形式(参数值为null), 并且将参数添加到parameters集合中
            if (valueStr.StartsWith('{') && valueStr.EndsWith('}'))
            {
                string valuePlaceholderName = valueStr.Trim('{', '}');
                string parameterName = Regex.IsMatch(valuePlaceholderName, @"\d+") ? $"p{valuePlaceholderName}" : valuePlaceholderName;

                if (parameters is not null && !parameters.ContainsKey(parameterName))
                {
                    parameters[parameterName] = "{{" + parameterName + "}}";
                }
                string conditionStatement = CompareType == CompareTypeConsts.Include
                    ? $"{FieldName} LIKE CONCAT(CONCAT('%', {varFlag}{parameterName}), '%')"
                    : $"{FieldName} {CompareType} {varFlag}{parameterName}";
                return conditionStatement;
            }
            else if (CompareType == CompareTypeConsts.In)
            {
                object[]? valList = GetValueList(valueStr);
                if (valList is null)
                {
                    return string.Empty;
                }

                StringBuilder inConditionBuilder = new($"{FieldName} in(");
                int valListIndex = 0;
                int valLength = valList.Length;

                #region 考虑当前字段有多个条件即多个in条件的情况, 第1次FieldName后缀为空(FIELDNAME_In, FIELDNAME_In1, FIELDNAME_In2...), 如果当前对象表示第2个in条件, 那么FieldName后缀就是"1"(FIELDNAME1_In, FIELDNAME1_In1, FIELDNAME1_In2...); In后缀用条件多个值对应的索引表示
                string fieldInConditionIndex = string.Empty;
                if (parameters is not null)
                {
                    string fieldLastInCondition = parameters.Keys.Where(x => Regex.IsMatch(x, $@"{FieldName}\d*_In$")).OrderBy(x => x).LastOrDefault();
                    if (!string.IsNullOrWhiteSpace(fieldLastInCondition))
                    {
                        var lastIndexStr = fieldLastInCondition.Replace(FieldName, string.Empty).Replace("_In", string.Empty);
                        if (string.IsNullOrWhiteSpace(lastIndexStr))
                        {
                            // 如果是空, 说明最后一个in条件是以"FIELDNAME_In"开始的, 也就是第一个in条件, 所以当前对象表示的已经是第2个in条件, 条件索引设为1
                            fieldInConditionIndex = "1";
                        }
                        else
                        {
                            //其他情况, 当前in条件索引 = 最后一个in条件的索引 + 1
                            fieldInConditionIndex = (int.Parse(lastIndexStr) + 1).ToString();
                        }
                    }
                }
                #endregion
                foreach (var valItem in valList)
                {
                    string parameterName = FieldName;
                    if (parameterName.Contains('.'))
                    {
                        parameterName = parameterName.Split('.').Last();
                    }
                    parameterName = valListIndex == 0 ? $"{parameterName}{fieldInConditionIndex}_In" : $"{parameterName}{fieldInConditionIndex}_In{valListIndex}";
                    string inValuesItem = valListIndex < valLength - 1 ? $"{varFlag}{parameterName}," : $"{varFlag}{parameterName}";
                    inConditionBuilder.Append(inValuesItem);
                    if (parameters is not null)
                    {
                        parameters[parameterName] = valItem;
                    }
                    valListIndex++;
                }
                inConditionBuilder.Append(')');
                return inConditionBuilder.ToString();
            }
            else
            {
                #region 考虑当前字段有多个条件时构建变量名 = FieldName + 索引后缀
                var parameterName = FieldName;
                if (parameterName.Contains("."))
                {
                    parameterName = parameterName.Split('.').Last();
                }
                if (parameters is not null && parameters.ContainsKey(parameterName))
                {
                    var last = parameters.Keys.Where(x => x.StartsWith(parameterName)).OrderBy(x => x).Last();
                    var lastIndexStr = last.Replace(parameterName, string.Empty);
                    var index = string.IsNullOrWhiteSpace(lastIndexStr) ? 0 : int.Parse(lastIndexStr);
                    parameterName = $"{parameterName}{index + 1}";
                }
                #endregion

                string conditionStatement = CompareType == CompareTypeConsts.Include
                    ? $"{FieldName} LIKE CONCAT(CONCAT('%', {varFlag}{parameterName}), '%')"
                    : $"{FieldName} {CompareType} {varFlag}{parameterName}";

                if (parameters is not null)
                {
                    if (Value is string && (Regex.IsMatch(valueStr, @"\d{4}-\d{1,2}-\d{1,2}") || Regex.IsMatch(valueStr, @"\d{1,2}:\d{1,2}:\d{1,2}")) && DateTime.TryParse(valueStr, out DateTime valDateTime))
                    {
                        parameters.Add(parameterName, valDateTime);
                    }
                    else
                    {
                        parameters.Add(parameterName, Value);
                    }
                }

                return conditionStatement;
            }
        }
        /// <summary>
        /// 重写ToString方法, 返回条件的主要信息
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            if (CompareType == CompareTypeConsts.In)
            {
                if (Value is null)
                {
                    return $"{FieldName} {CompareType} ";
                }
                object[]? valList;
                bool valItemIsNumber = false;
                if (Value is string)
                {
                    valList = $"{Value}".Split(',');
                }
                else if (Value is IEnumerable objList)
                {
                    valList = objList.Cast<object>().ToArray();
                    if (valList.Length == 0)
                    {
                        return $"{FieldName} {CompareType} ";
                    }
                    var first = valList.First();
                    if (first is JsonElement jeFirst && jeFirst.ValueKind == JsonValueKind.Number)
                    {
                        valItemIsNumber = true;
                    }
                    else if (first is Int16 || first is Int32 || first is Int64 || first is float || first is double)
                    {
                        valItemIsNumber = true;
                    }
                }
                else
                {
                    return $"{FieldName} {CompareType} ({Value})";
                }
                StringBuilder inConditionBuilder = new($" {FieldName} in(");
                int valListIndex = 0;
                int valLength = valList.Length;
                foreach (var valItem in valList)
                {
                    var itemStatement = valItemIsNumber ? $"{valItem}" : $"'{valItem}'";
                    string inValuesItem = valListIndex < valLength - 1 ? $"{itemStatement}," : $"{itemStatement}";
                    inConditionBuilder.Append(inValuesItem);
                    valListIndex++;
                }
                inConditionBuilder.Append(')');
                return inConditionBuilder.ToString();
            }
            else if (CompareType == CompareTypeConsts.Include)
            {
                return $"{FieldName} LIKE CONCAT(CONCAT('%', {Value}), '%')";
            }
            return $"{FieldName} {CompareType} {Value}";
        }

        object[]? GetValueList(string valueStr)
        {
            if (Value is null)
            {
                return null;
            }
            
            object[] valList;
            
            if (Value is string)
            {
                valList = valueStr.Split(',');
            }
            else if (Value is IEnumerable objList)
            {
                var objValues = objList.Cast<object>().ToArray();
                if (!objValues.Any() || objValues.All(x => x is null))
                {
                    return null;
                }
                var first = objValues.First();
                if (first is JsonElement jeFirst)
                {
                    if (jeFirst.ValueKind == JsonValueKind.Number)
                    {
                        if (objValues.Any(x => x.ToString().Contains('.')))
                        {
                            valList = objValues.Cast<JsonElement>().Select(x => x.GetDouble()).Cast<object>().ToArray();
                        }
                        else
                        {
                            valList = objValues.Cast<JsonElement>().Select(x => x.GetInt32()).Cast<object>().ToArray();
                        }
                    }
                    else
                    {
                        valList = objValues.Cast<JsonElement>().Select(x => x.GetString()).Cast<object>().ToArray();
                    }
                }
                else
                {
                    valList = objValues;
                }
            }
            else
            {
                valList = [Value];
            }
            return valList;
        }
    }
    /// <summary>
    /// 关键字
    /// </summary>
    public class Keywords
    {
        /// <summary>
        /// 关键字搜索字段
        /// </summary>
        public string[] Fields { get; set; } = [];
        /// <summary>
        /// 关键字的值
        /// </summary>
        public string Value { get; set; } = string.Empty;
    }
    /// <summary>
    /// 数据过滤器
    /// </summary>
    public class DataFilter
    {
        /// <summary>
        /// 过滤参数集合
        /// </summary>
        public List<FilterItem> FilterItems { get; set; } = [];
        /// <summary>
        /// 关键字
        /// </summary>
        public Keywords Keywords { get; set; } = new Keywords();
    }

    /// <summary>
    /// 分页查询参数
    /// </summary>
    public class SqlPage
    {
        /// <summary>
        /// 使用默认值创建对象
        /// </summary>
        public SqlPage()
        {

        }
        /// <summary>
        /// 使用指定分页参数创建对象
        /// </summary>
        /// <param name="pageIndex"></param>
        /// <param name="pageSize"></param>
        public SqlPage(int pageIndex, int pageSize)
        {
            PageIndex = pageIndex;
            PageSize = pageSize;
        }
        /// <summary>
        /// 分页 - 第几页
        /// </summary>
        public int PageIndex { get; set; }
        /// <summary>
        /// 分页 - 每页多少条记录
        /// </summary>
        public int PageSize { get; set; }
    }
    /// <summary>
    /// 查询条件组信息
    /// </summary>
    public class SqlGroupInfo
    {
        /// <summary>
        /// 分组字段
        /// </summary>
        public string FieldName { get; set; } = string.Empty;
        /// <summary>
        /// 分组条件
        /// </summary>
        public FilterGroup Having { get; set; } = new FilterGroup();
    }
    /// <summary>
    /// 查询数据表参数对象
    /// </summary>
    public class QueryTablesInDto
    {
        /// <summary>
        /// 要查询的主表
        /// </summary>
        public QueryTable Select { get; set; } = new QueryTable();
        /// <summary>
        /// 联查的表
        /// </summary>
        public List<QueryTable> LeftJoins { get; set; } = [];
        /// <summary>
        /// 表(或多表)查询的条件信息
        /// </summary>
        public FilterGroup Where { get; set; } = new FilterGroup();
        /// <summary>
        /// 分组参数
        /// </summary>
        public SqlGroupInfo Group { get; set; } = new SqlGroupInfo();
        /// <summary>
        /// 排序参数
        /// </summary>
        public List<OrderField> OrderBy { get; set; } = [];
        /// <summary>
        /// 分页参数
        /// </summary>
        public SqlPage Page { get; set; } = new SqlPage();
    }
    /// <summary>
    /// 查询的数据表对象
    /// </summary>
    public class QueryTable
    {
        /// <summary>
        /// 表名
        /// </summary>
        public string TableName { get; set; } = string.Empty;
        /// <summary>
        /// 别名
        /// </summary>
        public string Alias { get; set; } = string.Empty;
        /// <summary>
        /// 查询的字段列表
        /// </summary>
        public string[]? SelectColumns { get; set; } = [];
        /// <summary>
        /// 过滤条件组
        /// </summary>
        public FilterGroup? OnConditions { get; set; } = new FilterGroup();
    }
}
