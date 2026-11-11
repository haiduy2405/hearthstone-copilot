using System;
using System.Diagnostics;
using System.IO;
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
    /// the UI thread and shows the result in a click-through panel. Never touches the game client.
    /// </summary>
    public class CopilotPlugin : IPlugin
    {
        private static readonly string DataDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HearthstoneCopilot");
        private static readonly string LogFile = Path.Combine(DataDir, "plugin.log");
        private static readonly string SnapDir = Path.Combine(DataDir, "snapshots");

        private volatile bool _enabled;
        private CopilotPanel _panel;
        private MenuItem _menu;
        private CancellationTokenSource _cts;
        private string _lastSignature;
        private long _version;
        private long _lastTickMs;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private string _hdtVersion = "?";
        private GameSnapshot _latest;
        private string _gameId = "none";
        private volatile bool _pendingRecord;
        private int _lethalSavedTurn = -1;

        public string Name => "Hearthstone Copilot";
        public string Description => "Read-only tactical copilot: exact-lethal detection shown in a compact panel.";
        public string ButtonText => "Open data folder";
        public string Author => "Duy";
        public Version Version => new Version(0, 1, 0);
        public MenuItem MenuItem => _menu;

        public void OnLoad()
        {
            try
            {
                Directory.CreateDirectory(SnapDir);
                _hdtVersion = typeof(Hearthstone_Deck_Tracker.Plugins.IPlugin).Assembly.GetName().Version.ToString();
                _panel = new CopilotPanel();
                _menu = new MenuItem { Header = "Copilot: save snapshot now" };
                _menu.Click += (s, e) => SaveSnapshot("manual");
                GameEvents.OnTurnStart.Add(OnTurnStart);
                GameEvents.OnGameStart.Add(() =>
                {
                    if (!_enabled) return;
                    _gameId = Guid.NewGuid().ToString("N").Substring(0, 8);
                    _lethalSavedTurn = -1;
                    _lastSignature = null;
                    Log("Game start " + _gameId);
                });
                GameEvents.OnInMenu.Add(() => { if (_enabled) _panel?.HidePanel(); });
                _enabled = true;
                Log("Loaded v" + Version + " against HDT " + _hdtVersion);
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

        public void OnUpdate()
        {
            if (!_enabled) return;
            // HDT calls this ~every 100 ms; analyse at most ~4x per second.
            long now = _clock.ElapsedMilliseconds;
            if (now - _lastTickMs < 250) return;
            _lastTickMs = now;

            try
            {
                var game = Core.Game;
                if (game == null || !game.IsRunning || game.IsInMenu) { _panel?.HidePanel(); return; }
                if (!game.IsConstructedMatch && !game.IsTraditionalHearthstoneMatch) { _panel?.HidePanel(); return; }

                var snap = SnapshotAdapter.Build(game, _hdtVersion, _gameId);
                if (snap == null) return;
                _latest = snap;

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
                    var text = Presenter.Format(snap, result);
                    if ((result.Status == LethalStatus.Proven || result.Status == LethalStatus.Likely) && snap.Turn != _lethalSavedTurn)
                    {
                        _lethalSavedTurn = snap.Turn;
                        SaveSnapshot(snap, "lethal", result.Status.ToString());
                    }
                    if (version != Interlocked.Read(ref _version) || token.IsCancellationRequested) return; // stale
                    Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (_enabled && version == Interlocked.Read(ref _version))
                            _panel?.SetText(text + "\n(" + sw.ElapsedMilliseconds + " ms, " + result.NodesVisited + " nodes)");
                    }));
                }, token);
            }
            catch (Exception ex)
            {
                Log("OnUpdate error: " + ex);
            }
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
