using System;
using System.Collections.Generic;

namespace LiteFocus.Models
{
    public class AppSettings
    {
        public HashSet<string> WhiteListProcesses { get; set; } = new(StringComparer.OrdinalIgnoreCase)
        {
            "LiteFocus.exe"
        };

        // 当长时间无操作时，如果前台窗口符合以下进程/标题特征，认为是“被动专注”（观看/预览），避免触发清屏
        public HashSet<string> PassiveFocusProcesses { get; set; } = new(StringComparer.OrdinalIgnoreCase)
        {
            // 浏览器
            "msedge.exe","chrome.exe","firefox.exe","opera.exe","qqbrowser.exe",
            // 播放器/剪辑
            "vlc.exe","mpv.exe","PotPlayerMini64.exe","PotPlayerMini.exe","QQPlayer.exe",
            "Resolve.exe","Adobe Premiere Pro.exe","AfterFX.exe","vegas130.exe","vegas140.exe","vegas150.exe","vegas160.exe","vegas170.exe","vegas180.exe"
        };

        public List<string> PassiveFocusTitleKeywords { get; set; } = new()
        {
            "bilibili","哔哩哔哩","B站","youtube","YouTube","腾讯视频","爱奇艺","优酷","抖音","douyin","网课","课堂","讲座","直播","课程","学习"
        };

        public Dictionary<string,int> Cooldowns { get; set; } = new(StringComparer.OrdinalIgnoreCase)
        {
            {"QQ.exe",15},
            {"WeChat.exe",15},
            {"WXWork.exe",15}
        };
    }
}
