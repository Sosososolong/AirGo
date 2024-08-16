namespace Sylas.RemoteTasks.App.Study
{
    /// <summary>
    /// 用户回答问题
    /// </summary>
    public class AnswerQuestionDto
    {
        /// <summary>
        /// 问题Id
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// 用户的答案
        /// </summary>
        public string Answer { get; set; } = string.Empty;
    }
}
