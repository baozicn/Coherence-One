using System.IO;
using System.Linq;
using System.Windows;
using CoherenceOne.App.Views;
using CoherenceOne.Core.Interfaces;
using CoherenceOne.Data;
using CoherenceOne.Data.Services;
using CoherenceOne.Services.Focus;
using CoherenceOne.Services.Journal;
using CoherenceOne.Services.Pomodoro;
using CoherenceOne.Services.Scenes;
using CoherenceOne.Services.Stats;
using CoherenceOne.Services.System;
using CoherenceOne.UI.ViewModels;
using CoherenceOne.UI.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CoherenceOne.App;

public partial class App : Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseSqlite($"Data Source={Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CoherenceOne", "app.db")}");
                }, ServiceLifetime.Singleton);

                services.AddSingleton<ISystemWatcher, SystemWatcher>();
                services.AddSingleton<IFocusService, FocusService>();
                services.AddSingleton<IStatsService, StatsService>();
                services.AddSingleton<IJournalService, JournalService>();
                services.AddSingleton<DataExportService>();
                services.AddSingleton<IPomodoroService, PomodoroService>();
                services.AddSingleton<ISceneService, SceneService>();

                services.AddSingleton<HomeViewModel>();
                services.AddSingleton<HomeView>();
                services.AddTransient<MainWindow>();
            })
            .Build();

        _host.Start();

        using var scope = _host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();

        var viewModel = scope.ServiceProvider.GetRequiredService<HomeViewModel>();
        var scenes = db.Scenes.AsNoTracking().ToList();
        var timeline = db.Timeline.AsNoTracking().OrderBy(e => e.Ts).ToList();
        viewModel.Initialize(scenes, timeline);
        var mainWindow = scope.ServiceProvider.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
        base.OnExit(e);
    }
}
