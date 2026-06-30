using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace ImasKoreanPatcher
{
    public sealed class MainForm : Form
    {
        private Panel dropPanel;
        private Label dropTitleLabel;
        private Label dropHintLabel;
        private Button patchButton;
        private ProgressBar progressBar;
        private Label statusLabel;

        private string selectedIsoPath;
        private BackgroundWorker patchWorker;

        public MainForm()
        {
            Text = "\uc544\uc774\ub3cc\ub9c8\uc2a4\ud130 \ud55c\uae00 \ud328\uce58";
            Font = new Font("Malgun Gothic", 9F, FontStyle.Regular, GraphicsUnit.Point);
            BackColor = Color.FromArgb(246, 247, 250);
            MinimumSize = new Size(720, 360);
            Size = new Size(840, 420);
            StartPosition = FormStartPosition.CenterScreen;
            AllowDrop = true;

            TableLayoutPanel root = new TableLayoutPanel();
            root.ColumnCount = 2;
            root.RowCount = 3;
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(18);
            root.BackColor = BackColor;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70F));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            Controls.Add(root);

            dropPanel = BuildDropPanel();
            root.Controls.Add(dropPanel, 0, 0);

            Panel buttonPanel = BuildButtonPanel();
            root.Controls.Add(buttonPanel, 1, 0);

            progressBar = new ProgressBar();
            progressBar.Dock = DockStyle.Fill;
            progressBar.Minimum = 0;
            progressBar.Maximum = 100;
            progressBar.Value = 0;
            progressBar.Style = ProgressBarStyle.Continuous;
            root.Controls.Add(progressBar, 0, 1);
            root.SetColumnSpan(progressBar, 2);

            statusLabel = new Label();
            statusLabel.Dock = DockStyle.Fill;
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            statusLabel.AutoEllipsis = true;
            statusLabel.UseMnemonic = false;
            statusLabel.ForeColor = Color.FromArgb(65, 72, 86);
            statusLabel.Text = "\ub300\uae30 \uc911";
            root.Controls.Add(statusLabel, 0, 2);
            root.SetColumnSpan(statusLabel, 2);

            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;
        }

        private Panel BuildDropPanel()
        {
            Panel panel = new Panel();
            panel.AllowDrop = true;
            panel.BackColor = Color.White;
            panel.BorderStyle = BorderStyle.FixedSingle;
            panel.Dock = DockStyle.Fill;
            panel.Margin = new Padding(0, 0, 14, 14);
            panel.Cursor = Cursors.Hand;

            TableLayoutPanel inner = new TableLayoutPanel();
            inner.ColumnCount = 1;
            inner.RowCount = 3;
            inner.Dock = DockStyle.Fill;
            inner.Padding = new Padding(22);
            inner.BackColor = Color.White;
            inner.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            inner.RowStyles.Add(new RowStyle(SizeType.Absolute, 86F));
            inner.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            panel.Controls.Add(inner);

            TableLayoutPanel center = new TableLayoutPanel();
            center.ColumnCount = 1;
            center.RowCount = 2;
            center.Dock = DockStyle.Fill;
            center.BackColor = Color.White;
            center.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            center.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            inner.Controls.Add(center, 0, 1);

            dropTitleLabel = new Label();
            dropTitleLabel.Dock = DockStyle.Fill;
            dropTitleLabel.TextAlign = ContentAlignment.BottomCenter;
            dropTitleLabel.Font = new Font(Font.FontFamily, 14F, FontStyle.Bold, GraphicsUnit.Point);
            dropTitleLabel.ForeColor = Color.FromArgb(34, 42, 54);
            dropTitleLabel.Text = "\uc6d0\ubcf8 ISO\ub97c \uc5ec\uae30\uc5d0 \ub4dc\ub798\uadf8 & \ub4dc\ub86d";
            center.Controls.Add(dropTitleLabel, 0, 0);

            dropHintLabel = new Label();
            dropHintLabel.Dock = DockStyle.Fill;
            dropHintLabel.TextAlign = ContentAlignment.TopCenter;
            dropHintLabel.AutoEllipsis = true;
            dropHintLabel.UseMnemonic = false;
            dropHintLabel.ForeColor = Color.FromArgb(92, 101, 116);
            dropHintLabel.Text = "\ud074\ub9ad\ud574\uc11c ISO \ud30c\uc77c \uc120\ud0dd";
            center.Controls.Add(dropHintLabel, 0, 1);

            panel.Click += OnDropPanelClick;
            inner.Click += OnDropPanelClick;
            center.Click += OnDropPanelClick;
            dropTitleLabel.Click += OnDropPanelClick;
            dropHintLabel.Click += OnDropPanelClick;
            panel.DragEnter += OnDragEnter;
            panel.DragDrop += OnDragDrop;
            inner.DragEnter += OnDragEnter;
            inner.DragDrop += OnDragDrop;
            center.DragEnter += OnDragEnter;
            center.DragDrop += OnDragDrop;

            return panel;
        }

        private Panel BuildButtonPanel()
        {
            Panel panel = new Panel();
            panel.BackColor = BackColor;
            panel.Dock = DockStyle.Fill;
            panel.Margin = new Padding(0, 0, 0, 14);

            patchButton = new Button();
            patchButton.Anchor = AnchorStyles.None;
            patchButton.Size = new Size(166, 58);
            patchButton.Text = "\ud55c\uae00 \ud328\uce58";
            patchButton.Font = new Font(Font.FontFamily, 12F, FontStyle.Bold, GraphicsUnit.Point);
            patchButton.Enabled = false;
            patchButton.Click += OnPatchButtonClick;
            panel.Controls.Add(patchButton);
            panel.Resize += delegate
            {
                patchButton.Left = Math.Max(0, (panel.ClientSize.Width - patchButton.Width) / 2);
                patchButton.Top = Math.Max(0, (panel.ClientSize.Height - patchButton.Height) / 2);
            };

            return panel;
        }

        private void OnDropPanelClick(object sender, EventArgs e)
        {
            if (patchWorker != null && patchWorker.IsBusy)
            {
                return;
            }

            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "\uc6d0\ubcf8 ISO \ud30c\uc77c \uc120\ud0dd";
                dialog.Filter = "ISO files (*.iso)|*.iso|All files (*.*)|*.*";
                dialog.Multiselect = false;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    SelectIso(dialog.FileName);
                }
            }
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            string path = TryGetDroppedIso(e);
            e.Effect = path == null ? DragDropEffects.None : DragDropEffects.Copy;
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            string path = TryGetDroppedIso(e);
            if (path == null)
            {
                SetStatus("\uc120\ud0dd\ud55c \ud30c\uc77c\uc740 ISO\uac00 \uc544\ub2d9\ub2c8\ub2e4.");
                return;
            }

            SelectIso(path);
        }

        private static string TryGetDroppedIso(DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                return null;
            }

            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length != 1)
            {
                return null;
            }

            string path = files[0];
            if (!IsIsoPath(path))
            {
                return null;
            }

            return path;
        }

        private static bool IsIsoPath(string path)
        {
            return !String.IsNullOrEmpty(path)
                && File.Exists(path)
                && String.Equals(Path.GetExtension(path), ".iso", StringComparison.OrdinalIgnoreCase);
        }

        private void SelectIso(string path)
        {
            if (!IsIsoPath(path))
            {
                SetStatus("ISO \ud30c\uc77c\ub9cc \uc120\ud0dd\ud560 \uc218 \uc788\uc2b5\ub2c8\ub2e4.");
                return;
            }

            selectedIsoPath = path;
            patchButton.Enabled = true;
            progressBar.Value = 0;
            dropHintLabel.Text = Path.GetFileName(path);
            SetStatus("ISO \uc120\ud0dd\ub428: " + path);
        }

        private void OnPatchButtonClick(object sender, EventArgs e)
        {
            if (String.IsNullOrEmpty(selectedIsoPath))
            {
                SetStatus("\ud328\uce58\ud560 ISO\ub97c \uba3c\uc800 \uc120\ud0dd\ud574 \uc8fc\uc138\uc694.");
                return;
            }

            string isoPath = selectedIsoPath;
            patchButton.Enabled = false;
            dropPanel.Enabled = false;
            progressBar.Value = 0;
            SetStatus("\ud328\uce58 \uc2e4\ud589 \uc911...");

            patchWorker = new BackgroundWorker();
            patchWorker.WorkerReportsProgress = true;
            patchWorker.DoWork += delegate(object workerSender, DoWorkEventArgs workerArgs)
            {
                RunIsoRoundTrip((BackgroundWorker)workerSender, isoPath);
            };
            patchWorker.ProgressChanged += delegate(object workerSender, ProgressChangedEventArgs progressArgs)
            {
                progressBar.Value = Math.Max(progressBar.Minimum, Math.Min(progressBar.Maximum, progressArgs.ProgressPercentage));
                if (progressArgs.UserState is string)
                {
                    SetStatus((string)progressArgs.UserState);
                }
            };
            patchWorker.RunWorkerCompleted += delegate(object workerSender, RunWorkerCompletedEventArgs completedArgs)
            {
                dropPanel.Enabled = true;
                patchButton.Enabled = !String.IsNullOrEmpty(selectedIsoPath);
                if (completedArgs.Error != null)
                {
                    SetStatus("\uc624\ub958: " + completedArgs.Error.Message);
                    return;
                }
            };
            patchWorker.RunWorkerAsync();
        }

        private void RunIsoRoundTrip(BackgroundWorker worker, string isoPath)
        {
            Report(worker, 8, "\uc785\ub825 ISO \ud655\uc778 \uc911...");
            if (!File.Exists(isoPath))
            {
                throw new FileNotFoundException("\uc120\ud0dd\ud55c ISO \ud30c\uc77c\uc774 \uc874\uc7ac\ud558\uc9c0 \uc54a\uc2b5\ub2c8\ub2e4.", isoPath);
            }

            string assetRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");
            assetRoot = FindAssetsRoot(assetRoot);
            string exisoPath = Path.Combine(assetRoot, Path.Combine("Tools", "exiso.exe"));
            if (!File.Exists(exisoPath))
            {
                throw new FileNotFoundException("exiso.exe\ub97c \ucc3e\uc744 \uc218 \uc5c6\uc2b5\ub2c8\ub2e4.", exisoPath);
            }

            string translationsPath = Path.Combine(assetRoot, "translations_text_id_ko.jsonl");
            if (!File.Exists(translationsPath))
            {
                throw new FileNotFoundException("\ubc88\uc5ed \ub370\uc774\ud130\ub97c \ucc3e\uc744 \uc218 \uc5c6\uc2b5\ub2c8\ub2e4.", translationsPath);
            }

            string isoDirectory = Path.GetDirectoryName(isoPath);
            if (String.IsNullOrEmpty(isoDirectory))
            {
                isoDirectory = Environment.CurrentDirectory;
            }

            string baseName = Path.GetFileNameWithoutExtension(isoPath);
            string workRoot = GetAvailableDirectoryPath(Path.Combine(isoDirectory, baseName + "_patcher_work"));
            string extractRoot = Path.Combine(workRoot, "xiso_root");
            string outputIso = GetAvailableFilePath(Path.Combine(isoDirectory, baseName + "_repacked.iso"));

            Directory.CreateDirectory(extractRoot);

            Report(worker, 20, "\uc6d0\ubcf8 ISO \ud574\uc81c \uc911...");
            RunTool(
                exisoPath,
                "-x -d " + QuoteArgument(extractRoot) + " " + QuoteArgument(isoPath),
                Path.GetDirectoryName(exisoPath));

            if (!Directory.Exists(extractRoot) || Directory.GetFileSystemEntries(extractRoot).Length == 0)
            {
                throw new InvalidOperationException("ISO \ud574\uc81c \uacb0\uacfc\uac00 \ube44\uc5b4 \uc788\uc2b5\ub2c8\ub2e4.");
            }

            Report(worker, 32, "\ubc88\uc5ed \ub370\uc774\ud130 \ub85c\ub4dc \uc911...");
            XboxTextPatcher textPatcher = new XboxTextPatcher(JsonTranslationStore.Load(translationsPath));

            Report(worker, 35, "\ud574\uc81c\ub41c \ud30c\uc77c\uc5d0 \ubc88\uc5ed \ubc18\uc601 \uc911...");
            TranslationPatchResult patchResult = textPatcher.PatchExtractedRoot(
                extractRoot,
                delegate(int percent, string message)
                {
                    Report(worker, percent, message);
                });

            if (patchResult.MsgEntriesPatched == 0)
            {
                throw new InvalidOperationException("\ubc18\uc601\ub41c \ubc88\uc5ed \ubb38\uc790\uc5f4\uc774 0\uac1c\uc785\ub2c8\ub2e4.");
            }

            Report(worker, 70, "\ubc88\uc5ed\ub41c \ud30c\uc77c\ub85c ISO \uc7ac\uc0dd\uc131 \uc911...");
            RunTool(
                exisoPath,
                "-c " + QuoteArgument(extractRoot) + " " + QuoteArgument(outputIso),
                Path.GetDirectoryName(exisoPath));

            if (!File.Exists(outputIso))
            {
                throw new FileNotFoundException("\uc7ac\uc0dd\uc131\ub41c ISO\ub97c \ucc3e\uc744 \uc218 \uc5c6\uc2b5\ub2c8\ub2e4.", outputIso);
            }

            FileInfo outputInfo = new FileInfo(outputIso);
            if (outputInfo.Length == 0)
            {
                throw new InvalidOperationException("\uc7ac\uc0dd\uc131\ub41c ISO \ud30c\uc77c \ud06c\uae30\uac00 0\uc785\ub2c8\ub2e4.");
            }

            Report(
                worker,
                100,
                String.Format("\uc644\ub8cc: {0:N0}\uac1c \ubb38\uc790\uc5f4 \ubc18\uc601, {1}", patchResult.MsgEntriesPatched, outputIso));
        }

        private static string FindAssetsRoot(string preferredPath)
        {
            if (Directory.Exists(preferredPath))
            {
                return preferredPath;
            }

            DirectoryInfo directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, "Assets");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            return preferredPath;
        }

        private static string GetAvailableDirectoryPath(string desiredPath)
        {
            if (!Directory.Exists(desiredPath) && !File.Exists(desiredPath))
            {
                return desiredPath;
            }

            for (int i = 1; i < 1000; i++)
            {
                string candidate = desiredPath + "_" + i.ToString();
                if (!Directory.Exists(candidate) && !File.Exists(candidate))
                {
                    return candidate;
                }
            }

            throw new IOException("\uc0ac\uc6a9 \uac00\ub2a5\ud55c \uc791\uc5c5 \ud3f4\ub354 \uc774\ub984\uc744 \ucc3e\uc744 \uc218 \uc5c6\uc2b5\ub2c8\ub2e4.");
        }

        private static string GetAvailableFilePath(string desiredPath)
        {
            if (!File.Exists(desiredPath) && !Directory.Exists(desiredPath))
            {
                return desiredPath;
            }

            string directory = Path.GetDirectoryName(desiredPath);
            if (String.IsNullOrEmpty(directory))
            {
                directory = Environment.CurrentDirectory;
            }

            string name = Path.GetFileNameWithoutExtension(desiredPath);
            string extension = Path.GetExtension(desiredPath);
            for (int i = 1; i < 1000; i++)
            {
                string candidate = Path.Combine(directory, name + "_" + i.ToString() + extension);
                if (!File.Exists(candidate) && !Directory.Exists(candidate))
                {
                    return candidate;
                }
            }

            throw new IOException("\uc0ac\uc6a9 \uac00\ub2a5\ud55c \ucd9c\ub825 ISO \uc774\ub984\uc744 \ucc3e\uc744 \uc218 \uc5c6\uc2b5\ub2c8\ub2e4.");
        }

        private static string QuoteArgument(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static void RunTool(string fileName, string arguments, string workingDirectory)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = fileName;
            startInfo.Arguments = arguments;
            startInfo.WorkingDirectory = workingDirectory;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;

            StringBuilder output = new StringBuilder();
            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                process.Start();
                output.Append(process.StandardOutput.ReadToEnd());
                output.Append(process.StandardError.ReadToEnd());
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    string message = output.ToString().Trim();
                    if (message.Length == 0)
                    {
                        message = "exit code " + process.ExitCode.ToString();
                    }
                    throw new InvalidOperationException("exiso.exe \uc2e4\ud589 \uc2e4\ud328: " + message);
                }
            }
        }

        private static void Report(BackgroundWorker worker, int percent, string message)
        {
            worker.ReportProgress(percent, message);
        }

        private void SetStatus(string message)
        {
            statusLabel.Text = message;
        }
    }
}
