using LiteFocus.Models;
using LiteFocus.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace LiteFocus.Services
{
    public class SceneManager
    {
        private readonly string _path;
        private readonly AppSettings _appSettings;
        public HashSet<string> CurrentWhiteList => _appSettings.WhiteListProcesses;

        private List<Scene> _scenes = new();
        private static readonly HashSet<string> _baselineWhite = new(StringComparer.OrdinalIgnoreCase)
        {
            "LiteFocus.exe"
        };

        public SceneManager(string appDir)
        {
            _path = Path.Combine(appDir, "scenes.json");
            _appSettings = JsonStore.Load(Path.Combine(appDir, "appsettings.json"), new AppSettings());
            // 迁移：移除 explorer.exe 避免文件夹窗口被豁免
            _appSettings.WhiteListProcesses.RemoveWhere(n => n.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase));
            JsonStore.Save(Path.Combine(appDir, "appsettings.json"), _appSettings);
            SeedIfEmpty();
        }

        private void SeedIfEmpty()
        {
            if (!File.Exists(_path))
            {
                // 默认场景（Notion 放到第一位）
                _scenes = new List<Scene>
                {
                    new Scene
                    {
                        Name = "Notion",
                        LaunchApps = { "notion:" },
                        WhiteListProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase){ "Notion.exe","LiteFocus.exe" },
                        Cooldowns = { {"QQ.exe", 25}, {"WeChat.exe",25} }
                    },
                    new Scene
                    {
                        Name = "VS Code工作区",
                        LaunchApps = { "code" },
                        WhiteListProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase){ "devenv.exe","Code.exe","LiteFocus.exe" },
                        Cooldowns = { {"QQ.exe", 20}, {"WeChat.exe",20} }
                    },
                    new Scene
                    {
                        Name = "开会",
                        LaunchApps = { "msteams" },
                        WhiteListProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase){ "LiteFocus.exe","Teams.exe","Zoom.exe" },
                        Cooldowns = { {"QQ.exe", 30}, {"WeChat.exe",30} }
                    }
                };
                JsonStore.Save(_path, _scenes);
            }
            else
            {
                _scenes = JsonStore.Load(_path, new List<Scene>());
                // 迁移：将旧场景名“编码”或“Coding”改成“VS Code工作区”；
                // 若名称包含“code”或启动项包含 code，也统一命名为“VS Code工作区”。
                bool migrated = false;
                foreach (var s in _scenes)
                {
                    bool looksLikeVsCode =
                        s.Name?.IndexOf("编码", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        s.Name?.IndexOf("coding", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        s.Name?.IndexOf("code", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        s.LaunchApps.Exists(a => a.IndexOf("code", StringComparison.OrdinalIgnoreCase) >= 0);
                    if (looksLikeVsCode && !string.Equals(s.Name, "VS Code工作区", StringComparison.OrdinalIgnoreCase))
                    {
                        s.Name = "VS Code工作区";
                        migrated = true;
                    }
                    // 将“写作/Writing”迁移为 Notion，且改为启动 Notion 应用
                    if (s.Name != null && (s.Name.Equals("写作", StringComparison.OrdinalIgnoreCase) || s.Name.Equals("Writing", StringComparison.OrdinalIgnoreCase)))
                    {
                        s.Name = "Notion";
                        s.LaunchApps.Clear();
                        s.LaunchApps.Add("notion:");
                        s.WhiteListProcesses = new HashSet<string>(s.WhiteListProcesses, StringComparer.OrdinalIgnoreCase){ "Notion.exe" };
                        // 移除老的 notepad/winword
                        s.WhiteListProcesses.Remove("notepad.exe");
                        s.WhiteListProcesses.Remove("WINWORD.EXE");
                        migrated = true;
                    }
                }
                // 排序：Notion 放第一
                _scenes = _scenes
                    .OrderByDescending(sc => sc.Name.Equals("Notion", StringComparison.OrdinalIgnoreCase))
                    .ThenBy(sc => sc.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (migrated) JsonStore.Save(_path, _scenes);
            }
        }

        public IList<Scene> LoadScenes() => _scenes;

        public void Activate(Scene sc)
        {
            foreach (var item in sc.LaunchApps)
            {
                try { Process.Start(new ProcessStartInfo(item) { UseShellExecute = true }); }
                catch { }
            }
            // 合并白名单：保留已有 + 场景 + 基线（仅 LiteFocus）
            var merged = new HashSet<string>(_appSettings.WhiteListProcesses, StringComparer.OrdinalIgnoreCase);
            foreach (var w in sc.WhiteListProcesses) merged.Add(w);
            foreach (var b in _baselineWhite) merged.Add(b);
            // 确保不保留 explorer.exe（避免文件夹窗口被整体豁免）
            merged.RemoveWhere(n => n.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase));
            _appSettings.WhiteListProcesses = merged;
            foreach (var kv in sc.Cooldowns) _appSettings.Cooldowns[kv.Key] = kv.Value;
            JsonStore.Save(Path.Combine(Paths.AppDataDir, "appsettings.json"), _appSettings);
        }

        public void ApplySceneSettings(Scene sc)
        {
            var merged = new HashSet<string>(_appSettings.WhiteListProcesses, StringComparer.OrdinalIgnoreCase);
            foreach (var w in sc.WhiteListProcesses) merged.Add(w);
            foreach (var b in _baselineWhite) merged.Add(b);
            merged.RemoveWhere(n => n.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase));
            _appSettings.WhiteListProcesses = merged;
            foreach (var kv in sc.Cooldowns) _appSettings.Cooldowns[kv.Key] = kv.Value;
            JsonStore.Save(Path.Combine(Paths.AppDataDir, "appsettings.json"), _appSettings);
        }

        public bool IsAllowedForeground(string processName) => _appSettings.WhiteListProcesses.Contains(processName);

        public int GetCooldownMinutes(string processName)
            => _appSettings.Cooldowns.TryGetValue(processName, out var m) ? m : 15;

        public bool IsPassiveMediaProcess(string processName)
            => _appSettings.PassiveFocusProcesses.Contains(processName);

        public bool IsPassiveMediaTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return false;
            foreach (var k in _appSettings.PassiveFocusTitleKeywords)
            {
                if (title.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }
    }
}
