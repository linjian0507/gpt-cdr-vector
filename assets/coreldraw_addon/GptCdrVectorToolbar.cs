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
                Content = "GPT矢量",
                ToolTip = "打开 GPT CDR Vector",
                MinWidth = 76,
                Height = 28,
                Margin = new Thickness(2, 0, 2, 0),
                Padding = new Thickness(8, 2, 8, 2),
                Background = new SolidColorBrush(Color.FromRgb(245, 250, 255)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(92, 173, 222)),
                Foreground = new SolidColorBrush(Color.FromRgb(20, 76, 110))
            };
            button.Click += OpenPanel;

            StackPanel panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            panel.Children.Add(button);
            Content = panel;
        }

        void OpenPanel(object sender, RoutedEventArgs e)
        {
            string folder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string appPath = Path.Combine(folder, "app.exe");
            if (!File.Exists(appPath))
            {
                MessageBox.Show("找不到 app.exe：" + Environment.NewLine + appPath, "GPT CDR Vector", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ProcessStartInfo startInfo = new ProcessStartInfo(appPath)
            {
                WorkingDirectory = folder,
                UseShellExecute = true
            };
            Process.Start(startInfo);
        }
    }
}
