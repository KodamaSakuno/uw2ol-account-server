using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Distributed;
using RabbitMQ.Client;
using System.Text.Json;

namespace Backend.Services;

public class AccountService
{
    private readonly IConfiguration _configuration;
    private readonly IDistributedCache _cache;
    private readonly SqliteConnection _sqliteConnection;

    private readonly object _registerLock = new();

    private IConnection _rabbitMqConnection = default!;
    private IModel _channel = default!;

    public AccountService(IConfiguration configuration, IDistributedCache cache)
    {
        _configuration = configuration;
        _sqliteConnection = new(_configuration.GetValue<string?>("ConnectionString") ?? throw new InvalidOperationException("Require connection string"));
        _cache = cache;
    }

    public void Initialize()
    {
        _sqliteConnection.Open();
        _sqliteConnection.Execute("CREATE TABLE IF NOT EXISTS account(address TEXT PRIMARY KEY NOT NULL, name TEXT NOT NULL) WITHOUT ROWID;");

        var rabbitMqFactory = new ConnectionFactory()
        {
            HostName = _configuration.GetValue<string?>("RabbitMQHost") ?? throw new InvalidOperationException("Require rabbitmq host")
        };

        _rabbitMqConnection = rabbitMqFactory.CreateConnection();

        _channel = _rabbitMqConnection.CreateModel();
        _channel.QueueDeclare("LoginResult", exclusive: false);
    }

    public async ValueTask<string?> GetNonceAsync(string address) => await _cache.GetStringAsync($"nonce:{address.ToLowerInvariant()}");
    public async ValueTask<int> RefreshNonceAsync(string address)
    {
        var result = Random.Shared.Next(10000);

        await _cache.SetStringAsync($"nonce:{address.ToLowerInvariant()}", result.ToString(), new DistributedCacheEntryOptions()
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
        });

        return result;
    }

    public void Register(string address, string name)
    {
        lock(_registerLock)
            _sqliteConnection.Execute("INSERT INTO account VALUES(@address, @name);", new { address = address.ToLowerInvariant(), name });
    }

    public string? GetName(string address) =>
        _sqliteConnection.ExecuteScalar<string?>("SELECT name FROM account WHERE address = @address;", new { address = address.ToLowerInvariant() });

    public void NotifyLogin(string session, string address, string name)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(new { session, address = address.ToLowerInvariant(), name });

        _channel.BasicPublish(string.Empty, "LoginResult", body: json);
    }
}
