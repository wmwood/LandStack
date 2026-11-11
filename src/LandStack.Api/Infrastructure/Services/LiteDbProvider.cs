using System;
using System.IO;
using LiteDB;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace LandStack.Api.Infrastructure.Services;

public sealed class LiteDbProvider : IDisposable
{
    private readonly LiteDatabase _db;
    private readonly object _disposeLock = new();

    public LiteDbProvider(IOptions<LiteDbOptions> options, IHostEnvironment environment)
    {
        var configured = options.Value.DataDirectory;
        var directory = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(environment.ContentRootPath, "App_Data")
            : Path.GetFullPath(configured, environment.ContentRootPath);
        Directory.CreateDirectory(directory);
        // Preserve supplied text and historical invalid strings without mutating global mapping.
        var mapper = new BsonMapper { TrimWhitespace = false, EmptyStringToNull = false };
        _db = new LiteDatabase(Path.Combine(directory, "database.db"), mapper);
    }

    public LiteDatabase Database => _db;

    public void Dispose()
    {
        // Concurrent shutdown paths must wait for the first close to release file handles.
        lock (_disposeLock)
            _db.Dispose();
    }
}
