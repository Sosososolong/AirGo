using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sylas.RemoteTasks.App.Infrastructure;
using Sylas.RemoteTasks.App.Study;
using Sylas.RemoteTasks.Common.Dtos;
using Sylas.RemoteTasks.Database.SyncBase;
using System.ComponentModel.DataAnnotations;

namespace Sylas.RemoteTasks.App.Controllers
{
    public class StudyController(RepositoryBase<Question> questionRepository) : CustomBaseController
    {
        [AllowAnonymous]
        public IActionResult Index()
        {
            return View();
        }
        /// <summary>
        /// 问题分页查询
        /// </summary>
        /// <param name="search"></param>
        /// <returns></returns>
        public async Task<IActionResult> GetQuestions([FromBody] DataSearch? search = null)
        {
            search ??= new();
            var snippetPage = await questionRepository.GetPageAsync(search);
            var result = new RequestResult<PagedData<Question>>(snippetPage);
            return Ok(result);
        }

        /// <summary>
        /// 添加一个问题
        /// </summary>
        /// <param name="question"></param>
        /// <returns></returns>
        public async Task<IActionResult> AddQuestion([FromServices] IWebHostEnvironment env, [FromForm] Question question)
        {
            var operationResult = await SaveUploadedFilesAsync(env);
            if (operationResult.IsSuccess && operationResult.Data is not null)
            {
                question.ImageUrl = operationResult.Data.First();
            }
            int affectedRows = await questionRepository.AddAsync(question);
            var result = affectedRows > 0 ? new RequestResult<bool>(true) : new RequestResult<bool>(false);
            return Ok(result);
        }

        /// <summary>
        /// 更新一个问题
        /// </summary>
        /// <param name="question"></param>
        /// <returns></returns>
        public async Task<IActionResult> UpdateQuestion([FromServices] IWebHostEnvironment env, [FromForm] Dictionary<string, string> question)
        {
            Question? record = await questionRepository.GetByIdAsync(question);
            if (record is null)
            {
                return Ok(RequestResult<bool>.Error("记录不存在"));
            }

            string? imgUrlKey = question.Keys.FirstOrDefault(x => x.Equals("ImageUrl", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(imgUrlKey))
            {
                string imgUrl = question[imgUrlKey];
                var imgs = await HandleUploadedFilesAsync(record.ImageUrl, imgUrl, env);
                question[imgUrlKey] = string.Join(';', imgs);
            }

            int affectedRows = await questionRepository.UpdateAsync(question);
            var result = affectedRows > 0 ? new RequestResult<bool>(true) : new RequestResult<bool>(false);
            return Ok(result);
        }

        /// <summary>
        /// 删除一个问题
        /// </summary>
        /// <param name="question"></param>
        /// <returns></returns>
        public async Task<IActionResult> DeleteQuestion(int id)
        {
            int affectedRows = await questionRepository.DeleteAsync(id);
            var result = affectedRows > 0 ? new RequestResult<bool>(true) : new RequestResult<bool>(false);
            return Ok(result);
        }

        /// <summary>
        /// 回答问题
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<RequestResult<bool>> AnswerQuestion([FromBody][Required(ErrorMessage = "参数格式不正确")] AnswerQuestionDto dto)
        {
            var question = await questionRepository.GetByIdAsync(dto.Id);
            if (question is null)
            {
                return RequestResult<bool>.Error("问题不存在");
            }

            bool result = dto.Answer == question.Answer;
            if (result)
            {
                question.CorrectCount++;
            }
            else
            {
                question.ErrorCount++;
            }
            
            int affectedRows = await questionRepository.UpdateAsync(question);
            if (affectedRows <= 0)
            {
               return RequestResult<bool>.Error("更新失败");
            }
            return affectedRows > 0 ? new RequestResult<bool>(result) : new RequestResult<bool>(false);
        }

        /// <summary>
        /// 查询所有的题目类型
        /// </summary>
        /// <param name="typeRepository"></param>
        /// <returns></returns>
        public async Task<RequestResult<PagedData<QuestionType>>> GetQuestionTypes([FromServices]RepositoryBase<QuestionType> typeRepository, [FromBody] DataSearch search)
        {
            var pagedTypes = await typeRepository.GetPageAsync(search);
            var result = RequestResult<PagedData<QuestionType>>.Success(pagedTypes);
            return result;
        }
        /// <summary>
        /// 根据类型Id查询题目类型
        /// </summary>
        /// <param name="typeRepository"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<RequestResult<QuestionType>> GetQuestionType([FromServices] RepositoryBase<QuestionType> typeRepository, int id)
        {
            var type = await typeRepository.GetByIdAsync(id);
            if (type is null)
            {
                return RequestResult<QuestionType>.Error("类型不存在");
            }
            return RequestResult<QuestionType>.Success(type);
        }
    }
}
