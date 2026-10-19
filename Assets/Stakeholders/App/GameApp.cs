using System;
using Stakeholders.App.UI;
using Stakeholders.Core;
using UnityEngine;

namespace Stakeholders.App
{
    /// <summary>
    /// Starts the game in any scene: no scene setup or prefabs required.
    /// </summary>
    public static class Bootstrap
    {
        /// <summary>Set to false (e.g. from another script) to stop the game from auto-starting.</summary>
        public static bool Enabled = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (!Enabled || UnityEngine.Object.FindAnyObjectByType<GameApp>() != null) return;
            var go = new GameObject("Stakeholders");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<GameApp>();
        }
    }

    public abstract class AppScreen
    {
        protected readonly GameApp App;
        public UnityEngine.UIElements.VisualElement Root { get; protected set; }
        protected AppScreen(GameApp app) { App = app; }
        public abstract void Build();
        public virtual void Tick(float dt) { }
        public virtual void Dispose() { }
    }

    /// <summary>Root controller: owns the UI, the current screen and the current game session.</summary>
    public class GameApp : MonoBehaviour
    {
        public static GameApp Instance { get; private set; }
        public UiRoot Ui { get; private set; }
        public GameSession Session { get; private set; }
        public NetBridge Net { get; private set; }
        AppScreen _screen;
        int _lastAutosaveMonth = -1;

        string _testName;

        public string PlayerName
        {
            get => _testName ?? PlayerPrefs.GetString("stk_name", Environment.UserName ?? "Founder");
            set
            {
                if (Automation.Active) { _testName = value; return; } // automated runs leave no trace
                PlayerPrefs.SetString("stk_name", value ?? "");
                PlayerPrefs.Save();
            }
        }

        void Awake()
        {
            Instance = this;
            Application.targetFrameRate = 60;
            Application.runInBackground = true;
            Ui = new UiRoot(gameObject);
            Sfx.Init(gameObject);
        }

        void Start()
        {
            if (!Automation.TryStart(this)) ShowMainMenu();
        }

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.25f);
            try
            {
                Session?.Tick(dt);
                _screen?.Tick(dt);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            AutoSave();
        }

        void AutoSave()
        {
            if (Automation.Active || Session == null || Session.IsMultiplayer || Session.Authoritative == null) return;
            var s = Session.Authoritative;
            if (s.Phase == GamePhase.GameOver) return;
            int month = (s.Day - 1) / GameCalendar.DaysPerMonth;
            if (month != _lastAutosaveMonth)
            {
                if (_lastAutosaveMonth >= 0) SaveSystem.Save(s, SaveSystem.AutoSlot);
                _lastAutosaveMonth = month;
            }
        }

        public void Show(AppScreen screen)
        {
            _screen?.Dispose();
            _screen = screen;
            screen.Build();
            Ui.SetScreen(screen.Root);
        }

        public void ShowMainMenu()
        {
            EndSession();
            Show(new MainMenuScreen(this));
        }

        public void EndSession()
        {
            if (Session != null)
            {
                if (!Automation.Active && !Session.IsMultiplayer && Session.Authoritative != null && Session.Authoritative.Phase != GamePhase.GameOver)
                    SaveSystem.Save(Session.Authoritative, SaveSystem.AutoSlot);
                Session.Shutdown();
                Session = null;
            }
            if (Net != null) Net.Shutdown();
        }

        public NetBridge EnsureNet()
        {
            if (Net == null) Net = NetBridge.Create();
            return Net;
        }

        public void StartSolo(GameSettings settings)
        {
            EndSession();
            settings.Multiplayer = false;
            var state = GameFactory.Create(settings);
            Session = new LocalSession(state, 0);
            _lastAutosaveMonth = -1;
            Show(new GameScreen(this));
        }

        public bool LoadSolo(string slot)
        {
            var state = SaveSystem.Load(slot);
            if (state == null) return false;
            EndSession();
            state.Settings.Multiplayer = false;
            if (state.Phase == GamePhase.Lobby) state.Phase = GamePhase.Running;
            int me = 0;
            foreach (var p in state.Players) if (!p.IsAI) { me = p.Id; break; }
            foreach (var p in state.Players) p.Connected = true;
            Session = new LocalSession(state, me);
            _lastAutosaveMonth = (state.Day - 1) / GameCalendar.DaysPerMonth;
            Show(new GameScreen(this));
            return true;
        }

        public void BeginSession(GameSession session)
        {
            Session = session;
            Show(new GameScreen(this));
        }

        public void SaveNow()
        {
            if (Session?.Authoritative == null) return;
            bool ok = SaveSystem.Save(Session.Authoritative, SaveSystem.ManualSlot);
            Ui.Toast(ok ? "Game saved." : "Saving failed.", ok ? NoticeKind.Success : NoticeKind.Danger);
        }

        void OnApplicationQuit()
        {
            EndSession();
        }
    }
}
