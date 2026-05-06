using System;
using System.Threading;
using System.Threading.Tasks;
using Core.Interfaces.Data;
using Core.Interfaces.Services;
using GUI.Windows.Presenters;
using GUI.Windows.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.FSharp.Core;
using Stem.ButtonPanel.Tester.Core.Dictionary;
using Stem.ButtonPanel.Tester.Services.Dictionary;

namespace GUI.Windows
{
    public partial class Form1 : Form
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly DictionaryService _dictionaryService;
        private readonly ILogger<Form1> _logger;
        private readonly CancellationTokenSource _formClosedCts = new();
        private ButtonPanelTestPresenter? _presenter;
        private Label _dictionarySourceLabel = null!;
        private Button _refreshDictionaryButton = null!;

        public Form1(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _dictionaryService = serviceProvider.GetRequiredService<DictionaryService>();
            _logger = serviceProvider.GetRequiredService<ILogger<Form1>>();

            InitializeComponent();
            Text = "Stem Button Panel Tester";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1450, 850);
            AutoScroll = true;

            byte[] iconBytes = Properties.Resources.Ztem;
            if (iconBytes != null && iconBytes.Length > 0)
            {
                using var ms = new MemoryStream(iconBytes);
                Icon = new Icon(ms);
            }

            SetupDictionaryControls();
            SetupControls();

            // Render the initial state (set by Program.cs's InitializeAsync call).
            RenderDictionarySource();

            // Subscribe to live updates.
            _dictionaryService.SourceChanged += OnSourceChanged;
            FormClosed += OnFormClosed;
        }

        private void SetupDictionaryControls()
        {
            _dictionarySourceLabel = new Label
            {
                Location = new Point(12, 12),
                AutoSize = true,
                Font = new Font(Font.FontFamily, 9f, FontStyle.Regular),
                Text = "Dictionary: initialising…",
            };

            _refreshDictionaryButton = new Button
            {
                Location = new Point(420, 8),
                Size = new Size(140, 28),
                Text = "Refresh dictionary",
            };
            _refreshDictionaryButton.Click += OnRefreshClick;

            Controls.Add(_dictionarySourceLabel);
            Controls.Add(_refreshDictionaryButton);
        }

        private void SetupControls()
        {
            IButtonPanelTestService service = _serviceProvider.GetRequiredService<IButtonPanelTestService>();
            IProtocolRepositoryFactory repositoryFactory = _serviceProvider.GetRequiredService<IProtocolRepositoryFactory>();

            var view = new ButtonPanelTestUserControl();
            _presenter = new ButtonPanelTestPresenter(view, service, repositoryFactory);

            // Push the panel down so the dictionary status row stays visible.
            view.Location = new Point(0, 48);
            Controls.Add(view);
        }

        private void OnSourceChanged(object? sender, DictionarySource source)
        {
            // SourceChanged may fire from a background thread.
            if (InvokeRequired)
            {
                BeginInvoke(new Action(RenderDictionarySource));
            }
            else
            {
                RenderDictionarySource();
            }
        }

        private void RenderDictionarySource()
        {
            FSharpValueOption<Tuple<ButtonPanelDictionary, DictionarySource>> snapshot = _dictionaryService.Snapshot;
            if (snapshot.IsValueNone)
            {
                _dictionarySourceLabel.Text = "Dictionary: not loaded";
                _dictionarySourceLabel.ForeColor = Color.Firebrick;
                return;
            }

            DictionarySource source = snapshot.Value.Item2;
            (string text, Color color) = source switch
            {
                DictionarySource.Live live =>
                    ($"Dictionary: live (fetched {FormatTimestamp(live.FetchedAt)})", Color.ForestGreen),
                DictionarySource.Cached cached when cached.FallbackReason == FetchFailureReason.Unauthorized =>
                    ($"Dictionary: cached (credential problem; cached {FormatTimestamp(cached.FetchedAt)})", Color.DarkRed),
                DictionarySource.Cached cached when cached.FallbackReason == FetchFailureReason.NetworkUnreachable =>
                    ($"Dictionary: cached (offline; cached {FormatTimestamp(cached.FetchedAt)})", Color.DarkOrange),
                DictionarySource.Cached cached =>
                    ($"Dictionary: cached ({cached.FallbackReason}; cached {FormatTimestamp(cached.FetchedAt)})", Color.DarkOrange),
                _ => ("Dictionary: unknown state", Color.DimGray),
            };

            _dictionarySourceLabel.Text = text;
            _dictionarySourceLabel.ForeColor = color;
        }

        private static string FormatTimestamp(DateTimeOffset ts)
            => ts.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

        private async void OnRefreshClick(object? sender, EventArgs e)
        {
            // Async-void try/catch discipline per ERROR_HANDLING + #20: any
            // exception escaping here would crash the form.
            _refreshDictionaryButton.Enabled = false;
            string priorText = _dictionarySourceLabel.Text;
            _dictionarySourceLabel.Text = "Dictionary: refreshing…";
            _dictionarySourceLabel.ForeColor = Color.DimGray;

            try
            {
                await _dictionaryService.RefreshAsync(_formClosedCts.Token).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                // Form is closing — nothing to do.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dictionary refresh failed unexpectedly.");
                _dictionarySourceLabel.Text = priorText;
                _dictionarySourceLabel.ForeColor = Color.Firebrick;
            }
            finally
            {
                if (!IsDisposed)
                {
                    _refreshDictionaryButton.Enabled = true;
                    RenderDictionarySource();
                }
            }
        }

        private void OnFormClosed(object? sender, FormClosedEventArgs e)
        {
            _dictionaryService.SourceChanged -= OnSourceChanged;
            _formClosedCts.Cancel();
            _formClosedCts.Dispose();
        }
    }
}
