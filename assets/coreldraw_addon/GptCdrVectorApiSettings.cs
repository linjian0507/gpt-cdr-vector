using System;
using System.Drawing;
using System.Windows.Forms;

namespace GptCdrVectorShared
{
    public sealed class ApiSettingsForm : Form
    {
        const string ChatUrl = "https://ai.opendoor.sbs/v1/chat/completions";

        readonly TextBox keyBox = new TextBox();
        readonly TextBox apiUrlBox = new TextBox();
        readonly ComboBox modelBox = new ComboBox();
        readonly NumericUpDown timeoutBox = new NumericUpDown();
        readonly Label statusLabel = new Label();

        public ApiSettingsForm()
        {
            Text = "API 设置";
            Width = 520;
            Height = 330;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            AddLabel("中转 API Key", 22, 25, 120);
            keyBox.SetBounds(150, 22, 330, 24);
            keyBox.PasswordChar = '*';
            keyBox.Text = ReadEnv("OPENAI_RELAY_API_KEY");
            Controls.Add(keyBox);

            AddLabel("接口地址", 22, 70, 120);
            apiUrlBox.SetBounds(150, 67, 330, 24);
            apiUrlBox.Text = ReadEnv("OPENAI_VECTOR_API_URL");
            if (string.IsNullOrWhiteSpace(apiUrlBox.Text)) apiUrlBox.Text = ChatUrl;
            Controls.Add(apiUrlBox);

            AddLabel("默认模型", 22, 115, 120);
            modelBox.SetBounds(150, 112, 180, 24);
            modelBox.DropDownStyle = ComboBoxStyle.DropDown;
            modelBox.Items.AddRange(new object[] { "gpt-5.4-mini", "gpt-5.4", "gpt-5.5", "gpt-4.1-mini" });
            string model = ReadEnv("OPENAI_VECTOR_MODEL");
            modelBox.Text = string.IsNullOrWhiteSpace(model) ? "gpt-5.4-mini" : model;
            Controls.Add(modelBox);

            AddLabel("超时秒数", 22, 160, 120);
            timeoutBox.SetBounds(150, 157, 120, 24);
            timeoutBox.Minimum = 30;
            timeoutBox.Maximum = 3600;
            timeoutBox.Increment = 30;
            timeoutBox.Value = ParseTimeout(ReadEnv("OPENAI_API_TIMEOUT"));
            Controls.Add(timeoutBox);

            Label hint = new Label();
            hint.SetBounds(22, 198, 458, 42);
            hint.ForeColor = Color.DimGray;
            hint.Text = "保存后会写入当前 Windows 用户环境变量。接口地址是高级覆盖项；一般保持 chat/completions，面板默认会自动选择接口。";
            Controls.Add(hint);

            Button saveButton = new Button();
            saveButton.Text = "保存";
            saveButton.SetBounds(250, 245, 86, 30);
            saveButton.Click += delegate { SaveSettings(); };
            Controls.Add(saveButton);

            Button clearButton = new Button();
            clearButton.Text = "清空";
            clearButton.SetBounds(344, 245, 70, 30);
            clearButton.Click += delegate { ClearSettings(); };
            Controls.Add(clearButton);

            Button closeButton = new Button();
            closeButton.Text = "关闭";
            closeButton.SetBounds(422, 245, 58, 30);
            closeButton.Click += delegate { Close(); };
            Controls.Add(closeButton);

            statusLabel.SetBounds(22, 248, 210, 24);
            statusLabel.ForeColor = Color.Blue;
            Controls.Add(statusLabel);
        }

        public static bool ShowDialogAndSave(IWin32Window owner)
        {
            using (ApiSettingsForm form = new ApiSettingsForm())
            {
                form.ShowDialog(owner);
                return form.DialogResult == DialogResult.OK;
            }
        }

        void AddLabel(string text, int left, int top, int width)
        {
            Label label = new Label();
            label.Text = text;
            label.SetBounds(left, top, width, 22);
            Controls.Add(label);
        }

        void SaveSettings()
        {
            string key = keyBox.Text.Trim();
            string apiUrl = apiUrlBox.Text.Trim();
            string model = modelBox.Text.Trim();
            string timeout = ((int)timeoutBox.Value).ToString();

            SetUserEnv("OPENAI_RELAY_API_KEY", key);
            SetUserEnv("OPENAI_VECTOR_API_URL", apiUrl);
            SetUserEnv("OPENAI_VECTOR_MODEL", model);
            SetUserEnv("OPENAI_API_TIMEOUT", timeout);

            statusLabel.Text = "已保存";
            DialogResult = DialogResult.OK;
        }

        void ClearSettings()
        {
            SetUserEnv("OPENAI_RELAY_API_KEY", "");
            SetUserEnv("OPENAI_VECTOR_API_URL", "");
            SetUserEnv("OPENAI_VECTOR_MODEL", "");
            SetUserEnv("OPENAI_API_TIMEOUT", "");

            keyBox.Text = "";
            apiUrlBox.Text = ChatUrl;
            modelBox.Text = "gpt-5.4-mini";
            timeoutBox.Value = 600;
            statusLabel.Text = "已清空";
            DialogResult = DialogResult.OK;
        }

        static decimal ParseTimeout(string value)
        {
            int parsed;
            if (!int.TryParse(value, out parsed)) parsed = 600;
            if (parsed < 30) parsed = 30;
            if (parsed > 3600) parsed = 3600;
            return parsed;
        }

        static string ReadEnv(string name)
        {
            string value = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User);
            if (string.IsNullOrWhiteSpace(value)) value = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
            return value ?? "";
        }

        static void SetUserEnv(string name, string value)
        {
            string normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            Environment.SetEnvironmentVariable(name, normalized, EnvironmentVariableTarget.User);
            Environment.SetEnvironmentVariable(name, normalized, EnvironmentVariableTarget.Process);
        }
    }
}
