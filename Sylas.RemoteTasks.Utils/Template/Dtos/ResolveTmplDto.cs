namespace Sylas.RemoteTasks.Utils.Template.Dtos
{
    /// <summary>
    /// 解析模板传参
    /// </summary>
    public class ResolveTmplDto
    {
        /// <summary>
        /// 模板文本
        /// </summary>
        public string TmplTxt { get; set; } = "";
        /// <summary>
        /// 模板数据模型json字符串
        /// </summary>
        public string DatamodelJson { get; set; } = "";
    }
}
