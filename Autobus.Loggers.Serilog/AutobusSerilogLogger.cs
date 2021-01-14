using System;
using Autobus.Abstractions.Abstractions;
using Serilog;

namespace Autobus.Loggers.Serilog
{
    public class AutobusSerilogLogger : IAutobusLogger
    {
        private ILogger _logger;

        public AutobusSerilogLogger(ILogger logger) => _logger = logger.ForContext<IAutobus>();

        public AutobusSerilogLogger() => _logger = Log.ForContext<IAutobus>();

        public void Verbose(string message) => _logger.Verbose(message);
        
        public void Debug(string message) => _logger.Debug(message);

        public void Information(string message) => _logger.Information(message);

        public void Warning(string message) => _logger.Warning(message);

        public void Error(string message) => _logger.Error(message);

        public void Error(Exception e) => _logger.Error(e, "error");

        public void Fatal(string message) => _logger.Fatal(message);

        public void Fatal(Exception e) => _logger.Fatal(e, "fatal error");
    }
}