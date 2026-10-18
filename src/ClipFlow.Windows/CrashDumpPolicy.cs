using Microsoft.Win32;

namespace ClipFlow.Windows;

/// <summary>
/// 異常終了時に画像を含むメモリダンプが書き出される設定が有効になっていないかを調べる。
/// ClipFlow 自身はダンプを有効化する設定を提供しない。
/// </summary>
public static class CrashDumpPolicy
{
    private const string LocalDumpsKey = @"SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps";
    private static readonly string[] DumpVariables = ["DOTNET_DbgEnableMiniDump", "COMPlus_DbgEnableMiniDump"];

    /// <summary>有効になっているダンプ設定の説明。何もなければ空。</summary>
    public static IReadOnlyList<string> FindEnabledDumpSettings()
    {
        var findings = new List<string>();
        foreach (var name in DumpVariables)
        {
            if (Environment.GetEnvironmentVariable(name) is { } value && value.Trim() != "0" && value.Length > 0)
            {
                findings.Add($"環境変数 {name}={value}");
            }
        }

        foreach (var (hive, label) in new[] { (Registry.LocalMachine, "HKLM"), (Registry.CurrentUser, "HKCU") })
        {
            try
            {
                using var key = hive.OpenSubKey(LocalDumpsKey);
                if (key is not null)
                {
                    findings.Add($@"Windows エラー報告の LocalDumps（{label}\{LocalDumpsKey}）");
                }
            }
            catch (System.Security.SecurityException)
            {
                // 読めない場合は判定しない
            }
        }

        return findings;
    }
}
