using EmailService.Models;

using Microsoft.Extensions.Logging;

using Orleans.Runtime;

using Processor.Grains.Interfaces;

namespace Processor.Grains;

public record class Smtp(
    string ServerUrl,
    string ServerPort,
    string From,
    string FromName,
    string Username,
    string Password);

public class SmtpConfigGrain : Grain, ISmtpConfigGrain
{
    private readonly IPersistentState<Smtp> _state;
    private readonly ILogger<SmtpConfigGrain> _logger;

    private Smtp State => _state.State;

    public SmtpConfigGrain(
        [PersistentState(stateName: "SmtpConfig", storageName: "smtp-config-storage")] IPersistentState<Smtp> state,
        ILogger<SmtpConfigGrain> logger)
    {
        _state = state;
        _logger = logger;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("getting smtp config");
        await _state.ReadStateAsync();
        
        await base.OnActivateAsync(cancellationToken);
    }

    public ValueTask<SmtpConfig?> GetConfig() => _state.RecordExists
            ? ValueTask.FromResult<SmtpConfig?>(new SmtpConfig
            (
                State.ServerUrl,
                State.ServerPort,
                State.From,
                State.FromName,
                State.Username,
                State.Password
            ))
            : ValueTask.FromResult<SmtpConfig?>(default); // yeah I know bleh

    public async Task SetConfig(SmtpConfig config)
    {
        _state.State = new Smtp(
            config.ServerUrl,
            config.ServerPort,
            config.From,
            config.FromName,
            config.UserName,
            config.Password);

        await _state.WriteStateAsync();
    }
}
