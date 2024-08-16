﻿using Microsoft.AspNetCore.Mvc;
using Sylas.RemoteTasks.App.Infrastructure;
using Sylas.RemoteTasks.App.Study;
using Sylas.RemoteTasks.Database.SyncBase;
using Sylas.RemoteTasks.Utils.Dto;
using System.ComponentModel.DataAnnotations;

namespace Sylas.RemoteTasks.App.Controllers
{
    public class StudyController(RepositoryBase<Question> questionRepository) : CustomBaseController
    {
        public IActionResult Index()
        {
            return View();
        }
        /// <summary>
        /// 问题分页查询
        /// </summary>
        /// <param name="pageIndex"></param>
        /// <param name="pageSize"></param>
        /// <param name="orderField"></param>
        /// <param name="isAsc"></param>
        /// <param name="dataFilter"></param>
        /// <returns></returns>
        public async Task<IActionResult> GetQuestions(int pageIndex, int pageSize, string orderField = "", bool isAsc = false, [FromBody] DataFilter? dataFilter = null)
        {
            if (string.IsNullOrWhiteSpace(orderField))
            {
                orderField = "ErrorCount";
            }
            var snippetPage = await questionRepository.GetPageAsync(pageIndex, pageSize, orderField, isAsc, dataFilter);
            var result = new RequestResult<PagedData<Question>>(snippetPage);
            return Ok(result);
        }

        /// <summary>
        /// 添加一个问题
        /// </summary>
        /// <param name="question"></param>
        /// <returns></returns>
        public async Task<IActionResult> AddQuestion([FromBody] Question question)
        {
            int affectedRows = await questionRepository.AddAsync(question);
            var result = affectedRows > 0 ? new RequestResult<bool>(true) : new RequestResult<bool>(false);
            return Ok(result);
        }

        /// <summary>
        /// 更新一个问题
        /// </summary>
        /// <param name="question"></param>
        /// <returns></returns>
        public async Task<IActionResult> UpdateQuestion([FromBody] Question question)
        {
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
        public async Task<RequestResult<PagedData<QuestionType>>> GetQuestionTypes([FromServices]RepositoryBase<QuestionType> typeRepository, int pageIndex = 1, int pageSize = 10, string orderField = "", bool isAsc = false, [FromBody] DataFilter? dataFilter = null)
        {
            var pagedTypes = await typeRepository.GetPageAsync(pageIndex, pageSize, orderField, isAsc, dataFilter);
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
