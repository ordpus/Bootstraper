extern alias SystemBuffers;
extern alias SystemMemory;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;

using Bootstrap;

using Microsoft.Extensions.Logging;

using SystemBuffers::System.Buffers;

// ReSharper disable once CheckNamespace
namespace BootstrapApi.Logger;

internal sealed class NullScope : IDisposable {
    public static NullScope Instance { get; } = new();

    private NullScope() { }

    public void Dispose() { }
}

public interface IAsyncStreamWriter : IDisposable {
    public bool TryPublish(string content);
}

public class AsyncQueuedStreamWriter : IAsyncStreamWriter {
    private readonly StreamWriter _stream;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly ChannelReader<string> _reader;
    private readonly ChannelWriter<string> _writer;
    private readonly ArrayPool<char> _pool = ArrayPool<char>.Shared;
    private readonly Task _task;
    private volatile bool _disposed;

    private readonly int _batchSize;
    private readonly int _batchTimeout;

    public AsyncQueuedStreamWriter(
        StreamWriter stream, int maxQueueSize = 1000, int batchSize = 128,
        int batchTimeoutMillisecond = 100) {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _batchSize = batchSize;
        _batchTimeout = batchTimeoutMillisecond;

        var options = new BoundedChannelOptions(maxQueueSize) {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        };

        Channel<string> channel = Channel.CreateBounded<string>(options);
        _writer = channel.Writer;
        _reader = channel.Reader;
        _task = Task.Run(() => {
            try {
                return ConsumerLoop(_cancellation.Token);
            } catch (Exception e) {
                BootstrapLog.ErrorLogger.WriteLine(e.ToString());
                throw;
            }
        });
    }

    ~AsyncQueuedStreamWriter() {
        Dispose();
    }

    public bool TryPublish(string content) {
        return !_disposed && _writer.TryWrite(content);
    }

    private async Task ConsumerLoop(CancellationToken cancellation) {
        try {
            var count = 0;
            var lastTimeout = false;

            while (!cancellation.IsCancellationRequested) {
                if (count >= _batchSize) {
                    await _stream.FlushAsync();
                    count = 0;
                }

                if (lastTimeout) {
                    var readSuccessful = await _reader.WaitToReadAsync(cancellation);
                    if (!readSuccessful) return;
                    while (_reader.TryRead(out var item)) {
                        count++;
                        await Process(item);
                    }
                } else {
                    using var readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                    var readTask = _reader.WaitToReadAsync(readCts.Token).AsTask();
                    var timeoutTask = Task.Delay(_batchTimeout, timeoutCts.Token);

                    var resultTask = await Task.WhenAny(readTask, timeoutTask).ConfigureAwait(false);
                    if (resultTask == readTask) {
                        lastTimeout = false;
                        timeoutCts.Cancel();
                        var readSuccessful = await readTask;
                        if (!readSuccessful) return;
                        while (_reader.TryRead(out var item)) {
                            count++;
                            await Process(item);
                        }
                    } else {
                        lastTimeout = true;
                        readCts.Cancel();
                        await _stream.FlushAsync();
                    }
                }
            }

            await _stream.FlushAsync();
        } catch (OperationCanceledException) { } catch (Exception e) {
            await BootstrapLog.ErrorLogger.WriteLineAsync(e.ToString());
        }
    }

    private async Task Process(string content) {
        var resultLength = content.Length + 1;
        var result = _pool.Rent(resultLength);
        try {
            content.AsSpan().CopyTo(result.AsSpan());
            result[resultLength - 1] = '\n';

            await _stream.WriteAsync(result, 0, resultLength);
        } catch (Exception e) {
            await BootstrapLog.ErrorLogger.WriteLineAsync(e.ToString());
        } finally {
            _pool.Return(result);
        }
    }

    public void Dispose() {
        if (_disposed) return;
        _disposed = true;
        _writer.Complete();
        _cancellation.Cancel();
        _task.Wait(TimeSpan.FromSeconds(10));
        _stream.Dispose();
        _cancellation.Dispose();
    }
}

public class LoggerProvider : ILoggerProvider {
    private static readonly SHA256 Sha256 = SHA256.Create();
    private static readonly ConcurrentDictionary<string, AsyncQueuedStreamWriter> StreamWriters = [];
    private readonly ConcurrentDictionary<string, Logger> _loggers = new();
    private readonly string _hashKey;
    private readonly IAsyncStreamWriter _writer;
    private readonly int _categoryLength;

    private static string GetHashKey(string filePath) {
        var fullPath = Path.GetFullPath(filePath);
        return Sha256.ComputeHash(Encoding.UTF8.GetBytes(fullPath)).ToHexString();
    }

    private static StreamWriter GetWriter(string filePath) {
        File.WriteAllText(filePath, "");
        return new StreamWriter(
            new FileStream(
                filePath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.ReadWrite | FileShare.Delete));
    }

    public static LoggerProvider Create(string filePath, int categoryLength = 10) {
        try {
            var unifiedFilePath =
                RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                    ? filePath
                    : filePath.ToLower();
            var hashKey = GetHashKey(unifiedFilePath);
            var stream = StreamWriters.GetOrAdd(hashKey, _ => new AsyncQueuedStreamWriter(GetWriter(filePath)));
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);
            return new LoggerProvider(hashKey, stream, categoryLength);
        } catch (Exception e) {
            BootstrapLog.ErrorLogger.WriteLine(e.ToString());
            throw;
        }
    }

    protected LoggerProvider(string hashKey, IAsyncStreamWriter writer, int categoryLength) {
        _writer = writer;
        _categoryLength = categoryLength;
        _hashKey = hashKey;
    }

    public void Dispose() {
        _loggers.Clear();
        _writer.Dispose();
        if (StreamWriters.TryRemove(_hashKey, out var writer)) writer.Dispose();
    }

    public ILogger CreateLogger(string categoryName, LogLevel level) {
        try {
            return _loggers.GetOrAdd(categoryName, name => new Logger(name, level, _writer, _categoryLength));
        } catch (Exception e) {
            BootstrapLog.ErrorLogger.WriteLine(e.ToString());
            throw;
        }
    }

    public ILogger CreateLogger(string categoryName) {
        return CreateLogger(categoryName, LogLevel.Information);
    }
}

public class Logger(string categoryName, LogLevel level, IAsyncStreamWriter writer, int categoryLength)
    : ILogger {
    private readonly string _categoryName = categoryName ?? throw new ArgumentNullException(nameof(categoryName));
    private readonly WeakReference<IAsyncStreamWriter> _writer = new(writer);

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter) {
        if (!IsEnabled(logLevel)) return;
        var message = formatter(state, exception);
        if (!_writer.TryGetTarget(out var writer)) return;

        var cn = _categoryName.Length > categoryLength
            ? _categoryName.Substring(0, categoryLength)
            : _categoryName.PadRight(categoryLength);
        var logLevelFormatted = logLevel switch {
            LogLevel.Trace => "TRACE",
            LogLevel.Debug => "DEBUG",
            LogLevel.Information => "INFO ",
            LogLevel.Warning => "WARN ",
            LogLevel.Error => "ERR  ",
            LogLevel.Critical => "CRIT ",
            LogLevel.None => "None ",
            _ => throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null)
        };
        writer.TryPublish($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{cn}] [{logLevelFormatted}] {message}");

        if (exception != null) writer.TryPublish(exception.ToString());
    }

    public bool IsEnabled(LogLevel logLevel) {
        return level <= logLevel;
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull {
        return state == null ? throw new ArgumentNullException(nameof(state)) : NullScope.Instance;
    }
}

public static class BootstrapLog {
    // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
    public static readonly LoggerProvider DefaultProvider;
    public static readonly Logger DefaultLogger;

    public static readonly StreamWriter ErrorLogger =
        new("Bootstrap/logs/error.log") { AutoFlush = true };

    static BootstrapLog() {
        DefaultProvider = LoggerProvider.Create("Bootstrap/logs/bootstrap.log");
        DefaultLogger = (Logger)CreateLogger("Bootstrap");
    }

    public static ILogger CreateLogger(string categoryName, LogLevel level) =>
        DefaultProvider.CreateLogger(categoryName, level);

    public static ILogger CreateLogger(string categoryName) =>
        DefaultProvider.CreateLogger(categoryName);

    public static void Dispose() => DefaultProvider.Dispose();
}