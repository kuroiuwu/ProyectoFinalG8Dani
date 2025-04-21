using Microsoft.AspNetCore.Identity.UI.Services;
using System.Threading.Tasks;

namespace ProyectoFinal_G8.Services // Asegúrate que el namespace coincida con tu estructura
{
    public class DummyEmailSender : IEmailSender
    {
        // Este método simplemente completa la tarea sin enviar un email real.
        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            // Aquí podrías loggear el email si quisieras durante el desarrollo:
            // Console.WriteLine($"Email ficticio a {email}");
            // Console.WriteLine($"Asunto: {subject}");
            // Console.WriteLine($"Mensaje: {htmlMessage}");
            return Task.CompletedTask;
        }
    }
}