using EmailService.Models;

using Processor.Grains.Interfaces;

namespace Processor.Grains.Interfaces;

public interface ISmtpConfigGrain : IGrainWithStringKey
{
    public ValueTask<SmtpConfig?> GetConfig();
    
    public Task SetConfig(SmtpConfig config);
}

public interface ISmtpDispatchServiceGrain : IGrainWithStringKey
{
    public Task SendMessage(SmtpConfig smtpConfig, EmailRequest request);
}


public interface IClientTemplateConfigGrain : IGrainWithStringKey
{
    public Task SetTemplate(string lang, string type, string subjectTemplate, string bodyTemplate);
    
    public ValueTask<Template?> GetTemplate(string lang, string type);

    public ValueTask<IEnumerable<Template>> GetTemplates(string lang);
    
    public ValueTask<IEnumerable<string>> GetLanguages();
    
    public ValueTask<Dictionary<string, LocaleCollection>> GetAllTemplates();
}