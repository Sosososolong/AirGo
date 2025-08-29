using System;

namespace Sylas.RemoteTasks.Common.Dtos
{
    /// <summary>
    /// 参数信息, 包括参数名, 参数值, 参数类型等
    /// </summary>
    public class ParamInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string RealType { get; set; } = string.Empty;
        public string Others { get; set; } = string.Empty;
        public object RealValue
        {
            get
            {
                return RealType switch
                {
                    _ when string.IsNullOrWhiteSpace(RealType) => Value,
                    _ when RealType.Contains("time", StringComparison.OrdinalIgnoreCase) => Convert.ToDateTime(Value),
                    _ when RealType.Contains("bool", StringComparison.OrdinalIgnoreCase) => Value == "1" || Value == "True" || Value == "true",
                    _ when RealType.Contains("int", StringComparison.OrdinalIgnoreCase) => Value.Length > 0 ? Convert.ToInt32(Value) : 0,
                    _ when RealType.Contains("long", StringComparison.OrdinalIgnoreCase) => Value.Length > 0 ? Convert.ToInt64(Value) : 0,
                    _ when RealType.Contains("byte[]", StringComparison.OrdinalIgnoreCase) => Value.Length > 0 ? Convert.FromBase64String(Value) : [],
                    _ => Value,
                };
            }
        }
    }
}
