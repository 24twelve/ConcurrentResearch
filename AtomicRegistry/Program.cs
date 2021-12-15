using System;
using System.Linq;
using AtomicRegistry.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vostok.Configuration.Sources.Yaml;

namespace AtomicRegistry
{
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
            builder.Services.AddScoped(_ => new InstanceName(instanceName));

            //todo: if not development, instance name comes from settings

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
            var provider = new Vostok.Configuration.ConfigurationProvider();
            provider.SetupSourceFor<StorageSettings>(new YamlFileSource("settings\\storageSettings.yaml"));
            var settings = provider.Get<StorageSettings>();
            builder.Services.AddScoped(x => settings);
            if (settings == null)
                throw new Exception("Could not retrieve storage settings");

            DirectoryHelper.EnsurePathDirectoriesExist(settings.InstanceNameFilePath[instanceName]);
        }
    }
}