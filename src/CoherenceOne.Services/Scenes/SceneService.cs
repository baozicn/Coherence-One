using System.Diagnostics;
using System.Linq;
using CoherenceOne.Core.Interfaces;
using CoherenceOne.Core.Models;
using CoherenceOne.Data;
using Microsoft.EntityFrameworkCore;

namespace CoherenceOne.Services.Scenes;

public class SceneService : ISceneService
{
    private readonly AppDbContext _dbContext;
    private readonly IFocusService _focusService;
    private readonly IStatsService _statsService;

    public SceneService(AppDbContext dbContext, IFocusService focusService, IStatsService statsService)
    {
        _dbContext = dbContext;
        _focusService = focusService;
        _statsService = statsService;
    }

    public Scene Load(string id)
    {
        var scene = _dbContext.Scenes.AsNoTracking().FirstOrDefault(s => s.Id == id);
        if (scene == null)
        {
            throw new InvalidOperationException($"Scene {id} not found");
        }

        return scene;
    }

    public void Save(Scene scene)
    {
        var existing = _dbContext.Scenes.FirstOrDefault(s => s.Id == scene.Id);
        if (existing == null)
        {
            _dbContext.Scenes.Add(scene);
        }
        else
        {
            existing.Name = scene.Name;
            existing.LaunchApps = scene.LaunchApps;
            existing.Whitelist = scene.Whitelist;
            existing.CooldownRules = scene.CooldownRules;
            existing.PomodoroDefaults = scene.PomodoroDefaults;
        }

        _dbContext.SaveChanges();
    }

    public void Launch(string id)
    {
        var scene = Load(id);
        foreach (var app in scene.LaunchApps)
        {
            try
            {
                Process.Start(new ProcessStartInfo(app) { UseShellExecute = true });
            }
            catch
            {
                // ignore launching failures to avoid crashing the flow
            }
        }

        _focusService.SetWhitelist(scene.Whitelist);
        _statsService.Mark("scene_launch", new { scene = scene.Id });
    }
}
