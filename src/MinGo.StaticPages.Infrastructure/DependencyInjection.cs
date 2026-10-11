using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MinGo.StaticPages.Application.Abstractions;
using MinGo.StaticPages.Application.Services;
using MinGo.StaticPages.Application.Storage;
using MinGo.StaticPages.Infrastructure.Data;
using MinGo.StaticPages.Infrastructure.Storage;

namespace MinGo.StaticPages.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMinGoInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(connectionString));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddSingleton<IObjectStorage, ObjectStorageRouter>();
        services.AddScoped<IStorageSettingsService, StorageSettingsService>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        services.AddScoped<ProjectService>();
        services.AddScoped<VersionService>();
        services.AddScoped<ReleaseService>();
        services.AddScoped<ResolveService>();
        services.AddScoped<AuthService>();

        return services;
    }
}
