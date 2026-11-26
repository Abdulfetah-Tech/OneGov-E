using OneGov.Application.Documents;

namespace OneGov.Infrastructure.Services;

/// <summary>
/// Keeps files on disk below one root folder, laid out as ab/cd/abcd... so no folder grows huge. Keys must be 32 hex characters,
/// which makes path tricks impossible. Disk encryption and backups are the operator's responsibility.
/// </summary>
public sealed class FileSystemDocumentStore(string root) : IDocumentStore
{
    public async Task SaveAsync(string key, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        var path = PathFor(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Write beside the target, then move into place, so a crash never leaves a half-written file under the real name.
        var temporary = path + ".tmp";
        await File.WriteAllBytesAsync(temporary, content.ToArray(), cancellationToken);
        File.Move(temporary, path, overwrite: false);
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        var path = PathFor(key);
        Stream? stream = File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true)
            : null;
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        var path = PathFor(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string PathFor(string key)
    {
        if (key.Length != 32 || !key.All(char.IsAsciiHexDigit))
        {
            throw new ArgumentException("A storage key is 32 hexadecimal characters.", nameof(key));
        }

        return Path.Combine(root, key[..2], key[2..4], key);
    }
}
