using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Distributed;
using NATS.Client;
using System.Text.Json;

namespace Backend.Services;

public class AccountService
{
    private readonly IDistributedCache _cache;
    private readonly SqliteConnection _connection;

    private readonly Options _natsOptions;

    private readonly object _registerLock = new();

    public AccountService(IConfiguration configuration, IDistributedCache cache)
    {
        _connection = new(configuration.GetValue<string?>("ConnectionString") ?? throw new InvalidOperationException("Require connection string"));
        _cache = cache;

        _natsOptions = ConnectionFactory.GetDefaultOptions();
        _natsOptions.Servers = new[] { configuration.GetValue<string?>("NatsEndpoint") ?? throw new InvalidOperationException("Require nats endpoint") };
    }

    public void Initialize()
    {
        _connection.Open();
        _connection.Execute("CREATE TABLE IF NOT EXISTS account(address TEXT PRIMARY KEY NOT NULL, name TEXT NOT NULL) WITHOUT ROWID;");
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
            _connection.Execute("INSERT INTO account VALUES(@address, @name);", new { address = address.ToLowerInvariant(), name });
    }

    public string? GetName(string address) =>
        _connection.ExecuteScalar<string?>("SELECT name FROM account WHERE address = @address;", new { address = address.ToLowerInvariant() });

    public void NotifyLogin(string session, string address, string name)
    {
        using var connection = new ConnectionFactory().CreateConnection(_natsOptions);

        var json = JsonSerializer.SerializeToUtf8Bytes(new { session, address = address.ToLowerInvariant(), name });

        connection.Publish("login_result", json);
    }
}
