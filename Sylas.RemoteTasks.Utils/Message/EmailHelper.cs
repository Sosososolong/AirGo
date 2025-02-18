using MailKit.Net.Smtp;
using MimeKit;
using Sylas.RemoteTasks.Common.Dtos;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Sylas.RemoteTasks.Utils.Message
{
    /// <summary>
    /// 电子邮件帮助类
    /// </summary>
    public class EmailHelper
    {
        /// <summary>
        /// 发送电子邮件
        /// </summary>
        /// <param name="sender">发件人</param>
        /// <param name="to">收件人</param>
        /// <param name="subject">主题</param>
        /// <param name="body">正文</param>
        public static async Task<OperationResult> SendAsync(EmailSender sender, string to, string subject, string body)
        {
            string toName = to.Split('@')[0];
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(sender.Name, sender.Address));
            message.To.Add(new MailboxAddress(toName, to));
            message.Subject = subject;

            message.Body = new TextPart("html")
            {
                Text = body
            };

            using var client = new SmtpClient();
            try
            {
                await client.ConnectAsync(sender.Server, sender.Port, sender.UseSsl);

                await client.AuthenticateAsync(sender.Address, sender.Password);

                string sendResult = await client.SendAsync(message);
                Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} 发送邮件结束: {to} -> {sendResult}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {ex}");
                return new OperationResult(false, "发送邮件失败");
            }
            finally
            {
                await client.DisconnectAsync(true);
            }
            return new OperationResult(true, string.Empty);
        }

        /// <summary>
        /// 发送电子邮件
        /// </summary>
        /// <param name="sender">发件人</param>
        /// <param name="tos">收件人</param>
        /// <param name="subject">主题</param>
        /// <param name="body">正文</param>
        public static async Task<OperationResult> SendAsync(EmailSender sender, IEnumerable<string> tos, string subject, string body)
        {
            List<Task> sendTasks = [];
            foreach (var to in tos)
            {
                Task task = SendAsync(sender, to, subject, body);
                sendTasks.Add(task);
            }
            await Task.WhenAll(sendTasks);
            return new OperationResult(true, string.Empty);
        }
    }
}
