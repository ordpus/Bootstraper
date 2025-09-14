using Serilog;
using Serilog.Formatting.Display;

namespace BootstrapApi;

public static class SeriaLogger {
    static SeriaLogger() {
        if (!BootstrapUtility.ShouldIntercept()) BootstrapUtility.RollLogFile("Bootstrap/logs/bootstrap.log");
        Log.Logger = new LoggerConfiguration()
                     .WriteTo
                     .File(
                         new MessageTemplateTextFormatter(
                             "[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Level}] [{Source}] {Message}{NewLine}{Exception}"),
                         "Bootstrap/logs/bootstrap.log")
                     .CreateLogger();
    }
}