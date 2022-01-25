using AtomicRegister.Configuration;
using AtomicRegister.Controllers;
using AtomicRegister.Dto;
using Vostok.Configuration.Sources.Yaml;
using Vostok.Logging.Abstractions;
using Vostok.Logging.Console;
using Vostok.Logging.File;
using Vostok.Logging.File.Configuration;
using ConfigurationProvider = Vostok.Configuration.ConfigurationProvider;

namespace AtomicRegister;

public static class Program
{
    public static void Main(params string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        Console.WriteLine($"Args: {string.Join(",", args)}");
        Console.WriteLine($"Used url: {builder.WebHost.GetSetting("urls")}");

        var instanceArg = args.FirstOrDefault(x => x.StartsWith("--instance="));
        var instanceName = instanceArg != null ? instanceArg.Split('=')[1] : "default";

        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.SetupStorage(instanceName);
        builder.SetupFaultSettings();
        builder.SetupLogging(instanceName);
        builder.Services.AddSingleton(new ConcurrentCounter());

        //todo: find if there is auto-discover for microsoft DI

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();
        app.UseAuthorization();
        app.MapControllers();
        app.Run();
    }

    private static void SetupStorage(this WebApplicationBuilder builder, string instanceName)
    {
        var provider = new ConfigurationProvider();
        provider.SetupSourceFor<StorageSettings>(new YamlFileSource("settings\\storageSettings.yaml"));
        var settings = provider.Get<StorageSettings>();
        if (settings == null)
            throw new Exception("Could not retrieve storage settings");
        builder.Services.AddSingleton(_ => settings);
        DirectoryHelper.EnsurePathDirectoriesExist(settings.InstanceNameFilePath[instanceName]);

        var database = new Database(settings, instanceName);
        builder.Services.AddSingleton(database);
    }

    private static void SetupFaultSettings(this WebApplicationBuilder builder)
    {
        var faultSettingsProvider = new FaultSettingsProvider(FaultSettingsDto.EverythingOk);
        builder.Services.AddSingleton(faultSettingsProvider);
        builder.Services.AddSingleton(new FaultSettingsObserver(faultSettingsProvider));
    }

    private static void SetupLogging(this WebApplicationBuilder builder, string instanceName)
    {
        var consoleLog = new ConsoleLog();
        var fileLogSettings = new FileLogSettings { FilePath = $"LocalRuns\\test-log-{instanceName}.txt" };
        var fileLog = new FileLog(fileLogSettings);
        builder.Services.AddSingleton<ILog>(new CompositeLog(consoleLog, fileLog));
    }
}