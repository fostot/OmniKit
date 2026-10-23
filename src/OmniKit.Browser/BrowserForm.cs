using System.Diagnostics;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace OmniKit.Browser;

internal sealed class BrowserForm : Form
{
    internal const string HomeUrl = "https://ffxiv.consolegameswiki.com/wiki/FF14_Wiki";
    private readonly nint parentWindow;
    private readonly int parentPid;
    private readonly Process parentProcess;
    private readonly string profileDirectory;
    private readonly WebView2 browser = new();
    private readonly Panel controls = new() { Height = 76, Visible = false };
    private readonly Panel heading = new() { Dock = DockStyle.Top, Height = 30 };
    private readonly ToolStrip navigation = new() { Dock = DockStyle.Fill, GripStyle = ToolStripGripStyle.Hidden, AutoSize = false };
    private readonly ToolStripTextBox address = new() { AutoSize = false, Width = 300, MaxLength = 4096, ToolTipText = "Website address (HTTP or HTTPS)" };
    private readonly Button menu = new() { Text = "☰", Size = new Size(30, 28), TabStop = true, AccessibleName = "Browser controls" };
    private readonly Label resize = new() { Text = "◢", Size = new Size(18, 18), Cursor = Cursors.SizeNWSE, TextAlign = ContentAlignment.MiddleRight };
    private readonly Label notice = new() { AutoSize = false, Height = 30, Visible = false, Padding = new Padding(8, 5, 8, 4) };
    private readonly Panel extensionPanel = new() { Visible = false, Padding = new Padding(12) };
    private readonly ListBox extensions = new() { Height = 150, IntegralHeight = false, HorizontalScrollbar = true };
    private readonly FlowLayoutPanel extensionContent = new() { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Location = new Point(12, 12) };
    private readonly System.Windows.Forms.Timer parentTimer = new() { Interval = 300 };
    private readonly TaskCompletionSource initialized = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private string? pendingUrl;
    private bool initializedSuccessfully;
    private bool closing;
    private bool moving;
    private bool resizing;
    private Point dragMouse;
    private Rectangle dragBounds;
    private bool extensionBusy;

    internal Task Initialized => initialized.Task;

    internal BrowserForm(nint parentWindow, int parentPid, string profileDirectory)
    {
        this.parentWindow = parentWindow;
        this.parentPid = parentPid;
        this.profileDirectory = profileDirectory;
        parentProcess = Process.GetProcessById(parentPid);
        TopLevel = false;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.FromArgb(24, 29, 34);
        ForeColor = Color.Gainsboro;
        Font = new Font("Segoe UI", 9);
        StartPosition = FormStartPosition.Manual;
        Bounds = new Rectangle(30, 40, 760, 500);
        Controls.Add(browser);
        Controls.Add(extensionPanel);
        Controls.Add(controls);
        Controls.Add(notice);
        Controls.Add(menu);
        Controls.Add(resize);
        BuildControls();
        BuildExtensionPanel();
        menu.Click += (_, _) => ToggleControls();
        resize.MouseDown += (_, e) => StartDrag(e, true);
        resize.MouseMove += (_, _) => ContinueDrag();
        resize.MouseUp += (_, _) => EndDrag();
        heading.MouseDown += (_, e) => StartDrag(e, false);
        heading.MouseMove += (_, _) => ContinueDrag();
        heading.MouseUp += (_, _) => EndDrag();
        SizeChanged += (_, _) => LayoutChrome();
        parentTimer.Tick += (_, _) =>
        {
            if (!NativeWindow.Matches(parentWindow, parentPid) || parentProcess.HasExited) Close();
            else ClampToParent();
        };
        Shown += async (_, _) => await InitializeBrowserAsync();
        FormClosing += (_, _) => { closing = true; parentTimer.Stop(); };
        CreateControl();
        NativeWindow.SetParent(Handle, parentWindow);
        if (NativeWindow.GetParent(Handle) != parentWindow) throw new InvalidOperationException();
        ClampToParent();
        parentTimer.Start();
    }

    private void BuildControls()
    {
        heading.BackColor = Color.FromArgb(31, 39, 43);
        var title = new Label { Text = "OmniKit Browser · drag here", AutoSize = true, Location = new Point(8, 7) };
        title.MouseDown += (_, e) => StartDrag(e, false);
        title.MouseMove += (_, _) => ContinueDrag();
        title.MouseUp += (_, _) => EndDrag();
        heading.Controls.Add(title);
        var close = new Button { Text = "×", Dock = DockStyle.Right, Width = 30, AccessibleName = "Hide browser" };
        close.Click += (_, _) => HideBrowser();
        heading.Controls.Add(close);
        navigation.BackColor = BackColor;
        navigation.ForeColor = ForeColor;
        AddNavigation("←", "Back", () => { if (browser.CoreWebView2?.CanGoBack == true) browser.CoreWebView2.GoBack(); });
        AddNavigation("→", "Forward", () => { if (browser.CoreWebView2?.CanGoForward == true) browser.CoreWebView2.GoForward(); });
        AddNavigation("↻", "Reload", () => browser.CoreWebView2?.Reload());
        AddNavigation("⌂", "Home", () => Navigate(HomeUrl));
        navigation.Items.Add(address);
        AddNavigation("Go", "Navigate", () => NavigateAddress());
        AddNavigation("Extensions", "Manage unpacked browser extensions", () => _ = ShowExtensionsAsync());
        address.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; NavigateAddress(); } };
        controls.Controls.Add(navigation);
        controls.Controls.Add(heading);
    }

    private void AddNavigation(string text, string help, Action action)
    {
        var button = new ToolStripButton(text) { ToolTipText = help, DisplayStyle = ToolStripItemDisplayStyle.Text };
        button.Click += (_, _) => { try { action(); } catch (Exception) { Error("The browser action could not complete."); } };
        navigation.Items.Add(button);
    }

    private void BuildExtensionPanel()
    {
        extensionPanel.BackColor = BackColor;
        extensionPanel.AutoScroll = true;
        var explanation = new Label { AutoSize = true, Text = "Extensions\nAdd an unpacked extension folder that you trust. Extensions can read and change websites in this browser. Web Store installation and every extension API are not supported." };
        var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = true };
        foreach (var (text, callback) in new (string, Func<Task>)[]
        {
            ("Add folder…", AddExtensionAsync), ("Enable / disable", ToggleExtensionAsync),
            ("Remove", RemoveExtensionAsync), ("Back to website", () => { extensionPanel.Visible = false; return Task.CompletedTask; }),
        })
        {
            var button = new Button { Text = text, AutoSize = true, Height = 28 };
            button.Click += async (_, _) => await ExtensionActionAsync(callback);
            buttons.Controls.Add(button);
        }
        extensionContent.Controls.Add(explanation);
        extensionContent.Controls.Add(extensions);
        extensionContent.Controls.Add(buttons);
        extensionPanel.Controls.Add(extensionContent);
        extensionPanel.SizeChanged += (_, _) =>
        {
            var width = Math.Max(80, extensionPanel.ClientSize.Width - 48);
            explanation.MaximumSize = new Size(width, 0);
            extensions.Width = width;
            buttons.MaximumSize = new Size(width, 0);
        };
    }

    private async Task InitializeBrowserAsync()
    {
        if (initializedSuccessfully || closing) return;
        try
        {
            var options = new CoreWebView2EnvironmentOptions { AreBrowserExtensionsEnabled = true };
            var environment = await CoreWebView2Environment.CreateAsync(null, profileDirectory, options);
            if (closing) return;
            await browser.EnsureCoreWebView2Async(environment);
            if (closing) return;
            var core = browser.CoreWebView2;
            core.Settings.AreHostObjectsAllowed = false;
            core.Settings.IsWebMessageEnabled = false;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.AreBrowserAcceleratorKeysEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsPasswordAutosaveEnabled = false;
            core.Settings.IsGeneralAutofillEnabled = false;
            core.NavigationStarting += (_, e) =>
            {
                if (!BrowserProtocol.TryUrl(e.Uri, out var allowedUrl)) { e.Cancel = true; Error("Only HTTP and HTTPS website navigation is allowed."); }
                else { notice.Visible = false; LayoutChrome(); BrowserProtocol.Emit("loading", "Loading the requested website."); }
            };
            core.FrameNavigationStarting += (_, e) => { if (!BrowserProtocol.AllowSubframe(e.Uri)) e.Cancel = true; };
            core.NavigationCompleted += (_, e) =>
            {
                if (e.IsSuccess) { address.Text = core.Source; BrowserProtocol.Emit("navigated", "The website finished loading."); }
                else if (e.WebErrorStatus != CoreWebView2WebErrorStatus.OperationCanceled) Error("The website could not load. Check the address or connection.");
            };
            core.NewWindowRequested += (_, e) => { e.Handled = true; if (BrowserProtocol.TryUrl(e.Uri, out var url)) Navigate(url); else Error("That link could not open in this browser."); };
            core.LaunchingExternalUriScheme += (_, e) => { e.Cancel = true; Error("External applications cannot be opened from this browser."); };
            core.DownloadStarting += (_, e) => { e.Cancel = true; Error("Downloads are disabled in this browser."); };
            core.PermissionRequested += (_, e) => { e.State = CoreWebView2PermissionState.Deny; e.Handled = true; };
            core.ProcessFailed += (_, _) => { Error("The browser engine stopped. Open the browser again to restart it."); Close(); };
            initializedSuccessfully = true;
            initialized.TrySetResult();
            BrowserProtocol.Emit("ready", "The in-game browser is ready.", Handle.ToInt64());
            BrowserProtocol.Emit(Visible ? "shown" : "hidden", Visible ? "The browser is visible." : "The browser is hidden.");
            if (pendingUrl != null) { var requested = pendingUrl; pendingUrl = null; Navigate(requested); }
            LayoutChrome();
        }
        catch (Exception)
        {
            initialized.TrySetException(new InvalidOperationException("The browser engine could not initialize."));
            Error("The browser engine could not initialize. Check the WebView2 runtime.");
            if (!closing) Close();
        }
    }

    internal void Dispatch(BrowserCommand command)
    {
        if (closing) return;
        switch (command.Kind)
        {
            case "navigate": Navigate(command.Url!); break;
            case "toggle-controls": ToggleControls(); break;
            case "show":
                if (initializedSuccessfully && (string.IsNullOrEmpty(browser.CoreWebView2.Source) || browser.CoreWebView2.Source == "about:blank")) Navigate(HomeUrl);
                else ShowBrowser();
                break;
            case "hide": HideBrowser(); break;
            case "shutdown": Close(); break;
        }
    }

    private void NavigateAddress()
    {
        if (BrowserProtocol.TryUrl(address.Text.Trim(), out var url)) Navigate(url);
        else Error("Enter a complete HTTP or HTTPS address.");
    }

    private void Navigate(string requested)
    {
        if (!BrowserProtocol.TryUrl(requested, out var url)) { Error("Only HTTP and HTTPS website navigation is allowed."); return; }
        extensionPanel.Visible = false;
        ShowBrowser();
        if (!initializedSuccessfully) pendingUrl = url;
        else browser.CoreWebView2.Navigate(url);
    }

    private void ToggleControls()
    {
        ShowBrowser();
        controls.Visible = !controls.Visible;
        if (!controls.Visible) extensionPanel.Visible = false;
        LayoutChrome();
    }

    private void ShowBrowser() { if (!Visible) Show(); ClampToParent(); BrowserProtocol.Emit("shown", "The browser is visible."); }
    private void HideBrowser() { if (Visible) { Hide(); BrowserProtocol.Emit("hidden", "The browser is hidden."); } }
    private void Error(string message) { if (closing || IsDisposed) return; notice.Text = message; notice.Visible = true; LayoutChrome(); BrowserProtocol.Emit("error", message); }

    private void LayoutChrome()
    {
        notice.Height = Math.Max(30, TextRenderer.MeasureText(notice.Text, notice.Font, new Size(Math.Max(1, ClientSize.Width - 16), int.MaxValue), TextFormatFlags.WordBreak).Height + 10);
        var top = controls.Visible ? controls.Height : 0;
        var bottom = notice.Visible ? notice.Height : 0;
        controls.Bounds = new Rectangle(0, 0, ClientSize.Width, controls.Height);
        browser.Bounds = new Rectangle(0, top, ClientSize.Width, Math.Max(1, ClientSize.Height - top - bottom));
        extensionPanel.Bounds = browser.Bounds;
        notice.Bounds = new Rectangle(0, Math.Max(0, ClientSize.Height - bottom), ClientSize.Width, notice.Height);
        menu.Location = new Point(Math.Max(0, ClientSize.Width - menu.Width - (controls.Visible ? 40 : 8)), 4);
        resize.Location = new Point(Math.Max(0, ClientSize.Width - resize.Width - 2), Math.Max(0, ClientSize.Height - resize.Height - 2));
        resize.Visible = controls.Visible;
        address.Width = Math.Max(80, ClientSize.Width - 360);
        menu.BringToFront(); resize.BringToFront();
    }

    private void ClampToParent()
    {
        var size = NativeWindow.ClientSize(parentWindow);
        if (size.Width <= 0 || size.Height <= 0) return;
        var width = Math.Clamp(Width, Math.Min(360, size.Width), size.Width);
        var height = Math.Clamp(Height, Math.Min(240, size.Height), size.Height);
        var next = new Rectangle(Math.Clamp(Left, 0, size.Width - width), Math.Clamp(Top, 0, size.Height - height), width, height);
        if (Bounds != next) Bounds = next;
    }

    private void StartDrag(MouseEventArgs e, bool resizeWindow)
    {
        if (e.Button != MouseButtons.Left) return;
        dragMouse = Cursor.Position; dragBounds = Bounds;
        resizing = resizeWindow; moving = !resizeWindow;
        (resizeWindow ? (Control)resize : heading).Capture = true;
    }

    private void ContinueDrag()
    {
        if (!moving && !resizing) return;
        if ((MouseButtons & MouseButtons.Left) == 0) { EndDrag(); return; }
        var delta = new Size(Cursor.Position.X - dragMouse.X, Cursor.Position.Y - dragMouse.Y);
        if (moving) Location = dragBounds.Location + delta;
        else Size = new Size(Math.Max(360, dragBounds.Width + delta.Width), Math.Max(240, dragBounds.Height + delta.Height));
        ClampToParent();
    }

    private void EndDrag() { moving = resizing = false; heading.Capture = resize.Capture = false; }

    private async Task ShowExtensionsAsync()
    {
        if (!initializedSuccessfully) { Error("Wait for the browser to initialize."); return; }
        controls.Visible = true;
        extensionPanel.Visible = true;
        extensionPanel.BringToFront(); controls.BringToFront(); LayoutChrome();
        await ExtensionActionAsync(RefreshExtensionsAsync);
    }

    private async Task ExtensionActionAsync(Func<Task> action)
    {
        if (extensionBusy || !initializedSuccessfully || closing) return;
        extensionBusy = true;
        try { await action(); }
        catch (Exception) { Error("The extension action could not complete. Check the selected extension and runtime support."); }
        finally { extensionBusy = false; }
    }

    private async Task RefreshExtensionsAsync()
    {
        var installed = await browser.CoreWebView2.Profile.GetBrowserExtensionsAsync();
        if (closing) return;
        extensions.Items.Clear();
        foreach (var extension in installed) extensions.Items.Add(new ExtensionItem(extension));
    }

    private async Task AddExtensionAsync()
    {
        using var picker = new FolderBrowserDialog { Description = "Choose an unpacked extension folder containing manifest.json", UseDescriptionForTitle = true, ShowNewFolderButton = false };
        if (picker.ShowDialog(this) != DialogResult.OK) return;
        await browser.CoreWebView2.Profile.AddBrowserExtensionAsync(picker.SelectedPath);
        await RefreshExtensionsAsync();
    }

    private async Task ToggleExtensionAsync()
    {
        if (extensions.SelectedItem is not ExtensionItem item) return;
        await item.Value.EnableAsync(!item.Value.IsEnabled);
        await RefreshExtensionsAsync();
    }

    private async Task RemoveExtensionAsync()
    {
        if (extensions.SelectedItem is not ExtensionItem item) return;
        await item.Value.RemoveAsync();
        await RefreshExtensionsAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { closing = true; parentTimer.Dispose(); parentProcess.Dispose(); browser.Dispose(); }
        base.Dispose(disposing);
    }

    private sealed record ExtensionItem(CoreWebView2BrowserExtension Value)
    {
        public override string ToString() => (Value.IsEnabled ? "Enabled · " : "Disabled · ") + new string(Value.Name.Where(c => !char.IsControl(c)).Take(120).ToArray());
    }
}
