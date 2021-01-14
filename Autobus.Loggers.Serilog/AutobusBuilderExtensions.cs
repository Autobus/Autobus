using Autobus.Loggers.Serilog;
using Serilog;

namespace Autobus
{
    public static class AutobusBuilderExtensions
    {
        public static IAutobusBuilder UseSerilog(this IAutobusBuilder builder) =>
            builder.UseLogger(new AutobusSerilogLogger());

        public static IAutobusBuilder UseSerilog(this IAutobusBuilder builder, ILogger logger) =>
            builder.UseLogger(new AutobusSerilogLogger(logger));
    }
}
