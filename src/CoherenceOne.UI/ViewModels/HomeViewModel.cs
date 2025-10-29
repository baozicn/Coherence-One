using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CoherenceOne.Core.Interfaces;
using CoherenceOne.Core.Models;
using CoherenceOne.Data.Services;

namespace CoherenceOne.UI.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    private readonly IFocusService _focusService;
    private readonly IPomodoroService _pomodoroService;
    private readonly ISceneService _sceneService;
    private readonly IStatsService _statsService;
    private readonly DataExportService _exportService;

    [ObservableProperty]
    private string _ringText = "5:00";

    [ObservableProperty]
    private ObservableCollection<Scene> _scenes = new();

    [ObservableProperty]
    private ObservableCollection<TimelineEvent> _timeline = new();

    public IRelayCommand StartMicroActionCommand { get; }
    public IRelayCommand<string> LaunchSceneCommand { get; }
    public IRelayCommand ExportTimelineCommand { get; }

    public HomeViewModel(
        IFocusService focusService,
        IPomodoroService pomodoroService,
        ISceneService sceneService,
        IStatsService statsService,
        DataExportService exportService)
    {
        _focusService = focusService;
        _pomodoroService = pomodoroService;
        _sceneService = sceneService;
        _statsService = statsService;
        _exportService = exportService;

        StartMicroActionCommand = new AsyncRelayCommand(StartMicroActionAsync);
        LaunchSceneCommand = new RelayCommand<string>(LaunchScene);
        ExportTimelineCommand = new AsyncRelayCommand(ExportTimelineAsync);

        _pomodoroService.Tick += OnPomodoroTick;
        _pomodoroService.PhaseEnded += OnPhaseEnded;
    }

    public void Initialize(IEnumerable<Scene> scenes, IEnumerable<TimelineEvent> timeline)
    {
        Scenes = new ObservableCollection<Scene>(scenes);
        Timeline = new ObservableCollection<TimelineEvent>(timeline);
    }

    private async Task StartMicroActionAsync()
    {
        _focusService.ClearScreen();
        _pomodoroService.Start(new PomodoroConfig(300, 300, 900));
        var evt = new TimelineEvent { Ts = DateTime.UtcNow, Type = "micro_start", PayloadJson = string.Empty };
        _statsService.Mark(evt.Type);
        Timeline.Add(evt);
        await Task.CompletedTask;
    }

    private void LaunchScene(string? sceneId)
    {
        if (string.IsNullOrEmpty(sceneId))
        {
            return;
        }
        _sceneService.Launch(sceneId);
        Timeline.Add(new TimelineEvent { Ts = DateTime.UtcNow, Type = "scene_launch", PayloadJson = sceneId });
    }

    private async Task ExportTimelineAsync()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var exportFolder = Path.Combine(documents, "CoherenceOne");
        Directory.CreateDirectory(exportFolder);
        var md = await _exportService.ExportTodayMarkdownAsync();
        var json = await _exportService.ExportTodayJsonAsync();
        await File.WriteAllTextAsync(Path.Combine(exportFolder, $"timeline_{DateTime.Today:yyyyMMdd}.md"), md, Encoding.UTF8);
        await File.WriteAllTextAsync(Path.Combine(exportFolder, $"timeline_{DateTime.Today:yyyyMMdd}.json"), json, Encoding.UTF8);
    }

    private void OnPomodoroTick(object? sender, (int leftSec, string phase) e)
    {
        var minutes = e.leftSec / 60;
        var seconds = e.leftSec % 60;
        RingText = $"{minutes:00}:{seconds:00}";
    }

    private void OnPhaseEnded(object? sender, string phase)
    {
        var evt = new TimelineEvent { Ts = DateTime.UtcNow, Type = $"pomodoro_{phase}_end", PayloadJson = string.Empty };
        _statsService.Mark(evt.Type);
        Timeline.Add(evt);
    }
}
