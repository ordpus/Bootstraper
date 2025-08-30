using System.Buffers;
using System.Collections.Concurrent;
using System.Threading.Channels;

using Microsoft.Extensions.Logging;

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
    private const string ErrorLog = "Bootstrap/error.log";

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
        StreamWriter stream, int maxQueueSize = 1000, int batchSize = 128, int batchTimeoutMillisecond = 100) {
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
                File.AppendAllLines("Bootstrap/error.log", [e.ToString()]);
                throw;
            }
        });
    }

    public bool TryPublish(string content) {
        return !_disposed && _writer.TryWrite(content);
    }

    private async Task ConsumerLoop(CancellationToken cancellation) {
        try {
            var count = 0;

            while (!cancellation.IsCancellationRequested) {
                if (count >= _batchSize) {
                    await _stream.FlushAsync();
                    count = 0;
                }

                using var readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                var readCancel = readCts.Token;
                var readTask = _reader.WaitToReadAsync(readCancel).AsTask();
                var timeoutCancel = timeoutCts.Token;
                var timeoutTask = Task.Delay(_batchTimeout, timeoutCancel);

                var resultTask = await Task.WhenAny(readTask, timeoutTask).ConfigureAwait(false);
                if (resultTask == readTask) {
                    timeoutCts.Cancel();
                    var readSuccessful = await readTask;
                    if (!readSuccessful) return;
                    while (_reader.TryRead(out var item)) {
                        count++;
                        await Process(item);
                    }
                } else {
                    readCts.Cancel();
                    await _stream.FlushAsync();
                }
            }

            await _stream.FlushAsync();
        } catch (OperationCanceledException) { } catch (Exception e) {
            File.AppendAllLines(ErrorLog, [e.ToString()]);
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
            File.AppendAllLines(ErrorLog, [e.ToString()]);
        } finally {
            _pool.Return(result);
        }
    }

    public void Dispose() {
        if (_disposed) return;
        _disposed = true;
        _writer.Complete();
        _cancellation.Cancel();
        _task.Wait(TimeSpan.FromSeconds(5));
        _stream.Dispose();
        _cancellation.Dispose();
    }
}

public class LoggerProvider(IAsyncStreamWriter writer, int categoryLength = 10) : ILoggerProvider {
    private readonly ConcurrentDictionary<string, Logger> _loggers = new();

    public LoggerProvider(string filePath, int categoryLength = 10) : this(
        new AsyncQueuedStreamWriter(stream: new StreamWriter(filePath)),
        categoryLength) {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);
    }

    public void Dispose() {
        _loggers.Clear();
        writer.Dispose();
    }

    public ILogger CreateLogger(string categoryName, LogLevel level) {
        return _loggers.GetOrAdd(categoryName, name => new Logger(name, level, writer, categoryLength));
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
            : _categoryName.PadRight(categoryLength - _categoryName.Length);
        writer.TryPublish($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{cn}] [{logLevel}] {message}");

        if (exception != null) writer.TryPublish(exception.ToString());
    }

    public bool IsEnabled(LogLevel logLevel) {
        return level <= logLevel;
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull {
        if (state == null) {
            throw new ArgumentNullException(nameof(state));
        }

        return NullScope.Instance;
    }
}

public static class BootstrapLog {
    // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
    private static readonly LoggerProvider Provider;
    public static readonly Logger Logger;

    static BootstrapLog() {
        Provider = new LoggerProvider("Bootstrap/bootstrap.log");
        Logger = (Logger)Provider.CreateLogger("Bootstrap");
    }
}