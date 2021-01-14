using System;
using Autobus.Abstractions.Abstractions;

namespace Autobus.Implementations
{
    public class AutobusConsoleLogger : IAutobusLogger
    {
        private void WriteMessage(string level, string message) =>
            Console.WriteLine($"{level}: {message}");

        public void Verbose(string message) => WriteMessage("VERBOSE", message);

        public void Debug(string message) => WriteMessage("DEBUG", message);

        public void Information(string message) => WriteMessage("INFO", message);

        public void Warning(string message) => WriteMessage("WARNING", message);

        public void Error(string message) => WriteMessage("ERROR", message);

        public void Fatal(string message)
        {
            WriteMessage("FATAL", message);
            Environment.Exit(1);
        }
    }
}