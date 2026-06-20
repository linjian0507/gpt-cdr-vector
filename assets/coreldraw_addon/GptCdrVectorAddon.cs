using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using GptCdrVectorShared;

namespace GptCdrVectorAddon
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    sealed class MainForm : Form
    {
        const string ChatUrl = "https://ai.opendoor.sbs/v1/chat/completions";
        const string ResponsesUrl = "https://ai.opendoor.sbs/v1/responses";
        const string DefaultTimeout = "600";

        readonly TextBox promptBox = new TextBox();
        readonly ComboBox modelBox = new ComboBox();
        readonly ComboBox endpointBox = new ComboBox();
        readonly ComboBox styleBox = new ComboBox();
        readonly ComboBox presetBox = new ComboBox();
        readonly ComboBox sizeBox = new ComboBox();
        readonly TextBox colorsBox = new TextBox();
        readonly CheckBox allowTextBox = new CheckBox();
        readonly TextBox referenceBox = new TextBox();
        readonly Label statusLabel = new Label();
        readonly TextBox logBox = new TextBox();
        readonly Button testButton = new Button();
        readonly Button generateButton = new Button();
        readonly Button generateImportButton = new Button();
        readonly Button importButton = new Button();
        readonly Button settingsButton = new Button();
        readonly Button closeButton = new Button();

        string referenceImagePath = "";

        readonly Dictionary<string, string> styleHints = new Dictionary<string, string>
        {
            {"auto", "match the selected SVG preset without adding poster layout unless the preset is poster"},
            {"logo", "logo mark, clean geometry, limited colors, scalable at small sizes"},
            {"icon", "simple icon, strong silhouette, minimal detail, consistent stroke weight"},
            {"sticker", "bold sticker illustration, clear contour, printable flat colors"},
            {"poster", "finished advertising poster, editorial hierarchy, rich layered vector composition, polished spacing, strong focal area"},
            {"illustration", "editable flat vector illustration, layered scene, clear silhouette, controlled detail density"},
            {"line-art", "single-color line art, clean strokes, no fills unless requested"},
            {"engraving", "engraving-ready vector, high contrast, closed paths, no raster shading"},
            {"pattern", "tileable vector pattern, repeat-friendly edges, no raster texture"},
            {"custom", "follow the user's requested vector style"}
        };

        readonly HashSet<string> nonPosterPresets = new HashSet<string>
        {
            "图标/按钮",
            "Logo/字标",
            "单物体主体",
            "提取主体",
            "线稿轮廓",
            "切割雕刻",
            "贴纸徽章",
            "产品标签",
            "信息图表",
            "流程图解",
            "无缝图案",
            "背景纹理"
        };

        readonly Dictionary<string, string> presetHints = new Dictionary<string, string>
        {
            {"不使用预设", "No preset. Follow the user brief directly as editable SVG artwork."},
            {"图标/按钮", "Text-to-SVG icon: one clear symbol, centered, scalable, minimal paths, consistent stroke weight, no background unless requested."},
            {"Logo/字标", "Text-to-SVG logo or wordmark: memorable mark, clean geometry, limited colors, balanced negative space, avoid fake brand names unless supplied."},
            {"单物体主体", "Single object SVG: one isolated subject with clear silhouette, editable grouped parts, no poster frame, no unrelated decorations."},
            {"参照图转SVG", "Reference image to SVG: rebuild the reference as clean editable vectors, preserve major shapes and layout, simplify raster texture into vector forms."},
            {"提取主体", "Subject extraction SVG: focus on the main object from the prompt or reference image, remove background clutter, produce a clean editable subject layer."},
            {"线稿轮廓", "Line art SVG: clean contour lines, optional simple hatching, consistent strokes, no shaded raster-like texture."},
            {"切割雕刻", "Cutting/engraving SVG: closed paths, single-color or very limited colors, no overlapping duplicate strokes, production-friendly outlines."},
            {"贴纸徽章", "Sticker/badge SVG: bold contour, compact composition, clear border, printable flat colors, strong silhouette."},
            {"产品标签", "Product label SVG: balanced label layout, editable text areas only from the brief, decorative border, icons or badges as vector shapes."},
            {"可编辑海报", "Poster SVG: finished advertising/editorial composition, strong focal hierarchy, background depth, decorative accents, readable typography."},
            {"信息图表", "Infographic SVG: structured sections, icons, charts, labels, arrows, clear information hierarchy, no fake data beyond the brief."},
            {"流程图解", "Diagram-to-SVG: nodes, arrows, connectors, labels from the brief, aligned layout, readable structure, technical clarity."},
            {"无缝图案", "Pattern SVG: repeat-friendly motif, consistent spacing, tileable edges when relevant, organized groups."},
            {"背景纹理", "Decorative background SVG: abstract shapes, patterns, gradients made from vector elements, no central poster text unless requested."}
        };

        readonly Dictionary<string, string> presetRules = new Dictionary<string, string>
        {
            {"图标/按钮", "Hard output rule: create exactly one icon/symbol. Do not create a poster, card, app screenshot, title area, social-logo strip, or background scene."},
            {"Logo/字标", "Hard output rule: create a logo mark or wordmark only. Do not create a poster, app screenshot, ad layout, social-logo strip, or decorative page."},
            {"单物体主体", "Hard output rule: create one isolated object on a transparent/simple canvas. No poster frame, title, text panel, background stage, or unrelated icons."},
            {"提取主体", "Hard output rule: extract only the main subject. Ignore surrounding poster/card/page layout, background panels, decorative frames, social icons, and unrelated text."},
            {"参照图转SVG", "Hard output rule: convert the reference image content to editable SVG; preserve the source layout only when the selected preset is reference-to-SVG."},
            {"线稿轮廓", "Hard output rule: output clean contours only. No poster layout, color blocks, badges, title panels, or filled advertising design."},
            {"切割雕刻", "Hard output rule: output production paths only. No poster layout, shadows, gradients, tiny decorative filler, or text panels."},
            {"贴纸徽章", "Hard output rule: output one compact sticker/badge. No full poster/page composition or multi-section layout."},
            {"产品标签", "Hard output rule: output a product label asset, not a full poster. Keep layout compact and label-shaped."},
            {"信息图表", "Hard output rule: output structured information graphics only. Do not add poster hero artwork unless requested."},
            {"流程图解", "Hard output rule: output nodes, arrows, connectors, and labels only. No poster hero, decorative ad frame, or unrelated illustration scene."},
            {"无缝图案", "Hard output rule: output a repeatable pattern/motif. No poster, card, central title, or subject extraction layout."},
            {"背景纹理", "Hard output rule: output background vector texture only. No poster title, logo strip, subject extraction, or content card."}
        };

        sealed class GenerationRequest
        {
            public string Prompt;
            public string Preset;
            public string Model;
            public string ApiUrl;
            public int Timeout;
            public string Style;
            public Size Canvas;
            public string Colors;
            public bool AllowText;
            public string ReferenceImagePath;
        }

        public MainForm()
        {
            Text = "GPT CDR Vector - Addon";
            Width = 600;
            Height = 850;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;

            AddLabel("图形描述", 18, 18, 120);
            promptBox.SetBounds(18, 45, 545, 155);
            promptBox.Multiline = true;
            promptBox.ScrollBars = ScrollBars.Vertical;
            Controls.Add(promptBox);

            AddLabel("模型", 18, 220, 70);
            modelBox.SetBounds(90, 216, 160, 24);
            modelBox.DropDownStyle = ComboBoxStyle.DropDownList;
            modelBox.Items.AddRange(new object[] {"gpt-5.4-mini", "gpt-5.4", "gpt-5.5", "gpt-4.1-mini"});
            string configuredModel = Env("OPENAI_VECTOR_MODEL");
            if (!string.IsNullOrWhiteSpace(configuredModel) && !modelBox.Items.Contains(configuredModel)) modelBox.Items.Add(configuredModel);
            modelBox.SelectedItem = string.IsNullOrWhiteSpace(configuredModel) ? "gpt-5.4-mini" : configuredModel;
            Controls.Add(modelBox);

            AddLabel("接口", 285, 220, 70);
            endpointBox.SetBounds(355, 216, 208, 24);
            endpointBox.DropDownStyle = ComboBoxStyle.DropDownList;
            endpointBox.Items.AddRange(new object[] {"自动", "chat/completions", "responses", "环境变量"});
            endpointBox.SelectedItem = string.IsNullOrWhiteSpace(Env("OPENAI_VECTOR_API_URL")) ? "自动" : "环境变量";
            Controls.Add(endpointBox);

            AddLabel("风格", 18, 270, 70);
            styleBox.SetBounds(90, 266, 160, 24);
            styleBox.DropDownStyle = ComboBoxStyle.DropDownList;
            styleBox.Items.AddRange(new object[] {"auto", "poster", "illustration", "logo", "icon", "sticker", "line-art", "engraving", "pattern", "custom"});
            styleBox.SelectedItem = "auto";
            Controls.Add(styleBox);

            AddLabel("尺寸", 285, 270, 70);
            sizeBox.SetBounds(355, 266, 208, 24);
            sizeBox.DropDownStyle = ComboBoxStyle.DropDownList;
            sizeBox.Items.AddRange(new object[] {"1240x1754", "1080x1920", "1024x1024", "2048x2048", "1024x768", "768x1024"});
            sizeBox.SelectedItem = "1240x1754";
            Controls.Add(sizeBox);

            AddLabel("颜色", 18, 320, 70);
            colorsBox.SetBounds(90, 316, 473, 24);
            Controls.Add(colorsBox);

            AddLabel("SVG预设", 18, 355, 70);
            presetBox.SetBounds(90, 351, 245, 24);
            presetBox.DropDownStyle = ComboBoxStyle.DropDownList;
            presetBox.Items.AddRange(new object[] {"不使用预设", "图标/按钮", "Logo/字标", "单物体主体", "参照图转SVG", "提取主体", "线稿轮廓", "切割雕刻", "贴纸徽章", "产品标签", "可编辑海报", "信息图表", "流程图解", "无缝图案", "背景纹理"});
            presetBox.SelectedItem = "不使用预设";
            presetBox.SelectedIndexChanged += delegate
            {
                if (nonPosterPresets.Contains(Convert.ToString(presetBox.SelectedItem)) && Convert.ToString(styleBox.SelectedItem) == "poster")
                {
                    styleBox.SelectedItem = "auto";
                }
            };
            Controls.Add(presetBox);

            allowTextBox.SetBounds(355, 352, 180, 24);
            allowTextBox.Text = "允许 SVG 文字";
            Controls.Add(allowTextBox);

            AddLabel("参照图", 18, 395, 70);
            referenceBox.SetBounds(90, 391, 190, 24);
            referenceBox.ReadOnly = true;
            Controls.Add(referenceBox);

            Button pickReferenceButton = new Button();
            pickReferenceButton.Text = "选择";
            pickReferenceButton.SetBounds(288, 389, 60, 28);
            pickReferenceButton.Click += delegate { PickReferenceImage(); };
            Controls.Add(pickReferenceButton);

            Button selectedReferenceButton = new Button();
            selectedReferenceButton.Text = "用选中";
            selectedReferenceButton.SetBounds(354, 389, 70, 28);
            selectedReferenceButton.Click += delegate { UseSelectedCorelReference(); };
            Controls.Add(selectedReferenceButton);

            Button pasteReferenceButton = new Button();
            pasteReferenceButton.Text = "粘贴";
            pasteReferenceButton.SetBounds(430, 389, 60, 28);
            pasteReferenceButton.Click += delegate { PasteClipboardReference(); };
            Controls.Add(pasteReferenceButton);

            Button clearReferenceButton = new Button();
            clearReferenceButton.Text = "清除";
            clearReferenceButton.SetBounds(496, 389, 60, 28);
            clearReferenceButton.Click += delegate
            {
                referenceImagePath = "";
                referenceBox.Text = "";
                AddLog("已清除参照图。");
            };
            Controls.Add(clearReferenceButton);

            statusLabel.SetBounds(18, 435, 545, 45);
            statusLabel.Text = "就绪：选择模型后生成可编辑 SVG，再导入 CorelDRAW。";
            Controls.Add(statusLabel);

            AddLabel("运行日志", 18, 490, 100);
            logBox.SetBounds(18, 516, 545, 180);
            logBox.Multiline = true;
            logBox.ReadOnly = true;
            logBox.ScrollBars = ScrollBars.Vertical;
            Controls.Add(logBox);

            testButton.Text = "测试连接";
            testButton.SetBounds(18, 715, 105, 30);
            testButton.Click += delegate { TestConnection(); };
            Controls.Add(testButton);

            generateButton.Text = "只生成 SVG";
            generateButton.SetBounds(140, 715, 115, 30);
            generateButton.Click += delegate { RunGenerate(false); };
            Controls.Add(generateButton);

            generateImportButton.Text = "生成并导入";
            generateImportButton.SetBounds(272, 715, 115, 30);
            generateImportButton.Click += delegate { RunGenerate(true); };
            Controls.Add(generateImportButton);

            importButton.Text = "导入文件";
            importButton.SetBounds(405, 715, 118, 30);
            importButton.Click += delegate { ImportExistingFile(); };
            Controls.Add(importButton);

            settingsButton.Text = "设置";
            settingsButton.SetBounds(18, 765, 105, 30);
            settingsButton.Click += delegate { OpenApiSettings(); };
            Controls.Add(settingsButton);

            closeButton.Text = "关闭";
            closeButton.SetBounds(458, 765, 105, 30);
            closeButton.Click += delegate { Close(); };
            Controls.Add(closeButton);
        }

        void AddLabel(string text, int left, int top, int width)
        {
            Label label = new Label();
            label.Text = text;
            label.SetBounds(left, top, width, 22);
            Controls.Add(label);
        }

        void AddLog(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(AddLog), message);
                return;
            }

            string line = string.Format("[{0}] {1}", DateTime.Now.ToString("HH:mm:ss"), message);
            logBox.AppendText(line + Environment.NewLine);
            logBox.SelectionStart = logBox.TextLength;
            logBox.ScrollToCaret();
        }

        void SetStatus(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(SetStatus), message);
                return;
            }
            statusLabel.Text = message;
        }

        void SetBusy(bool busy)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<bool>(SetBusy), busy);
                return;
            }

            testButton.Enabled = !busy;
            generateButton.Enabled = !busy;
            generateImportButton.Enabled = !busy;
            importButton.Enabled = !busy;
        }

        void PickReferenceImage()
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "Image files (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp|All files (*.*)|*.*";
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                SetReferenceImage(dialog.FileName, "已选择参照图");
            }
        }

        void UseSelectedCorelReference()
        {
            try
            {
                string path = ExportCorelSelectionReference();
                SetReferenceImage(path, "已使用 CDR 选中对象作为参照图");
            }
            catch (Exception ex)
            {
                SetStatus("读取选中对象失败：" + ex.Message);
                AddLog("读取选中对象失败：" + ex.Message);
                MessageBox.Show(this, ex.Message, "GPT CDR Vector", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void PasteClipboardReference()
        {
            try
            {
                if (!Clipboard.ContainsImage()) throw new InvalidOperationException("剪贴板里没有可用图片。请先截图或复制图片。");
                Image image = Clipboard.GetImage();
                if (image == null) throw new InvalidOperationException("无法读取剪贴板图片。");
                string path = TempReferencePath("clipboard");
                try
                {
                    image.Save(path, ImageFormat.Png);
                }
                finally
                {
                    image.Dispose();
                }
                SetReferenceImage(path, "已粘贴截图作为参照图");
            }
            catch (Exception ex)
            {
                SetStatus("粘贴参照图失败：" + ex.Message);
                AddLog("粘贴参照图失败：" + ex.Message);
                MessageBox.Show(this, ex.Message, "GPT CDR Vector", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void SetReferenceImage(string path, string message)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("找不到参照图：" + path);
            referenceImagePath = path;
            referenceBox.Text = path;
            AddLog(message + "：" + path);
            SetStatus(message + "。");
        }

        string ExportCorelSelectionReference()
        {
            AddLog("正在读取 CorelDRAW 当前选中对象。");
            dynamic app = GetCorelDrawApp();
            dynamic doc = null;
            try { doc = app.ActiveDocument; } catch { }
            if (doc == null) throw new InvalidOperationException("CorelDRAW 当前没有活动文档。");
            if (ActiveSelectionCount(app, doc) <= 0) throw new InvalidOperationException("请先在 CorelDRAW 页面上选择一个对象或图片。");

            string path = TempReferencePath("cdr-selection");
            try
            {
                AddLog("正在导出选中对象为 PNG 参照图。");
                dynamic filter = doc.ExportBitmap(path, 802, 2, 4, 0, 0, 180, 180, 1, false, true, true, false, 0);
                try { filter.Finish(); } catch { }
            }
            catch (Exception ex)
            {
                AddLog("选区导出失败，尝试复制选区读取剪贴板：" + ex.Message);
                CopyCorelSelectionToClipboard(app, doc);
                if (!Clipboard.ContainsImage()) throw;
                Image image = Clipboard.GetImage();
                if (image == null) throw;
                try
                {
                    image.Save(path, ImageFormat.Png);
                }
                finally
                {
                    image.Dispose();
                }
            }

            if (!File.Exists(path) || new FileInfo(path).Length == 0) throw new InvalidOperationException("选中对象导出为空，请确认对象可见且未被锁定。");
            return path;
        }

        int ActiveSelectionCount(dynamic app, dynamic doc)
        {
            object selection = null;
            try { selection = doc.ActiveSelectionRange; } catch { }
            if (selection == null) try { selection = app.ActiveSelectionRange; } catch { }
            if (selection == null) return 0;
            try { return Convert.ToInt32(selection.GetType().InvokeMember("Count", System.Reflection.BindingFlags.GetProperty, null, selection, null)); } catch { }
            try { return Convert.ToInt32(((dynamic)selection).Count); } catch { }
            return 0;
        }

        void CopyCorelSelectionToClipboard(dynamic app, dynamic doc)
        {
            object selection = null;
            try { selection = doc.ActiveSelectionRange; } catch { }
            if (selection == null) try { selection = app.ActiveSelectionRange; } catch { }
            if (selection == null) throw new InvalidOperationException("无法读取 CorelDRAW 当前选区。");
            try
            {
                ((dynamic)selection).Copy();
            }
            catch
            {
                try { app.ActiveSelection.Copy(); }
                catch { throw new InvalidOperationException("当前 CorelDRAW 版本不支持自动复制选区。请先手动复制或截图，再点“粘贴”。"); }
            }
            Thread.Sleep(200);
        }

        string TempReferencePath(string source)
        {
            return Path.Combine(Path.GetTempPath(), "gpt-cdr-vector-ref-" + source + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png");
        }

        void OpenApiSettings()
        {
            if (ApiSettingsForm.ShowDialogAndSave(this))
            {
                string configuredModel = Env("OPENAI_VECTOR_MODEL");
                if (!string.IsNullOrWhiteSpace(configuredModel))
                {
                    if (!modelBox.Items.Contains(configuredModel)) modelBox.Items.Add(configuredModel);
                    modelBox.SelectedItem = configuredModel;
                }

                endpointBox.SelectedItem = string.IsNullOrWhiteSpace(Env("OPENAI_VECTOR_API_URL")) ? "自动" : "环境变量";
                AddLog("API 设置已更新。");
                SetStatus("API 设置已保存。新任务会使用最新配置。");
            }
        }

        void TestConnection()
        {
            logBox.Clear();
            AddLog("开始测试连接。");
            try
            {
                string key = Env("OPENAI_RELAY_API_KEY");
                if (string.IsNullOrWhiteSpace(key)) key = Env("OPENAI_API_KEY");
                AddLog("API Key：" + (string.IsNullOrWhiteSpace(key) ? "missing" : "set"));
                AddLog("模型：" + SelectedModel());
                AddLog("接口：" + SelectedApiUrl());
                AddLog("超时：" + TimeoutSeconds() + " 秒");

                dynamic app = GetCorelDrawApp();
                AddLog("CorelDRAW：" + app.Version);
                SetStatus("连接正常：CorelDRAW " + app.Version + "，模型 " + SelectedModel());
                AddLog("连接测试完成。");
            }
            catch (Exception ex)
            {
                SetStatus("连接失败：" + ex.Message);
                AddLog("连接失败：" + ex.Message);
                MessageBox.Show(this, ex.Message, "GPT CDR Vector", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void RunGenerate(bool importAfterGenerate)
        {
            GenerationRequest request;
            try
            {
                request = CaptureGenerationRequest();
            }
            catch (Exception ex)
            {
                SetStatus("生成失败：" + ex.Message);
                AddLog("任务失败：" + ex.Message);
                MessageBox.Show(this, ex.Message, "GPT CDR Vector", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            logBox.Clear();
            SetBusy(true);
            AddLog(importAfterGenerate ? "任务开始：生成并导入。" : "任务开始：只生成 SVG。");
            SetStatus("正在生成 SVG，复杂海报可能需要几分钟...");

            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    string outputPath = GenerateSvg(request);
                    AddLog("SVG 生成完成：" + outputPath);
                    if (importAfterGenerate)
                    {
                        SetStatus("正在导入 CorelDRAW...");
                        ImportToCorelDraw(outputPath);
                        SetStatus("完成：SVG 已生成并导入 CorelDRAW。");
                        AddLog("任务完成：SVG 已生成并导入。");
                    }
                    else
                    {
                        SetStatus("完成：SVG 已生成。");
                        AddLog("任务完成：SVG 已生成。");
                        BeginInvoke(new Action(delegate
                        {
                            MessageBox.Show(this, "SVG 已生成：" + Environment.NewLine + outputPath, "GPT CDR Vector", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }));
                    }
                }
                catch (Exception ex)
                {
                    SetStatus("失败：" + ex.Message);
                    AddLog("任务失败：" + ex.Message);
                    BeginInvoke(new Action(delegate
                    {
                        MessageBox.Show(this, ex.Message, "GPT CDR Vector", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }));
                }
                finally
                {
                    SetBusy(false);
                }
            });
        }

        GenerationRequest CaptureGenerationRequest()
        {
            string prompt = promptBox.Text.Trim();
            if (prompt.Length == 0) throw new InvalidOperationException("请输入图形描述。");

            string model = SelectedModel();
            return new GenerationRequest
            {
                Prompt = prompt,
                Preset = Convert.ToString(presetBox.SelectedItem),
                Model = model,
                ApiUrl = SelectedApiUrl(model),
                Timeout = TimeoutSeconds(),
                Style = styleBox.SelectedItem.ToString(),
                Canvas = ParseSize(sizeBox.SelectedItem.ToString()),
                Colors = colorsBox.Text.Trim(),
                AllowText = allowTextBox.Checked,
                ReferenceImagePath = referenceImagePath
            };
        }

        string GenerateSvg(GenerationRequest request)
        {
            string outputPath = Path.Combine(Path.GetTempPath(), "gpt-cdr-vector-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".svg");

            AddLog("读取配置完成。");
            AddLog("预设：" + request.Preset);
            AddLog("模型：" + request.Model);
            AddLog("接口：" + request.ApiUrl);
            AddLog("超时：" + request.Timeout + " 秒");
            if (!string.IsNullOrWhiteSpace(request.ReferenceImagePath)) AddLog("参照图：" + request.ReferenceImagePath);
            AddLog("输出文件：" + outputPath);

            string key = Env("OPENAI_RELAY_API_KEY");
            if (string.IsNullOrWhiteSpace(key)) key = Env("OPENAI_API_KEY");
            if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("OPENAI_RELAY_API_KEY 或 OPENAI_API_KEY 未设置。");

            string userPrompt = BuildUserPrompt(request);
            object payload = BuildPayload(request.ApiUrl, request.Model, userPrompt, request.ReferenceImagePath);

            AddLog("开始调用中转接口。");
            DateTime startedAt = DateTime.Now;
            ManualResetEvent waitDone = new ManualResetEvent(false);
            Thread progressThread = new Thread(new ThreadStart(delegate
            {
                while (!waitDone.WaitOne(15000))
                {
                    AddLog("接口生成中，已等待 " + (int)(DateTime.Now - startedAt).TotalSeconds + " 秒。");
                }
            }));
            progressThread.IsBackground = true;
            progressThread.Start();

            string response;
            try
            {
                response = PostJson(request.ApiUrl, key, payload, request.Timeout);
            }
            finally
            {
                waitDone.Set();
                progressThread.Join(1000);
                waitDone.Close();
            }
            AddLog("接口已返回，用时 " + (int)(DateTime.Now - startedAt).TotalSeconds + " 秒。");

            string svg = ValidateSvg(ExtractSvg(ResponseText(response)), request.Canvas);
            File.WriteAllText(outputPath, svg + Environment.NewLine, new UTF8Encoding(false));
            return outputPath;
        }

        string BuildUserPrompt(GenerationRequest request)
        {
            string colorLine = request.Colors.Length > 0 ? "Preferred colors: " + request.Colors + "." : "Choose a compact production-friendly palette.";
            string textLine = request.AllowText ? "Text is allowed if requested by the design brief." : "Avoid text unless it is absolutely necessary.";
            string presetHint = presetHints.ContainsKey(request.Preset) ? presetHints[request.Preset] : presetHints["不使用预设"];
            string style = EffectiveStyle(request);
            string presetGuard = PresetGuard(request.Preset);
            string referenceLine = string.IsNullOrWhiteSpace(request.ReferenceImagePath) ? "" : ReferenceInstruction(request.Preset);

            return
                "Create CorelDRAW-ready SVG vector artwork.\n\n" +
                "Design brief:\n" + request.Prompt + "\n\n" +
                "Production specs:\n" +
                "- Canvas: " + request.Canvas.Width + "x" + request.Canvas.Height + ", viewBox 0 0 " + request.Canvas.Width + " " + request.Canvas.Height + ".\n" +
                "- SVG preset: " + presetHint + "\n" +
                "- Style: " + styleHints[style] + ".\n" +
                "- When the selected SVG preset conflicts with the visual style, prioritize the preset.\n" +
                presetGuard +
                "- " + colorLine + "\n" +
                "- " + textLine + "\n" +
                "- Target use: CorelDRAW editable SVG vector artwork.\n" +
                "- Build the artwork with clear top-level SVG groups for editable CorelDRAW layers.\n" +
                "- Use group names that match the preset, such as background, subject, icon_mark, logo_mark, label_layout, diagram_nodes, connectors, typography, decorative_shapes, foreground_details, and polish.\n" +
                "- Use only readable text from the design brief. Do not invent garbled Chinese copy.\n" +
                "- If the brief is short, expand only within the selected preset. For isolated assets, refine the subject itself instead of adding a surrounding scene.\n" +
                "- Avoid a sparse row of isolated icons, generic circles, random social logos, placeholder brand names, and large empty panels unless the selected preset or brief specifically asks for them.\n" +
                "- Use 3-6 coordinated colors with deliberate contrast. For isolated assets, add only subject details; for composition presets, add depth through layered vector shapes, subtle gradients, frames, grids, badges, or ornaments.\n" +
                "- Make the result feel finished for its preset: icon clarity, object isolation, diagram structure, pattern consistency, label balance, or poster hierarchy as appropriate.\n" +
                "- Keep text areas legible: no overlapping text, no tiny filler text, and no pseudo-letters.\n" +
                referenceLine +
                "- Output only SVG XML.";
        }

        string EffectiveStyle(GenerationRequest request)
        {
            if (request.Style == "poster" && nonPosterPresets.Contains(request.Preset)) return "auto";
            return styleHints.ContainsKey(request.Style) ? request.Style : "auto";
        }

        string PresetGuard(string preset)
        {
            if (preset == "不使用预设") return "";
            StringBuilder guard = new StringBuilder();
            if (nonPosterPresets.Contains(preset))
            {
                guard.Append("- This is not a poster task. Do not create a poster canvas, advertisement card, app screenshot, large title panel, social icon row, background stage, or multi-section page unless the user explicitly asks for one.\n");
            }
            if (presetRules.ContainsKey(preset))
            {
                guard.Append("- ").Append(presetRules[preset]).Append("\n");
            }
            return guard.ToString();
        }

        string ReferenceInstruction(string preset)
        {
            if (preset == "提取主体" || preset == "单物体主体" || preset == "图标/按钮" || preset == "Logo/字标" || preset == "线稿轮廓" || preset == "切割雕刻" || preset == "贴纸徽章")
            {
                return "- Use the attached reference image only to identify the main subject, silhouette, proportions, important colors, and useful surface details. Ignore the reference background, page/card layout, title area, social logos, decorative frames, and unrelated text. Output the isolated preset asset as native SVG shapes.\n";
            }
            if (preset == "参照图转SVG")
            {
                return "- Rebuild the attached reference image as editable SVG shapes. Preserve the major source layout and visible objects, but do not embed, trace, or rasterize the image.\n";
            }
            return "- Use the attached reference image for visual direction, spacing, color mood, and useful composition cues. Rebuild it as editable SVG shapes; do not embed or trace the image as raster data. Keep only the parts that match the selected preset and user brief.\n";
        }

        object BuildPayload(string apiUrl, string model, string userPrompt, string requestReferenceImagePath)
        {
            if (apiUrl.IndexOf("/chat/completions", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new Dictionary<string, object>
                {
                    {"model", model},
                    {"messages", new object[]
                        {
                            new Dictionary<string, object> {{"role", "system"}, {"content", SystemPrompt()}},
                            new Dictionary<string, object> {{"role", "user"}, {"content", ChatUserContent(userPrompt, requestReferenceImagePath)}}
                        }
                    },
                    {"temperature", 0.2}
                };
            }

            return new Dictionary<string, object>
            {
                {"model", model},
                {"input", new object[]
                    {
                        new Dictionary<string, object> {{"role", "system"}, {"content", SystemPrompt()}},
                        new Dictionary<string, object> {{"role", "user"}, {"content", ResponsesUserContent(userPrompt, requestReferenceImagePath)}}
                    }
                }
            };
        }

        object ChatUserContent(string prompt, string requestReferenceImagePath)
        {
            if (string.IsNullOrWhiteSpace(requestReferenceImagePath)) return prompt;
            return new object[]
            {
                new Dictionary<string, object> {{"type", "text"}, {"text", prompt}},
                new Dictionary<string, object> {{"type", "image_url"}, {"image_url", new Dictionary<string, object> {{"url", ImageDataUrl(requestReferenceImagePath)}}}}
            };
        }

        object ResponsesUserContent(string prompt, string requestReferenceImagePath)
        {
            if (string.IsNullOrWhiteSpace(requestReferenceImagePath)) return prompt;
            return new object[]
            {
                new Dictionary<string, object> {{"type", "input_text"}, {"text", prompt}},
                new Dictionary<string, object> {{"type", "input_image"}, {"image_url", ImageDataUrl(requestReferenceImagePath)}}
            };
        }

        string PostJson(string url, string apiKey, object payload, int timeoutSeconds)
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;
            byte[] data = Encoding.UTF8.GetBytes(serializer.Serialize(payload));

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.Headers["Authorization"] = "Bearer " + apiKey;
            request.Timeout = timeoutSeconds * 1000;
            request.ReadWriteTimeout = timeoutSeconds * 1000;
            request.ContentLength = data.Length;

            using (Stream requestStream = request.GetRequestStream())
            {
                requestStream.Write(data, 0, data.Length);
            }

            try
            {
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (WebException ex)
            {
                string detail = "";
                if (ex.Response != null)
                {
                    using (StreamReader reader = new StreamReader(ex.Response.GetResponseStream(), Encoding.UTF8))
                    {
                        detail = reader.ReadToEnd();
                    }
                }
                if (ex.Status == WebExceptionStatus.Timeout)
                {
                    throw new InvalidOperationException("接口等待超时。可调大 OPENAI_API_TIMEOUT，或换更便宜/更快的模型。");
                }
                throw new InvalidOperationException("接口请求失败：" + (detail.Length > 0 ? detail : ex.Message));
            }
        }

        string ResponseText(string json)
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;
            object root = serializer.DeserializeObject(json);
            IDictionary dict = root as IDictionary;
            if (dict == null) throw new InvalidOperationException("接口返回不是 JSON 对象。");

            if (dict.Contains("output_text") && dict["output_text"] is string) return (string)dict["output_text"];

            if (dict.Contains("choices"))
            {
                object[] choices = dict["choices"] as object[];
                if (choices != null)
                {
                    foreach (object item in choices)
                    {
                        IDictionary choice = item as IDictionary;
                        if (choice == null || !choice.Contains("message")) continue;
                        IDictionary message = choice["message"] as IDictionary;
                        if (message == null || !message.Contains("content")) continue;
                        string contentText = ContentToText(message["content"]);
                        if (!string.IsNullOrWhiteSpace(contentText)) return contentText;
                    }
                }
            }

            if (dict.Contains("output"))
            {
                List<string> parts = new List<string>();
                object[] output = dict["output"] as object[];
                if (output != null)
                {
                    foreach (object outputItem in output)
                    {
                        IDictionary outputDict = outputItem as IDictionary;
                        if (outputDict == null || !outputDict.Contains("content")) continue;
                        string text = ContentToText(outputDict["content"]);
                        if (!string.IsNullOrWhiteSpace(text)) parts.Add(text);
                    }
                }
                if (parts.Count > 0) return string.Join("\n", parts.ToArray());
            }

            throw new InvalidOperationException("接口返回中没有找到文本输出。");
        }

        string ContentToText(object value)
        {
            if (value is string) return (string)value;
            object[] list = value as object[];
            if (list == null) return "";
            List<string> parts = new List<string>();
            foreach (object item in list)
            {
                IDictionary dict = item as IDictionary;
                if (dict == null) continue;
                if (dict.Contains("text") && dict["text"] is string) parts.Add((string)dict["text"]);
                else if (dict.Contains("output_text") && dict["output_text"] is string) parts.Add((string)dict["output_text"]);
            }
            return string.Join("\n", parts.ToArray());
        }

        string ExtractSvg(string raw)
        {
            string text = raw.Trim();
            text = Regex.Replace(text, @"^```(?:svg|xml)?\s*", "", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\s*```$", "");
            Match match = Regex.Match(text, @"<svg\b[\s\S]*?</svg>", RegexOptions.IgnoreCase);
            if (!match.Success) throw new InvalidOperationException("模型返回中没有完整的 <svg>...</svg>。");
            return match.Value.Trim();
        }

        string ValidateSvg(string svg, Size canvas)
        {
            string lower = svg.ToLowerInvariant();
            string[] blocked = {"<script", "<foreignobject", "data:image", "<image", "@import", "url(http"};
            foreach (string token in blocked)
            {
                if (lower.Contains(token)) throw new InvalidOperationException("SVG 包含不适合导入 CorelDRAW 的内容：" + token);
            }
            if (Regex.IsMatch(lower, "\\b(?:href|xlink:href)\\s*=\\s*['\\\"](?:https?:|data:)"))
            {
                throw new InvalidOperationException("SVG 包含外部链接。");
            }

            if (!Regex.IsMatch(svg, @"<svg\b[^>]*\sviewBox\s*=", RegexOptions.IgnoreCase))
                svg = AddSvgAttr(svg, "viewBox", "0 0 " + canvas.Width + " " + canvas.Height);
            if (!Regex.IsMatch(svg, @"<svg\b[^>]*\swidth\s*=", RegexOptions.IgnoreCase))
                svg = AddSvgAttr(svg, "width", canvas.Width.ToString());
            if (!Regex.IsMatch(svg, @"<svg\b[^>]*\sheight\s*=", RegexOptions.IgnoreCase))
                svg = AddSvgAttr(svg, "height", canvas.Height.ToString());
            if (!Regex.IsMatch(svg, @"<svg\b[^>]*\sxmlns\s*=", RegexOptions.IgnoreCase))
                svg = AddSvgAttr(svg, "xmlns", "http://www.w3.org/2000/svg");
            return svg;
        }

        string AddSvgAttr(string svg, string name, string value)
        {
            Match match = Regex.Match(svg, @"<svg\b[^>]*>", RegexOptions.IgnoreCase);
            if (!match.Success) throw new InvalidOperationException("找不到 SVG 开始标签。");
            int insertAt = match.Index + match.Length - 1;
            return svg.Substring(0, insertAt) + " " + name + "=\"" + value + "\"" + svg.Substring(insertAt);
        }

        void ImportExistingFile()
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "SVG/Image files (*.svg;*.png;*.jpg;*.jpeg)|*.svg;*.png;*.jpg;*.jpeg|All files (*.*)|*.*";
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            logBox.Clear();
            AddLog("任务开始：导入已有文件。");
            AddLog("文件：" + dialog.FileName);
            try
            {
                ImportToCorelDraw(dialog.FileName);
                SetStatus("完成：文件已导入 CorelDRAW。");
                AddLog("任务完成：文件已导入。");
            }
            catch (Exception ex)
            {
                SetStatus("导入失败：" + ex.Message);
                AddLog("任务失败：" + ex.Message);
                MessageBox.Show(this, ex.Message, "GPT CDR Vector", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void ImportToCorelDraw(string filePath)
        {
            if (!File.Exists(filePath)) throw new FileNotFoundException("找不到文件：" + filePath);
            AddLog("连接 CorelDRAW。");
            dynamic app = GetCorelDrawApp();
            dynamic doc = null;
            try { doc = app.ActiveDocument; } catch { }
            if (doc == null)
            {
                AddLog("当前没有活动文档，创建新文档。");
                doc = app.CreateDocument();
            }

            AddLog("导入文件到当前活动图层。");
            dynamic options = app.CreateStructImportOptions();
            app.ActiveLayer.Import(filePath, 0, options);
            try { app.Refresh(); } catch { }
            AddLog("CorelDRAW 导入完成。");
        }

        dynamic GetCorelDrawApp()
        {
            string progId = CorelProgId();
            try
            {
                return Marshal.GetActiveObject(progId);
            }
            catch
            {
            }

            Type type = Type.GetTypeFromProgID(progId);
            if (type == null) throw new InvalidOperationException("无法找到 " + progId + "。");
            dynamic app = Activator.CreateInstance(type);
            try { app.Visible = true; } catch { }
            return app;
        }

        string CorelProgId()
        {
            string version = Env("GPT_CDR_VECTOR_COREL_VERSION");
            if (string.IsNullOrWhiteSpace(version))
            {
                string versionFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "corel_version.txt");
                if (File.Exists(versionFile)) version = File.ReadAllText(versionFile).Trim();
            }
            if (string.IsNullOrWhiteSpace(version)) version = "20";
            return "CorelDRAW.Application." + version;
        }

        string SelectedModel()
        {
            return Convert.ToString(modelBox.SelectedItem);
        }

        string SelectedApiUrl()
        {
            return SelectedApiUrl(SelectedModel());
        }

        string SelectedApiUrl(string model)
        {
            string selected = Convert.ToString(endpointBox.SelectedItem);
            if (selected == "chat/completions") return ChatUrl;
            if (selected == "responses") return ResponsesUrl;
            if (selected == "环境变量")
            {
                string env = Env("OPENAI_VECTOR_API_URL");
                if (string.IsNullOrWhiteSpace(env)) env = Env("OPENAI_RESPONSES_API_URL");
                if (!string.IsNullOrWhiteSpace(env)) return env;
            }

            if (model.StartsWith("gpt-5.5", StringComparison.OrdinalIgnoreCase)) return ResponsesUrl;
            return ChatUrl;
        }

        int TimeoutSeconds()
        {
            string timeout = Env("OPENAI_API_TIMEOUT");
            int parsed;
            if (!int.TryParse(timeout, out parsed)) parsed = int.Parse(DefaultTimeout);
            if (parsed < 30) parsed = 30;
            return parsed;
        }

        string Env(string name)
        {
            string value = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
            if (string.IsNullOrWhiteSpace(value)) value = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User);
            return value ?? "";
        }

        Size ParseSize(string value)
        {
            Match match = Regex.Match(value, @"^\s*(\d{2,5})\s*[xX]\s*(\d{2,5})\s*$");
            if (!match.Success) throw new InvalidOperationException("尺寸格式无效：" + value);
            return new Size(int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value));
        }

        string ImageDataUrl(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("参照图不存在：" + path);
            string ext = Path.GetExtension(path).ToLowerInvariant();
            string mime;
            if (ext == ".jpg" || ext == ".jpeg") mime = "image/jpeg";
            else if (ext == ".png") mime = "image/png";
            else if (ext == ".webp") mime = "image/webp";
            else throw new InvalidOperationException("参照图必须是 PNG、JPG、JPEG 或 WEBP。");
            return "data:" + mime + ";base64," + Convert.ToBase64String(File.ReadAllBytes(path));
        }

        string SystemPrompt()
        {
            return
                "You are a senior vector designer preparing artwork for CorelDRAW.\n" +
                "Return exactly one valid standalone SVG XML document and nothing else.\n\n" +
                "Work like a professional SVG production designer:\n" +
                "- First reason internally about the selected task type, geometry, layout, focal hierarchy, spacing, palette, and editable layer structure.\n" +
                "- Then output only the final SVG. Do not include your reasoning, notes, or comments.\n" +
                "- Follow the selected SVG preset. If it conflicts with style, the SVG preset wins.\n" +
                "- Use a complete result appropriate to the task: single-object SVGs should be clean and isolated; icons should be simple and legible; diagrams should be structured; posters should feel complete.\n" +
                "- If the preset is not explicitly poster, never add poster/page/card/app-screen layout, title panels, social icon strips, or unrelated background sections.\n" +
                "- Keep all shapes intentional and aligned; avoid random symbols, unrelated brands, fake logos, and invented text.\n\n" +
                "CorelDRAW compatibility requirements:\n" +
                "- Use a viewBox and explicit width/height.\n" +
                "- Prefer paths, basic shapes, flat fills, simple strokes, and simple gradients.\n" +
                "- Do not use Markdown, comments explaining the design, scripts, animation, foreignObject, external links, embedded raster images, base64 data, CSS imports, or web fonts.\n" +
                "- Keep text minimal. If text is requested, use plain <text> elements only.\n" +
                "- Keep the SVG editable: organize major objects in <g> groups with useful ids.\n" +
                "- For poster work, use top-level layer groups such as background, key_visual, layout, typography, decorative_shapes, foreground_details, and polish.\n" +
                "- Use smooth closed paths, consistent stroke widths, regular curves, and clean geometry.\n" +
                "- Avoid filters, blur, masks, clipping, and excessive tiny paths unless essential.\n" +
                "- Keep every visible object inside the viewBox and leave balanced margins.\n" +
                "- Do a final internal quality check before output: clear hierarchy, coherent palette, no accidental overlaps, no broken paths, no placeholder text, no empty-looking canvas.";
        }
    }
}
