using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GptCdrVectorHost
{
    public sealed class Toolbar : UserControl
    {
        public Toolbar()
        {
            Button button = new Button
            {
                Content = "GPT\u77e2\u91cf",
                ToolTip = "\u6253\u5f00 GPT CDR Vector \u9762\u677f",
                MinWidth = 76,
                Height = 26,
                Margin = new Thickness(2, 0, 2, 0),
                Padding = new Thickness(8, 1, 8, 1),
                Background = new SolidColorBrush(Color.FromRgb(245, 250, 255)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(92, 173, 222)),
                Foreground = new SolidColorBrush(Color.FromRgb(20, 76, 110)),
                Focusable = false
            };
            button.Click += OpenPanel;

            Content = button;
        }

        void OpenPanel(object sender, RoutedEventArgs e)
        {
            string folder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string panelPath = Path.Combine(folder, "panel", "app.exe");
            string fallbackPath = Path.Combine(folder, "app.exe");
            string appPath = File.Exists(panelPath) ? panelPath : fallbackPath;
            if (!File.Exists(appPath))
            {
                MessageBox.Show("找不到面板程序：" + Environment.NewLine + panelPath, "GPT CDR Vector", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Process.Start(new ProcessStartInfo(appPath)
            {
                WorkingDirectory = Path.GetDirectoryName(appPath),
                UseShellExecute = true
            });
        }
    }
}
