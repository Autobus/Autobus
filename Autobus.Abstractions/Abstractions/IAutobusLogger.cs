using System;

namespace Autobus.Abstractions.Abstractions
{
    public interface IAutobusLogger
    {
        void Verbose(string message);

        void Debug(string message);

        void Information(string message);
        
        void Warning(string message);

        void Error(string message);

        void Error(Exception e) => Error(e.ToString());
        
        void Fatal(string message);

        void Fatal(Exception e) => Fatal(e.ToString());
    }
}