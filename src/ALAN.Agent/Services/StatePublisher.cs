using ALAN.Shared.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace ALAN.Agent.Services;

public class StatePublisher
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<StatePublisher> _logger;
    private readonly string _webServiceUrl;
    private bool _isConnected = false;
    private DateTime _lastConnectionAttempt = DateTime.MinValue;
    private readonly TimeSpan _reconnectInterval = TimeSpan.FromMinutes(1);

    public StatePublisher(ILogger<StatePublisher> logger, IConfiguration configuration)
    {
        // TODO: publish current state to a table or message queue, we should not depend on the webservice here
        _logger = logger;
        _webServiceUrl = configuration["WebService:Url"]
            ?? Environment.GetEnvironmentVariable("WEB_SERVICE_URL")
            ?? "http://localhost:5000";

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_webServiceUrl),
            Timeout = TimeSpan.FromSeconds(2)
        };

        _logger.LogInformation("StatePublisher initialized with Web service URL: {Url}", _webServiceUrl);
    }

    public async Task PublishStateAsync(AgentState state, CancellationToken cancellationToken = default)
    {
        if (!ShouldAttemptConnection())
            return;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/state", state, cancellationToken);
            response.EnsureSuccessStatusCode();

            if (!_isConnected)
            {
                _isConnected = true;
                _logger.LogInformation("Successfully connected to Web service at {Url}", _webServiceUrl);
            }
        }
        catch (HttpRequestException)
        {
            HandleConnectionFailure();
        }
        catch (TaskCanceledException)
        {
            HandleConnectionFailure();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error publishing state to Web service");
        }
    }

    public async Task PublishThoughtAsync(AgentThought thought, CancellationToken cancellationToken = default)
    {
        if (!ShouldAttemptConnection())
            return;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/thought", thought, cancellationToken);
            response.EnsureSuccessStatusCode();

            if (!_isConnected)
            {
                _isConnected = true;
                _logger.LogInformation("Successfully connected to Web service at {Url}", _webServiceUrl);
            }
        }
        catch (HttpRequestException)
        {
            HandleConnectionFailure();
        }
        catch (TaskCanceledException)
        {
            HandleConnectionFailure();
        }
    }

    public async Task PublishActionAsync(AgentAction action, CancellationToken cancellationToken = default)
    {
        if (!ShouldAttemptConnection())
            return;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/action", action, cancellationToken);
            response.EnsureSuccessStatusCode();

            if (!_isConnected)
            {
                _isConnected = true;
                _logger.LogInformation("Successfully connected to Web service at {Url}", _webServiceUrl);
            }
        }
        catch (HttpRequestException)
        {
            HandleConnectionFailure();
        }
        catch (TaskCanceledException)
        {
            HandleConnectionFailure();
        }
    }

    private bool ShouldAttemptConnection()
    {
        if (_isConnected)
            return true;

        var now = DateTime.UtcNow;
        if (now - _lastConnectionAttempt < _reconnectInterval)
            return false;

        _lastConnectionAttempt = now;
        return true;
    }

    private void HandleConnectionFailure()
    {
        if (_isConnected)
        {
            _logger.LogWarning("Lost connection to Web service at {Url}", _webServiceUrl);
            _isConnected = false;
        }
        else if (_lastConnectionAttempt == DateTime.UtcNow)
        {
            // Only log on first attempt and subsequent retry attempts
            _logger.LogDebug("Web service at {Url} is not reachable. Will retry every {Minutes} minute(s)",
                _webServiceUrl, _reconnectInterval.TotalMinutes);
        }
    }
}
