using EmailService.Models;

using Microsoft.Extensions.Logging;

using Orleans.Runtime;

using Processor.Grains.Interfaces;

using System.Diagnostics.Metrics;
using System.Text;

namespace Processor.Grains;

public class ClientTemplateConfigGrain : Grain, IClientTemplateConfigGrain
{
    private readonly IPersistentState<TemplatesState> _state;    
    private readonly ILogger<ClientTemplateConfigGrain> _logger;
    private readonly Meter _meter;
    private readonly Histogram<long> _templateSizeHist;

    public ClientTemplateConfigGrain(
        [PersistentState("email-templates", "templates")] IPersistentState<TemplatesState> state,
        IMeterFactory meterFactory,
        ILogger<ClientTemplateConfigGrain> logger)
    {
        _state = state;
        _logger = logger;

        _meter = meterFactory.Create(new MeterOptions("TemplatesMeter"));
        
        _templateSizeHist = _meter.CreateHistogram<long>("template_size", "byte", "size of the template");
    }

    public ValueTask<Dictionary<string, LocaleCollection>> GetAllTemplates()
    {
        return ValueTask.FromResult(_state.State.Locales);
    }

    public ValueTask<IEnumerable<string>> GetLanguages()
    {
        return ValueTask.FromResult(_state.State.Locales.Keys.AsEnumerable());
    }

    public ValueTask<Template?> GetTemplate(string lang, string type) => ValueTask.FromResult(
        _state.State.Locales.TryGetValue(lang, out var locales) 
        && locales.Templates.TryGetValue(type, out var template)
            ? template
            : null);

    public ValueTask<IEnumerable<Template>> GetTemplates(string lang)
    {
        return ValueTask.FromResult(_state.State.Locales.TryGetValue(lang, out var localeCollection)
            ? localeCollection.Templates.Values
            : Enumerable.Empty<Template>());
    }

    public async Task SetTemplate(string lang, string type, string subjectTemplate, string bodyTemplate)
    {
        if (!_state.State.Locales.TryGetValue(lang, out var localeCollection))
        {
            localeCollection = new();
            _state.State.Locales[lang] = localeCollection;
        }

        if (localeCollection.Templates.ContainsKey(type))
        {
            _logger.LogInformation("replacing `{type}` template for `{lang}` language", type, lang);
        }

        localeCollection.Templates[type] = new(lang, type, subjectTemplate, bodyTemplate);
        
        _templateSizeHist.Record(
            Encoding.UTF8.GetByteCount(bodyTemplate), 
            new KeyValuePair<string, object?>("type", type), 
            new KeyValuePair<string, object?>("lang", lang)
        );

        await _state.WriteStateAsync();
    }
}

public class TemplatesState
{
    public Dictionary<string, LocaleCollection> Locales { get; set; } = [];
}
