using System;
using MailKit;
using MimeKit;
using MailKit.Net.Smtp;
using Microsoft.EntityFrameworkCore;

namespace Review.Models
{
    public interface IEmailSender
    {
        Task SendEmailAsync(string receiverUserName, string receiverEmail, string subject, string message);
    }

    public class EmailSender : IEmailSender
    {
        public async Task SendEmailAsync(string receiverUserName, string receiverEmail, string subject, string message)
        {
            string senderEmail = "ahsgfdajhsgt@gmail.com";
            string senderGmailPassword = "wnmwahawpaexytmh";
            string senderName = "Admin";

            MimeMessage email = new MimeMessage();
            email.From.Add(new MailboxAddress(senderName, senderEmail));
            email.To.Add(new MailboxAddress(receiverUserName, receiverEmail));
            email.Subject = subject;
            email.Body = new TextPart(MimeKit.Text.TextFormat.Html)
            {
                Text = message
            };

            using (SmtpClient smtp = new SmtpClient())
            {
                await smtp.ConnectAsync("smtp.gmail.com", 587, false);
                await smtp.AuthenticateAsync(senderEmail, senderGmailPassword);
                await smtp.SendAsync(email);
                await smtp.DisconnectAsync(true);
            }

        }
    }
}