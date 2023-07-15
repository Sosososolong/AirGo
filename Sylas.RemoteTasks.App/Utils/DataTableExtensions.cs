using Newtonsoft.Json.Linq;
using System.Data;

namespace Sylas.RemoteTasks.App.Utils
{
    public static class DataTableExtensions
    {
        public static IEnumerable<T> ToObjectList<T>(this DataTable source) where T : new()
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            if (typeof(T).Name == "JObject")
            {
                var rows = source.AsEnumerable().Select(row => JObject.FromObject(row));
                return rows.Select(x => x.ToObject<T>());
            }
            else if (typeof(T).Name == "object")
            {
                return source.AsEnumerable().Select(row =>
                {
                    dynamic dynamicRow = new System.Dynamic.ExpandoObject();
                    foreach (DataColumn column in source.Columns)
                    {
                        ((IDictionary<string, object>)dynamicRow)[column.ColumnName] = row[column];
                    }
                    return (T)dynamicRow;
                });
            }
            var objectList = new List<T>();
            foreach (DataRow dataRow in source.Rows)
            {
                var objectInstance = new T();
                foreach (DataColumn dataColumn in source.Columns)
                {
                    var property = typeof(T).GetProperty(dataColumn.ColumnName);
                    if (property != null && dataRow[dataColumn] != DBNull.Value)
                    {
                        property.SetValue(objectInstance, Convert.ChangeType(dataRow[dataColumn], property.PropertyType));
                    }
                }
                objectList.Add(objectInstance);
            }
            return objectList;
        }
    }
}
