using System;
using AtomicRegistry.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vostok.Configuration;
using Vostok.Configuration.Sources.Json;

namespace AtomicRegistry
{
    public static class Program
    {
        public static void Main(params string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.SetupStorage();
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

        private static void SetupStorage(this WebApplicationBuilder builder)
        {
            var provider = new Vostok.Configuration.ConfigurationProvider();
            provider.SetupSourceFor<StorageSettings>(new JsonFileSource("settings\\storageSettings.json"));
            var settings = provider.Get<StorageSettings>();
            builder.Services.AddScoped(x => settings);
            if (settings?.FilePath == null)
                throw new Exception("Could not retrieve storage settings");
            DirectoryHelper.EnsurePathDirectoriesExist(settings.FilePath);
        }
    }
}