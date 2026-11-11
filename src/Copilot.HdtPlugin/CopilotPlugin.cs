using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Copilot.Engine;
using Copilot.Engine.Lethal;
using Copilot.Engine.Model;
using Hearthstone_Deck_Tracker.API;
using Hearthstone_Deck_Tracker.Enums;
using Hearthstone_Deck_Tracker.Plugins;
using Newtonsoft.Json;

namespace Copilot.HdtPlugin
{
    /// <summary>
    /// Read-only HDT plugin: builds a snapshot from HDT's game state, runs the exact-lethal solver off
    /// the UI thread and shows a big LETHAL banner when there is one. Details appear on demand.
    /// Never touches the game client.
    /// </summary>
    public class CopilotPlugin : IPlugin
    {
        private static readonly string DataDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HearthstoneCopilot");
        private static readonly string LogFile = Path.Combine(DataDir, "plugin.log");
        private static readonly string SnapDir = Path.Combine(DataDir, "snapshots");
        private static readonly string ConfigFile = Path.Combine(DataDir, "config.json");

        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);

        private volatile bool _enabled;
        private CopilotPanel _panel;
        private PanelConfig _cfg = new PanelConfig();
        private MenuItem _menu;
        private CancellationTokenSource _cts;
        private string _lastSignature;
        private long _version;
        private long _lastTickMs;
        private long _lastKeyMs;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private string _hdtVersion = "?";
        private GameSnapshot _latest;
        private string _gameId = "none";
        private volatile bool _pendingRecord;
        private int _lethalSavedTurn = -1;
        private string _soundKey = "";
        private PanelView _view;
        private bool _expanded;

        public string Name => "Hearthstone Copilot";
        public string Description => "Read-only lethal detector: shows a big LETHAL banner; details on demand (default key F8).";
        public string ButtonText => "Open data folder";
        public string Author => "Duy";
        public Version Version => new Version(0, 3, 1);
        public MenuItem MenuItem => _menu;

        public void OnLoad()
        {
            try
            {
                Directory.CreateDirectory(SnapDir);
                _cfg = PanelConfig.Load(ConfigFile, Log);
                _hdtVersion = typeof(Hearthstone_Deck_Tracker.Plugins.IPlugin).Assembly.GetName().Version.ToString();
                BuildPanel();

                _menu = new MenuItem { Header = "Hearthstone Copilot" };
                var save = new MenuItem { Header = "Save snapshot now" };
                save.Click += (s, e) => SaveSnapshot("manual");
                var toggle = new MenuItem { Header = "Show/hide details" };
                toggle.Click += (s, e) => ToggleDetails();
                var reload = new MenuItem { Header = "Reload config.json" };
                reload.Click += (s, e) => ReloadConfig();
                _menu.Items.Add(save);
                _menu.Items.Add(toggle);
                _menu.Items.Add(reload);

                GameEvents.OnTurnStart.Add(OnTurnStart);
                GameEvents.OnInMenu.Add(() => { if (_enabled) HideAll(); });
                GameEvents.OnGameStart.Add(() =>
                {
                    if (!_enabled) return;
                    _gameId = Guid.NewGuid().ToString("N").Substring(0, 8);
                    _lethalSavedTurn = -1;
                    _soundKey = "";
                    _lastSignature = null;
                    Log("Game start " + _gameId);
                });
                _enabled = true;
                Log("Loaded v" + Version + " against HDT " + _hdtVersion + " (hotkey " + _cfg.Hotkey + ")");
            }
            catch (Exception ex)
            {
                Log("OnLoad failed: " + ex);
                throw; // HDT disables a plugin that throws on load; better than half-working
            }
        }

        public void OnUnload()
        {
            _enabled = false;
            try { _cts?.Cancel(); } catch { }
            try { _panel?.Close(); } catch { }
            _panel = null;
            Log("Unloaded");
        }

        public void OnButtonPress()
        {
            try { Process.Start("explorer.exe", DataDir); } catch (Exception ex) { Log("Open folder failed: " + ex.Message); }
        }

        private void BuildPanel()
        {
            try { _panel?.Close(); } catch { }
            _panel = new CopilotPanel(_cfg);
            _panel.ToggleRequested += ToggleDetails;
        }

        private void ReloadConfig()
        {
            try
            {
                _cfg = PanelConfig.Load(ConfigFile, Log);
                BuildPanel();
                _panel.Render(_view, _expanded, _cfg.Hotkey);
                Log("Config reloaded");
            }
            catch (Exception ex) { Log("Reload failed: " + ex.Message); }
        }

        private void ToggleDetails()
        {
            _expanded = !_expanded;
            _panel?.Render(_view, _expanded, _cfg.Hotkey);
        }

        private void HideAll()
        {
            _view = null;
            _panel?.HidePanel();
        }

        public void OnUpdate()
        {
            if (!_enabled) return;
            long now = _clock.ElapsedMilliseconds;

            // Hotkey: polled (HDT gives plugins no key events). Only while the panel is visible.
            if (_view != null && _view.Level != PanelLevel.Hidden && now - _lastKeyMs > 300
                && (GetAsyncKeyState(_cfg.HotkeyVirtualKey()) & 0x8001) != 0)
            {
                _lastKeyMs = now;
                ToggleDetails();
            }

            // HDT calls this ~every 100 ms; analyse at most ~4x per second.
            if (now - _lastTickMs < 250) return;
            _lastTickMs = now;

            try
            {
                var game = Core.Game;
                if (game == null || !game.IsRunning || game.IsInMenu) { HideAll(); return; }
                if (!game.IsConstructedMatch && !game.IsTraditionalHearthstoneMatch) { HideAll(); return; }

                var snap = SnapshotAdapter.Build(game, _hdtVersion, _gameId);
                if (snap == null) return;
                _latest = snap;
                if (_view == null) _panel?.ShowIdle();

                // OnTurnStart fires before HDT updates turn tags, so record once the state has settled.
                if (_pendingRecord && snap.MyTurn && snap.Me.Mana > 0)
                {
                    _pendingRecord = false;
                    SaveSnapshot(snap, "turn", null);
                }

                var sig = snap.Signature();
                if (sig == _lastSignature) return;
                _lastSignature = sig;

                _cts?.Cancel();
                _cts = new CancellationTokenSource();
                var token = _cts.Token;
                long version = Interlocked.Increment(ref _version);
                Task.Run(() =>
                {
                    var sw = Stopwatch.StartNew();
                    var result = LethalSolver.Solve(snap, token: token);
                    var view = Presenter.Build(snap, result);
                    if (view.Details != null)
                        view.Details += "\n(" + sw.ElapsedMilliseconds + " ms, " + result.NodesVisited + " nodes)";
                    bool lethal = result.Status == LethalStatus.Proven || result.Status == LethalStatus.Likely;
                    if (lethal && snap.Turn != _lethalSavedTurn)
                    {
                        _lethalSavedTurn = snap.Turn;
                        SaveSnapshot(snap, "lethal", result.Status.ToString());
                    }
                    if (version != Interlocked.Read(ref _version) || token.IsCancellationRequested) return; // stale
                    Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (!_enabled || version != Interlocked.Read(ref _version)) return;
                        _view = view;
                        _panel?.Render(view, _expanded, _cfg.Hotkey);
                        if (_cfg.Sound && view.Level != PanelLevel.Hidden)
                        {
                            var key = snap.GameId + ":" + snap.Turn + ":" + view.Level;
                            if (key != _soundKey) { _soundKey = key; Beep(view.Level); }
                        }
                    }));
                }, token);
            }
            catch (Exception ex)
            {
                Log("OnUpdate error: " + ex);
            }
        }

        private static void Beep(PanelLevel level)
        {
            // Console.Beep blocks, so play on a pool thread. Distinct patterns: lethal rises, danger is low and double.
            Task.Run(() =>
            {
                try
                {
                    switch (level)
                    {
                        case PanelLevel.Lethal: Console.Beep(900, 140); Console.Beep(1200, 140); Console.Beep(1600, 260); break;
                        case PanelLevel.Likely: Console.Beep(900, 140); Console.Beep(1200, 220); break;
                        case PanelLevel.Danger: Console.Beep(400, 220); Console.Beep(400, 220); break;
                    }
                }
                catch { try { System.Media.SystemSounds.Exclamation.Play(); } catch { } }
            });
        }

        private void OnTurnStart(ActivePlayer who)
        {
            if (!_enabled) return;
            // Record one settled snapshot per own turn so real games become offline fixtures.
            if (who == ActivePlayer.Player) _pendingRecord = true;
        }

        private void SaveSnapshot(string reason)
        {
            var snap = _latest ?? SnapshotAdapter.Build(Core.Game, _hdtVersion, _gameId);
            if (snap == null) { Log("SaveSnapshot(" + reason + "): no game state"); return; }
            SaveSnapshot(snap, reason, null);
        }

        private void SaveSnapshot(GameSnapshot snap, string reason, string engineStatus)
        {
            try
            {
                var name = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + "-g" + snap.GameId + "-t" + snap.Turn + "-" + reason + ".json";
                // "expected" stays null until a human reviews the recording and fills it in.
                var wrapper = new { name = reason, engineStatus, expected = (string)null, snapshot = snap };
                File.WriteAllText(Path.Combine(SnapDir, name), JsonConvert.SerializeObject(wrapper, Formatting.Indented));
                Log("Saved " + name);
            }
            catch (Exception ex)
            {
                Log("SaveSnapshot failed: " + ex.Message);
            }
        }

        private static readonly object LogLock = new object();

        private static void Log(string line)
        {
            try
            {
                lock (LogLock)
                {
                    Directory.CreateDirectory(DataDir);
                    File.AppendAllText(LogFile, DateTime.Now.ToString("s") + " " + line + Environment.NewLine);
                }
            }
            catch { /* logging must never break HDT */ }
        }
    }
}
