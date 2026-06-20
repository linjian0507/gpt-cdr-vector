using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using GptCdrVectorShared;

namespace GptCdrVectorInstaller
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            if (args.Any(a => string.Equals(a, "--self-test", StringComparison.OrdinalIgnoreCase)))
            {
                InstallerCore.DetectTargets();
                return InstallerCore.FindPackageDir().Length > 0 ? 0 : 1;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new InstallerForm());
            return 0;
        }
    }

    sealed class InstallTarget
    {
        public string Label;
        public string VersionKey;
        public string AddonsRoot;
        public string Source;

        public override string ToString()
        {
            return string.Format("{0}    {1}", Label, AddonsRoot);
        }
    }

    static class InstallerCore
    {
        public const string AddonFolderName = "gpt-cdr-vector";

        public static string FindPackageDir()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidates =
            {
                Path.Combine(baseDir, AddonFolderName),
                baseDir
            };

            foreach (string candidate in candidates)
            {
                if (HasPackageFiles(candidate)) return candidate;
            }
            return "";
        }

        public static bool HasPackageFiles(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return false;
            string[] files = { "app.exe", "CorelDrw.addon", "AppUI.xslt", "GptCdrVectorHost.dll" };
            return files.All(file => File.Exists(Path.Combine(folder, file)));
        }

        public static List<InstallTarget> DetectTargets()
        {
            List<InstallTarget> targets = new List<InstallTarget>();
            DetectComTargets(targets);
            DetectUninstallTargets(targets);
            return targets
                .GroupBy(t => t.AddonsRoot.ToLowerInvariant())
                .Select(g => g.First())
                .OrderBy(t => t.Label)
                .ToList();
        }

        static void DetectComTargets(List<InstallTarget> targets)
        {
            for (int version = 10; version <= 30; version++)
            {
                try
                {
                    string clsid = "";
                    using (RegistryKey clsidKey = Registry.ClassesRoot.OpenSubKey(@"CorelDRAW.Application." + version + @"\CLSID"))
                    {
                        if (clsidKey != null) clsid = Convert.ToString(clsidKey.GetValue(""));
                    }
                    if (string.IsNullOrWhiteSpace(clsid)) continue;

                    string command = "";
                    using (RegistryKey serverKey = Registry.ClassesRoot.OpenSubKey(@"CLSID\" + clsid + @"\LocalServer32"))
                    {
                        if (serverKey != null) command = Convert.ToString(serverKey.GetValue(""));
                    }
                    string exePath = ExtractExePath(command);
                    if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath)) continue;

                    string programDir = Path.GetDirectoryName(exePath);
                    AddTarget(targets, VersionLabel(version.ToString()), version.ToString(), programDir, "COM");
                }
                catch
                {
                }
            }
        }

        static void DetectUninstallTargets(List<InstallTarget> targets)
        {
            foreach (RegistryHive hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
            {
                foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
                {
                    try
                    {
                        using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view))
                        using (RegistryKey root = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
                        {
                            if (root == null) continue;
                            foreach (string name in root.GetSubKeyNames())
                            {
                                using (RegistryKey key = root.OpenSubKey(name))
                                {
                                    if (key == null) continue;
                                    string displayName = Convert.ToString(key.GetValue("DisplayName"));
                                    if (string.IsNullOrWhiteSpace(displayName) || displayName.IndexOf("CorelDRAW", StringComparison.OrdinalIgnoreCase) < 0) continue;

                                    string installLocation = Convert.ToString(key.GetValue("InstallLocation"));
                                    string displayIcon = Convert.ToString(key.GetValue("DisplayIcon"));
                                    string programDir = ProgramDirFromInstallInfo(installLocation, displayIcon);
                                    if (string.IsNullOrWhiteSpace(programDir)) continue;

                                    string versionKey = GuessVersionKey(displayName, programDir);
                                    AddTarget(targets, DisplayLabel(displayName, versionKey), versionKey, programDir, "Registry");
                                }
                            }
                        }
                    }
                    catch
                    {
                    }
                }
            }
        }

        static string ProgramDirFromInstallInfo(string installLocation, string displayIcon)
        {
            foreach (string path in new[] { installLocation, displayIcon })
            {
                if (string.IsNullOrWhiteSpace(path)) continue;
                string cleaned = ExtractExePath(path);
                if (File.Exists(cleaned) && string.Equals(Path.GetFileName(cleaned), "CorelDRW.exe", StringComparison.OrdinalIgnoreCase))
                    return Path.GetDirectoryName(cleaned);

                string folder = cleaned.Trim('"');
                string direct = Path.Combine(folder, "CorelDRW.exe");
                if (File.Exists(direct)) return folder;

                string programs64 = Path.Combine(folder, "Programs64", "CorelDRW.exe");
                if (File.Exists(programs64)) return Path.GetDirectoryName(programs64);
            }
            return "";
        }

        static void AddTarget(List<InstallTarget> targets, string label, string versionKey, string programDir, string source)
        {
            string addonsRoot = NormalizeAddonsRoot(programDir);
            if (string.IsNullOrWhiteSpace(addonsRoot) || !Directory.Exists(addonsRoot)) return;

            targets.Add(new InstallTarget
            {
                Label = label,
                VersionKey = versionKey,
                AddonsRoot = addonsRoot,
                Source = source
            });
        }

        public static string NormalizeAddonsRoot(string selectedPath)
        {
            if (string.IsNullOrWhiteSpace(selectedPath)) return "";
            string path = selectedPath.Trim().Trim('"');
            if (File.Exists(path)) path = Path.GetDirectoryName(path);
            if (!Directory.Exists(path)) return "";

            if (string.Equals(Path.GetFileName(path), "Addons", StringComparison.OrdinalIgnoreCase)) return path;

            string direct = Path.Combine(path, "Addons");
            if (Directory.Exists(direct)) return direct;

            string programs64 = Path.Combine(path, "Programs64", "Addons");
            if (Directory.Exists(programs64)) return programs64;

            string programs = Path.Combine(path, "Programs", "Addons");
            if (Directory.Exists(programs)) return programs;

            return "";
        }

        public static void Install(string packageDir, InstallTarget target)
        {
            if (!HasPackageFiles(packageDir)) throw new InvalidOperationException("插件包不完整：" + packageDir);
            string targetDir = Path.Combine(target.AddonsRoot, AddonFolderName);

            if (Directory.Exists(targetDir)) Directory.Delete(targetDir, true);
            CopyDirectory(packageDir, targetDir);

            if (!string.IsNullOrWhiteSpace(target.VersionKey))
            {
                File.WriteAllText(Path.Combine(targetDir, "corel_version.txt"), target.VersionKey);
            }
        }

        public static void Uninstall(InstallTarget target)
        {
            string targetDir = Path.Combine(target.AddonsRoot, AddonFolderName);
            if (Directory.Exists(targetDir)) Directory.Delete(targetDir, true);
        }

        static void CopyDirectory(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (string dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(Path.Combine(target, dir.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
            }
            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string relative = file.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                File.Copy(file, Path.Combine(target, relative), true);
            }
        }

        static string ExtractExePath(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return "";
            string text = command.Trim();
            if (text.StartsWith("\""))
            {
                int end = text.IndexOf('"', 1);
                return end > 1 ? text.Substring(1, end - 1) : text.Trim('"');
            }

            int exeIndex = text.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (exeIndex >= 0) return text.Substring(0, exeIndex + 4).Trim();
            return text;
        }

        static string VersionLabel(string versionKey)
        {
            Dictionary<string, string> labels = new Dictionary<string, string>
            {
                {"14", "X4"},
                {"15", "X5"},
                {"16", "X6"},
                {"17", "X7"},
                {"18", "X8"},
                {"19", "2017(64)"},
                {"20", "2018(64)"},
                {"21", "2019(64)"},
                {"22", "2020(64)"},
                {"23", "2021(64)"},
                {"24", "2022(64)"},
                {"25", "2023(64)"},
                {"26", "2024(64)"},
                {"27", "2025(64)"},
                {"28", "2026(64)"}
            };
            return labels.ContainsKey(versionKey) ? labels[versionKey] : "CorelDRAW " + versionKey;
        }

        static string DisplayLabel(string displayName, string versionKey)
        {
            if (!string.IsNullOrWhiteSpace(versionKey)) return VersionLabel(versionKey);
            return displayName;
        }

        static string GuessVersionKey(string displayName, string programDir)
        {
            string text = (displayName + " " + programDir).ToLowerInvariant();
            string[,] map =
            {
                {"2026", "28"}, {"2025", "27"}, {"2024", "26"}, {"2023", "25"}, {"2022", "24"},
                {"2021", "23"}, {"2020", "22"}, {"2019", "21"}, {"2018", "20"}, {"2017", "19"},
                {"x8", "18"}, {"x7", "17"}, {"x6", "16"}, {"x5", "15"}, {"x4", "14"}
            };
            for (int i = 0; i < map.GetLength(0); i++)
            {
                if (text.Contains(map[i, 0])) return map[i, 1];
            }
            return "";
        }
    }

    sealed class InstallerForm : Form
    {
        readonly CheckedListBox targetList = new CheckedListBox();
        readonly TextBox pathBox = new TextBox();
        readonly Label statusLabel = new Label();
        readonly string packageDir;

        public InstallerForm()
        {
            packageDir = InstallerCore.FindPackageDir();

            Text = "GPT CDR Vector 安装器";
            Width = 560;
            Height = 430;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;

            Panel header = new Panel { Left = 0, Top = 0, Width = 560, Height = 92, BackColor = Color.FromArgb(73, 143, 232) };
            Label title = new Label { Left = 22, Top = 20, Width = 290, Height = 34, Text = "GPT CDR Vector", ForeColor = Color.White, Font = new Font("Microsoft YaHei UI", 20, FontStyle.Bold) };
            Label sub = new Label { Left = 24, Top = 58, Width = 280, Height = 22, Text = "CorelDRAW 固定工具栏插件", ForeColor = Color.White, Font = new Font("Microsoft YaHei UI", 10, FontStyle.Italic) };
            Label version = new Label { Left = 430, Top = 27, Width = 92, Height = 44, Text = "VIP\n1.0", ForeColor = Color.White, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Microsoft YaHei UI", 14, FontStyle.Bold) };
            header.Controls.Add(title);
            header.Controls.Add(sub);
            header.Controls.Add(version);
            Controls.Add(header);

            Label listLabel = new Label { Left = 22, Top = 112, Width = 260, Height = 22, Text = "可选择安装多个版本" };
            Controls.Add(listLabel);

            LinkLabel clearLink = new LinkLabel { Left = 178, Top = 112, Width = 80, Height = 22, Text = "取消打勾" };
            clearLink.Click += delegate { SetAllChecked(false); };
            Controls.Add(clearLink);

            targetList.Left = 22;
            targetList.Top = 138;
            targetList.Width = 330;
            targetList.Height = 130;
            targetList.CheckOnClick = true;
            Controls.Add(targetList);

            Label other = new Label { Left = 22, Top = 285, Width = 70, Height = 22, Text = "其他路径" };
            Controls.Add(other);
            pathBox.Left = 92;
            pathBox.Top = 281;
            pathBox.Width = 226;
            Controls.Add(pathBox);
            Button browseButton = new Button { Left = 323, Top = 279, Width = 29, Height = 26, Text = "..." };
            browseButton.Click += delegate { BrowsePath(); };
            Controls.Add(browseButton);

            Label hint = new Label { Left = 22, Top = 315, Width = 338, Height = 22, Text = "CDR目录 如：...\\CorelDRAW Graphics Suite 或 Programs64\\Addons", ForeColor = Color.Blue };
            Controls.Add(hint);

            Button installButton = MakeButton("安装\n插件", 382, 120);
            installButton.Click += delegate { InstallSelected(); };
            Controls.Add(installButton);

            Button uninstallButton = MakeButton("卸载\n插件", 382, 180);
            uninstallButton.Click += delegate { UninstallSelected(); };
            Controls.Add(uninstallButton);

            Button refreshButton = MakeButton("刷新\n检测", 382, 240);
            refreshButton.Click += delegate { LoadTargets(); };
            Controls.Add(refreshButton);

            Button openButton = MakeButton("打开\n目录", 464, 120);
            openButton.Click += delegate { OpenSelectedDir(); };
            Controls.Add(openButton);

            Button docButton = MakeButton("安装\n说明", 464, 180);
            docButton.Click += delegate { OpenReadme(); };
            Controls.Add(docButton);

            Button settingsButton = MakeButton("设置\nAPI", 464, 240);
            settingsButton.Click += delegate { OpenApiSettings(); };
            Controls.Add(settingsButton);

            Button closeButton = MakeButton("关闭", 464, 300);
            closeButton.Click += delegate { Close(); };
            Controls.Add(closeButton);

            statusLabel.Left = 22;
            statusLabel.Top = 355;
            statusLabel.Width = 500;
            statusLabel.Height = 36;
            statusLabel.ForeColor = Color.Blue;
            Controls.Add(statusLabel);

            LoadTargets();
        }

        Button MakeButton(string text, int left, int top)
        {
            return new Button
            {
                Left = left,
                Top = top,
                Width = 70,
                Height = 48,
                Text = text,
                ForeColor = Color.Blue
            };
        }

        void LoadTargets()
        {
            targetList.Items.Clear();
            foreach (InstallTarget target in InstallerCore.DetectTargets())
            {
                targetList.Items.Add(target, true);
            }

            statusLabel.Text = targetList.Items.Count > 0
                ? "已自动识别 CorelDRAW 安装目录。安装前请先关闭 CorelDRAW。"
                : "未自动识别到 CorelDRAW，可通过“其他路径”手动选择。";

            if (string.IsNullOrWhiteSpace(packageDir))
            {
                statusLabel.Text = "未找到 gpt-cdr-vector 插件包，请把安装器放在插件包同级目录。";
            }
        }

        void SetAllChecked(bool value)
        {
            for (int i = 0; i < targetList.Items.Count; i++)
            {
                targetList.SetItemChecked(i, value);
            }
        }

        void BrowsePath()
        {
            FolderBrowserDialog dialog = new FolderBrowserDialog();
            dialog.Description = "请选择 CorelDRAW 安装目录或 Programs64\\Addons 目录";
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            pathBox.Text = dialog.SelectedPath;

            string addonsRoot = InstallerCore.NormalizeAddonsRoot(dialog.SelectedPath);
            if (string.IsNullOrWhiteSpace(addonsRoot))
            {
                MessageBox.Show(this, "该目录下没有找到 Addons 目录。", "GPT CDR Vector", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            InstallTarget target = new InstallTarget
            {
                Label = "手动路径",
                VersionKey = "",
                AddonsRoot = addonsRoot,
                Source = "Manual"
            };
            targetList.Items.Add(target, true);
            statusLabel.Text = "已添加手动路径：" + addonsRoot;
        }

        IEnumerable<InstallTarget> CheckedTargets()
        {
            foreach (object item in targetList.CheckedItems)
            {
                InstallTarget target = item as InstallTarget;
                if (target != null) yield return target;
            }
        }

        InstallTarget CurrentTarget()
        {
            InstallTarget selected = targetList.SelectedItem as InstallTarget;
            return selected ?? CheckedTargets().FirstOrDefault();
        }

        void InstallSelected()
        {
            try
            {
                List<InstallTarget> targets = CheckedTargets().ToList();
                if (targets.Count == 0) throw new InvalidOperationException("请先勾选要安装的 CorelDRAW 版本。");
                if (string.IsNullOrWhiteSpace(packageDir)) throw new InvalidOperationException("找不到插件包目录。");

                foreach (InstallTarget target in targets)
                {
                    InstallerCore.Install(packageDir, target);
                }
                statusLabel.Text = "安装完成。请重启 CorelDRAW；若未显示工具栏，请按住 F8 启动重置工作区。";
                MessageBox.Show(this, "安装完成。", "GPT CDR Vector", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                statusLabel.Text = "安装失败：" + ex.Message;
                MessageBox.Show(this, ex.Message, "GPT CDR Vector", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void UninstallSelected()
        {
            try
            {
                List<InstallTarget> targets = CheckedTargets().ToList();
                if (targets.Count == 0) throw new InvalidOperationException("请先勾选要卸载的 CorelDRAW 版本。");
                foreach (InstallTarget target in targets)
                {
                    InstallerCore.Uninstall(target);
                }
                statusLabel.Text = "卸载完成。请重启 CorelDRAW。";
                MessageBox.Show(this, "卸载完成。", "GPT CDR Vector", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                statusLabel.Text = "卸载失败：" + ex.Message;
                MessageBox.Show(this, ex.Message, "GPT CDR Vector", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void OpenSelectedDir()
        {
            InstallTarget target = CurrentTarget();
            if (target == null) return;
            Process.Start("explorer.exe", target.AddonsRoot);
        }

        void OpenReadme()
        {
            string readme = string.IsNullOrWhiteSpace(packageDir) ? "" : Path.Combine(packageDir, "README_INSTALL.txt");
            if (File.Exists(readme)) Process.Start(readme);
        }

        void OpenApiSettings()
        {
            if (ApiSettingsForm.ShowDialogAndSave(this))
            {
                statusLabel.Text = "API 设置已保存。安装后插件面板会读取最新配置。";
            }
        }
    }
}
