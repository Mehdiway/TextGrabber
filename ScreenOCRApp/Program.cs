using Tesseract;

namespace ScreenOCRApp;

public partial class MainForm : Form
{
    private NotifyIcon trayIcon;
    private ContextMenuStrip trayMenu;
    private ScreenCaptureForm captureForm;

    public MainForm()
    {
        InitializeComponent();
        CreateTrayIcon();

        // Hide the main form
        this.WindowState = FormWindowState.Minimized;
        this.ShowInTaskbar = false;
        this.Visible = false;
    }

    private void CreateTrayIcon()
    {
        // Create tray menu
        trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("Capture & OCR", null, OnCaptureClick);
        trayMenu.Items.Add("-");
        trayMenu.Items.Add("Exit", null, OnExitClick);

        // Create tray icon
        trayIcon = new NotifyIcon()
        {
            Icon = CreateIcon(),
            ContextMenuStrip = trayMenu,
            Visible = true,
            Text = "Screen OCR Tool"
        };

        trayIcon.Click += OnTrayIconClick;
    }

    private Icon CreateIcon()
    {
        // Create a simple icon programmatically
        Bitmap bitmap = new Bitmap(16, 16);
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Blue);
            g.FillRectangle(Brushes.White, 2, 2, 12, 12);
            g.DrawString("A", new Font("Arial", 8, FontStyle.Bold), Brushes.Blue, 3, 1);
        }

        IntPtr hIcon = bitmap.GetHicon();
        return Icon.FromHandle(hIcon);
    }

    private void OnTrayIconClick(object sender, EventArgs e)
    {
        if (((MouseEventArgs)e).Button == MouseButtons.Left)
        {
            StartScreenCapture();
        }
    }

    private void OnCaptureClick(object sender, EventArgs e)
    {
        StartScreenCapture();
    }

    private void StartScreenCapture()
    {
        if (captureForm == null || captureForm.IsDisposed)
        {
            captureForm = new ScreenCaptureForm();
            captureForm.ScreenshotCaptured += OnScreenshotCaptured;
        }
        captureForm.StartCapture();
    }

    private void OnScreenshotCaptured(object sender, ScreenshotEventArgs e)
    {
        try
        {
            string text = PerformOCR(e.Screenshot);
            ShowOCRResult(text);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"OCR Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private string PerformOCR(Bitmap image)
    {
        // Save image temporarily
        string tempPath = Path.Combine(Path.GetTempPath(), "ocr_temp.png");
        image.Save(tempPath, System.Drawing.Imaging.ImageFormat.Png);

        try
        {
            // Initialize Tesseract engine
            using (var engine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default))
            {
                using (var img = Pix.LoadFromFile(tempPath))
                {
                    using (var page = engine.Process(img))
                    {
                        return page.GetText();
                    }
                }
            }
        }
        finally
        {
            // Clean up temp file
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    private void ShowOCRResult(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            MessageBox.Show("No text found in the selected area.", "OCR Result",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
            // Show result in a dialog with copy functionality
            OCRResultForm resultForm = new OCRResultForm(text);
            resultForm.ShowDialog();
        }
    }

    private void OnExitClick(object sender, EventArgs e)
    {
        trayIcon.Visible = false;
        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            trayIcon?.Dispose();
            trayMenu?.Dispose();
            captureForm?.Dispose();
        }
        base.Dispose(disposing);
    }

    protected override void SetVisibleCore(bool value)
    {
        base.SetVisibleCore(false);
    }
}

// Screen capture form for selecting rectangle
public partial class ScreenCaptureForm : Form
{
    private bool isSelecting = false;
    private Point startPoint;
    private Point endPoint;
    private Rectangle selectionRect;
    private Bitmap screenCapture;

    public event EventHandler<ScreenshotEventArgs> ScreenshotCaptured;

    public ScreenCaptureForm()
    {
        InitializeComponent();
        this.FormBorderStyle = FormBorderStyle.None;
        this.WindowState = FormWindowState.Maximized;
        this.TopMost = true;
        this.BackColor = Color.Black;
        this.Opacity = 0.3;
        this.Cursor = Cursors.Cross;
        this.ShowInTaskbar = false;

        this.MouseDown += OnMouseDown;
        this.MouseMove += OnMouseMove;
        this.MouseUp += OnMouseUp;
        this.Paint += OnPaint;
        this.KeyDown += OnKeyDown;
    }

    public void StartCapture()
    {
        CaptureScreen();
        this.Show();
        this.Focus();
    }

    private void CaptureScreen()
    {
        Rectangle bounds = Screen.PrimaryScreen.Bounds;
        screenCapture = new Bitmap(bounds.Width, bounds.Height);

        using (Graphics g = Graphics.FromImage(screenCapture))
        {
            g.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size);
        }
    }

    private void OnMouseDown(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            isSelecting = true;
            startPoint = e.Location;
            endPoint = e.Location;
            selectionRect = new Rectangle();
        }
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (isSelecting)
        {
            endPoint = e.Location;
            UpdateSelectionRect();
            this.Invalidate();
        }
    }

    private void OnMouseUp(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && isSelecting)
        {
            isSelecting = false;

            if (selectionRect.Width > 10 && selectionRect.Height > 10)
            {
                CaptureSelectedArea();
            }

            this.Hide();
        }
    }

    private void UpdateSelectionRect()
    {
        selectionRect = new Rectangle(
            Math.Min(startPoint.X, endPoint.X),
            Math.Min(startPoint.Y, endPoint.Y),
            Math.Abs(endPoint.X - startPoint.X),
            Math.Abs(endPoint.Y - startPoint.Y)
        );
    }

    private void CaptureSelectedArea()
    {
        if (selectionRect.Width > 0 && selectionRect.Height > 0)
        {
            Bitmap selectedArea = new Bitmap(selectionRect.Width, selectionRect.Height);

            using (Graphics g = Graphics.FromImage(selectedArea))
            {
                g.DrawImage(screenCapture, 0, 0, selectionRect, GraphicsUnit.Pixel);
            }

            ScreenshotCaptured?.Invoke(this, new ScreenshotEventArgs(selectedArea));
        }
    }

    private void OnPaint(object sender, PaintEventArgs e)
    {
        if (isSelecting && selectionRect.Width > 0 && selectionRect.Height > 0)
        {
            using (Pen pen = new Pen(Color.Red, 2))
            {
                e.Graphics.DrawRectangle(pen, selectionRect);
            }

            // Draw selection info
            string info = $"{selectionRect.Width} x {selectionRect.Height}";
            using (Font font = new Font("Arial", 12))
            using (Brush brush = new SolidBrush(Color.Yellow))
            {
                e.Graphics.DrawString(info, font, brush, selectionRect.X, selectionRect.Y - 25);
            }
        }
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            this.Hide();
        }
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();
        this.AutoScaleDimensions = new SizeF(6F, 13F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.ClientSize = new Size(284, 261);
        this.Name = "ScreenCaptureForm";
        this.Text = "Screen Capture";
        this.ResumeLayout(false);
    }

    protected override void Dispose(bool disposing)
    {
        screenCapture?.Dispose();
        base.Dispose(disposing);
    }
}

// OCR Result display form
public partial class OCRResultForm : Form
{
    private TextBox textBox;
    private Button copyButton;
    private Button closeButton;

    public OCRResultForm(string ocrText)
    {
        InitializeComponent();
        textBox.Text = ocrText;
        textBox.SelectAll();
    }

    private void InitializeComponent()
    {
        this.textBox = new TextBox();
        this.copyButton = new Button();
        this.closeButton = new Button();
        this.SuspendLayout();

        // textBox
        this.textBox.Location = new Point(12, 12);
        this.textBox.Multiline = true;
        this.textBox.Name = "textBox";
        this.textBox.ScrollBars = ScrollBars.Vertical;
        this.textBox.Size = new Size(460, 200);
        this.textBox.TabIndex = 0;
        this.textBox.ReadOnly = true;

        // copyButton
        this.copyButton.Location = new Point(316, 230);
        this.copyButton.Name = "copyButton";
        this.copyButton.Size = new Size(75, 23);
        this.copyButton.TabIndex = 1;
        this.copyButton.Text = "Copy";
        this.copyButton.UseVisualStyleBackColor = true;
        this.copyButton.Click += new EventHandler(this.CopyButton_Click);

        // closeButton
        this.closeButton.Location = new Point(397, 230);
        this.closeButton.Name = "closeButton";
        this.closeButton.Size = new Size(75, 23);
        this.closeButton.TabIndex = 2;
        this.closeButton.Text = "Close";
        this.closeButton.UseVisualStyleBackColor = true;
        this.closeButton.Click += new EventHandler(this.CloseButton_Click);

        // OCRResultForm
        this.AutoScaleDimensions = new SizeF(6F, 13F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.ClientSize = new Size(484, 271);
        this.Controls.Add(this.closeButton);
        this.Controls.Add(this.copyButton);
        this.Controls.Add(this.textBox);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.Name = "OCRResultForm";
        this.StartPosition = FormStartPosition.CenterScreen;
        this.Text = "OCR Result";
        this.TopMost = true;
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    private void CopyButton_Click(object sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(textBox.Text))
        {
            Clipboard.SetText(textBox.Text);
            MessageBox.Show("Text copied to clipboard!", "Success",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void CloseButton_Click(object sender, EventArgs e)
    {
        this.Close();
    }
}

// Event args for screenshot capture
public class ScreenshotEventArgs : EventArgs
{
    public Bitmap Screenshot { get; }

    public ScreenshotEventArgs(Bitmap screenshot)
    {
        Screenshot = screenshot;
    }
}

// Main form designer code
public partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    private void InitializeComponent()
    {
        this.SuspendLayout();
        this.AutoScaleDimensions = new SizeF(6F, 13F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.ClientSize = new Size(284, 261);
        this.Name = "MainForm";
        this.Text = "Screen OCR";
        this.WindowState = FormWindowState.Minimized;
        this.ShowInTaskbar = false;
        this.ResumeLayout(false);
    }
}

// Program entry point
class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}
