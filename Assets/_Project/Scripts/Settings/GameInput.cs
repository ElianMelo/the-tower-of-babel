using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TowerOfBabel.Settings
{
    /// <summary>
    /// Every keyboard and mouse control of the game in one place, so all of them can be remapped from the settings
    /// menu. The actions live in the project's input asset (map "Player"); an action the asset in use does not have is
    /// created here with its default key, so the game always has the full set. Remapped keys are stored with the
    /// settings and applied as binding overrides.
    /// </summary>
    public static class GameInput
    {
        public const string MapName = "Player";

        /// <summary>One remappable key: an action, or one direction of the movement keys.</summary>
        public sealed class Binding
        {
            /// <summary>Stable identifier used in the saved settings.</summary>
            public string Id { get; }
            public string Label { get; }
            public string Group { get; }
            public string ActionName { get; }
            /// <summary>Composite part (up, down, left, right) for the movement keys; null otherwise.</summary>
            public string Part { get; }
            public string DefaultPath { get; }
            /// <summary>False for keys the menu itself depends on.</summary>
            public bool Rebindable { get; }
            public InputAction Action { get; internal set; }
            public int Index { get; internal set; } = -1;
            /// <summary>The control path in use: the remapped one, or the default.</summary>
            public string Path => Action != null && Index >= 0 ? Action.bindings[Index].effectivePath : DefaultPath;
            public bool IsDefault => string.Equals(Path, DefaultPath, StringComparison.OrdinalIgnoreCase);

            internal Binding(string id, string label, string group, string actionName, string part, string defaultPath, bool rebindable = true)
            { Id = id; Label = label; Group = group; ActionName = actionName; Part = part; DefaultPath = defaultPath; Rebindable = rebindable; }
        }

        [Serializable] private sealed class SavedBinding { public string id; public string path; }
        [Serializable] private sealed class SavedBindings { public List<SavedBinding> entries = new(); }

        private static readonly Binding[] Table =
        {
            new("Move/up", "Move forward", "Movement", "Move", "up", "<Keyboard>/w"),
            new("Move/down", "Move back", "Movement", "Move", "down", "<Keyboard>/s"),
            new("Move/left", "Move left", "Movement", "Move", "left", "<Keyboard>/a"),
            new("Move/right", "Move right", "Movement", "Move", "right", "<Keyboard>/d"),
            new("Jump", "Jump / fly up", "Movement", "Jump", null, "<Keyboard>/space"),
            new("Sprint", "Sprint", "Movement", "Sprint", null, "<Keyboard>/leftShift"),
            new("Descend", "Fly down (god mode)", "Movement", "Descend", null, "<Keyboard>/leftCtrl"),
            new("Interact", "Interact / build / gather", "Actions", "Interact", null, "<Keyboard>/e"),
            new("Scan", "Scan nearby construction", "Actions", "Scan", null, "<Keyboard>/1"),
            new("ToggleCursor", "Show or hide the mouse cursor", "Actions", "ToggleCursor", null, "<Keyboard>/backquote"),
            new("Inventory", "Inventory", "Menus", "Inventory", null, "<Keyboard>/i"),
            new("Upgrade", "Upgrades", "Menus", "Upgrade", null, "<Keyboard>/tab"),
            new("Emote", "Emotes", "Menus", "Emote", null, "<Keyboard>/r"),
            new("Menus", "Menu dock", "Menus", "Menus", null, "<Keyboard>/m"),
            new("Settings", "Settings / back", "Menus", "Settings", null, "<Keyboard>/escape", false),
            new("CheatMenu", "Developer controls", "Developer", "CheatMenu", null, "<Keyboard>/f3"),
            new("GraphicsPanel", "Developer graphics window", "Developer", "GraphicsPanel", null, "<Keyboard>/f4"),
            new("StartHost", "Start a local host", "Developer", "StartHost", null, "<Keyboard>/f1"),
            new("JoinGame", "Join the local host", "Developer", "JoinGame", null, "<Keyboard>/f2")
        };

        // Actions the interface scripts enable themselves while they are alive.
        private static readonly HashSet<string> OwnedByInterface = new() { "Inventory", "Upgrade", "Emote", "Menus" };

        private static InputActionAsset asset, supplement;
        private static bool resolved;
        private static InputAction move, jump, sprint, descend, interact, scan, toggleCursor, settings, cheatMenu, graphicsPanel, startHost, joinGame;

        /// <summary>Raised when a key was remapped or the defaults were restored.</summary>
        public static event Action BindingsChanged;

        public static IReadOnlyList<Binding> Bindings { get { Resolve(); return Table; } }
        /// <summary>The asset whose "Player" map supplies the actions, or null while only built-in defaults are in use.</summary>
        public static InputActionAsset Asset => asset;

        public static Vector2 Move { get { Resolve(); return move.ReadValue<Vector2>(); } }
        public static bool JumpPressed => Pressed(ref jump);
        public static bool JumpHeld => Held(ref jump);
        public static bool SprintHeld => Held(ref sprint);
        public static bool DescendHeld => Held(ref descend);
        public static bool InteractPressed => Pressed(ref interact);
        public static bool ScanPressed => Pressed(ref scan);
        public static bool ToggleCursorPressed => Pressed(ref toggleCursor);
        public static bool SettingsPressed => Pressed(ref settings);
        public static bool CheatMenuPressed => Pressed(ref cheatMenu);
        public static bool GraphicsPanelPressed => Pressed(ref graphicsPanel);
        public static bool StartHostPressed => Pressed(ref startHost);
        public static bool JoinGamePressed => Pressed(ref joinGame);

        private static bool Pressed(ref InputAction action) { Resolve(); return action.WasPressedThisFrame(); }
        private static bool Held(ref InputAction action) { Resolve(); return action.IsPressed(); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            asset = null; resolved = false; BindingsChanged = null;
            DestroySupplement();
            foreach (Binding binding in Table) { binding.Action = null; binding.Index = -1; }
        }

        private static void DestroySupplement()
        {
            if (supplement == null) return;
            supplement.Disable();
            if (Application.isPlaying) UnityEngine.Object.Destroy(supplement); else UnityEngine.Object.DestroyImmediate(supplement);
            supplement = null;
        }

        /// <summary>
        /// Uses <paramref name="actions"/> (the project's input asset) for every action it defines, so the interface
        /// scripts that read the same asset follow the remapped keys too.
        /// </summary>
        public static void Use(InputActionAsset actions)
        {
            if (actions == null || actions == asset) return;
            asset = actions;
            resolved = false;
            Resolve();
        }

        public static Binding Find(string id)
        {
            Resolve();
            foreach (Binding binding in Table) if (binding.Id == id) return binding;
            return null;
        }

        private static void Resolve()
        {
            if (resolved) return;
            resolved = true;
            DestroySupplement();
            supplement = ScriptableObject.CreateInstance<InputActionAsset>();
            supplement.name = "Game input defaults"; supplement.hideFlags = HideFlags.HideAndDontSave;
            InputActionMap extra = supplement.AddActionMap(MapName);
            foreach (Binding binding in Table)
            {
                InputAction action = asset != null ? asset.FindAction(MapName + "/" + binding.ActionName, false) : null;
                int index = action != null ? IndexOf(action, binding) : -1;
                if (index < 0)
                {
                    // The asset in use lacks this action (or its keyboard binding): supply the default.
                    action = extra.FindAction(binding.ActionName, false);
                    if (action == null)
                    {
                        if (binding.Part != null)
                        {
                            action = extra.AddAction(binding.ActionName, InputActionType.Value, expectedControlLayout: "Vector2");
                            var composite = action.AddCompositeBinding("2DVector");
                            foreach (Binding part in Table)
                                if (part.ActionName == binding.ActionName) composite.With(part.Part, part.DefaultPath);
                        }
                        else action = extra.AddAction(binding.ActionName, InputActionType.Button, binding.DefaultPath);
                    }
                    index = IndexOf(action, binding);
                }
                binding.Action = action; binding.Index = index;
            }
            move = Table[0].Action; jump = Find("Jump").Action; sprint = Find("Sprint").Action; descend = Find("Descend").Action;
            interact = Find("Interact").Action; scan = Find("Scan").Action; toggleCursor = Find("ToggleCursor").Action;
            settings = Find("Settings").Action; cheatMenu = Find("CheatMenu").Action; graphicsPanel = Find("GraphicsPanel").Action;
            startHost = Find("StartHost").Action; joinGame = Find("JoinGame").Action;
            LoadOverrides(GameSettings.Current.bindingOverrides, false);
            foreach (Binding binding in Table)
                if (!OwnedByInterface.Contains(binding.ActionName) || binding.Action.actionMap == extra) binding.Action.Enable();
        }

        // The keyboard or mouse binding of the action (of the composite part for the movement keys).
        private static int IndexOf(InputAction action, Binding binding)
        {
            var bindings = action.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                InputBinding candidate = bindings[i];
                if (candidate.isComposite) continue;
                if (binding.Part != null && !(candidate.isPartOfComposite && string.Equals(candidate.name, binding.Part, StringComparison.OrdinalIgnoreCase))) continue;
                if (binding.Part == null && candidate.isPartOfComposite) continue;
                string path = candidate.path ?? "";
                if (path.StartsWith("<Keyboard>", StringComparison.OrdinalIgnoreCase) || path.StartsWith("<Mouse>", StringComparison.OrdinalIgnoreCase)) return i;
            }
            return -1;
        }

        /// <summary>The key's name on the player's keyboard layout.</summary>
        public static string DisplayName(Binding binding)
        {
            Resolve();
            string text = null;
            try { text = binding.Action.GetBindingDisplayString(binding.Index, InputBinding.DisplayStringOptions.DontUseShortDisplayNames); }
            catch (Exception) { /* A device-less session has no layout to name the key with. */ }
            if (string.IsNullOrWhiteSpace(text))
                text = InputControlPath.ToHumanReadableString(binding.Path, InputControlPath.HumanReadableStringOptions.OmitDevice);
            return string.IsNullOrWhiteSpace(text) ? "Unbound" : text;
        }

        /// <summary>Another remappable key already using <paramref name="path"/>, or null.</summary>
        public static Binding Conflict(Binding binding, string path)
        {
            Resolve();
            foreach (Binding other in Table)
                if (other != binding && string.Equals(other.Path, path, StringComparison.OrdinalIgnoreCase)) return other;
            return null;
        }

        /// <summary>
        /// Maps <paramref name="binding"/> to <paramref name="path"/>. A key that another control used is swapped: that
        /// control takes this one's previous key, so nothing ends up unbound. Returns false for a fixed or empty key.
        /// </summary>
        public static bool SetBinding(Binding binding, string path, bool save = true)
        {
            Resolve();
            if (binding == null || !binding.Rebindable || string.IsNullOrEmpty(path)) return false;
            string previous = binding.Path;
            Binding other = Conflict(binding, path);
            if (other != null && !other.Rebindable) return false;
            Override(binding, path);
            if (other != null) Override(other, previous);
            Commit(save);
            return true;
        }

        private static void Override(Binding binding, string path)
        {
            bool enabled = binding.Action.enabled;
            if (enabled) binding.Action.Disable();
            if (string.Equals(path, binding.DefaultPath, StringComparison.OrdinalIgnoreCase)) binding.Action.RemoveBindingOverride(binding.Index);
            else binding.Action.ApplyBindingOverride(binding.Index, path);
            if (enabled) binding.Action.Enable();
        }

        /// <summary>
        /// Listens for the next key or mouse button and maps <paramref name="binding"/> to it. Escape cancels.
        /// <paramref name="finished"/> receives true when a key was taken.
        /// </summary>
        public static InputActionRebindingExtensions.RebindingOperation StartRebind(Binding binding, Action<bool> finished)
        {
            Resolve();
            if (binding == null || !binding.Rebindable) { finished?.Invoke(false); return null; }
            InputAction action = binding.Action;
            string previous = binding.Path;
            bool enabled = action.enabled;
            if (enabled) action.Disable();
            var operation = action.PerformInteractiveRebinding(binding.Index)
                .WithControlsExcluding("<Keyboard>/anyKey")
                .WithControlsExcluding("<Mouse>/position").WithControlsExcluding("<Mouse>/delta").WithControlsExcluding("<Mouse>/scroll")
                .WithControlsExcluding("<Pointer>/position").WithControlsExcluding("<Pointer>/delta")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnMatchWaitForAnother(.1f);
            operation.OnComplete(op =>
            {
                op.Dispose();
                if (enabled) action.Enable();
                string chosen = binding.Path;
                // Put the previous key back first so SetBinding can swap with a control that already used the new one.
                Override(binding, previous);
                bool accepted = SetBinding(binding, chosen);
                finished?.Invoke(accepted);
            });
            operation.OnCancel(op =>
            {
                op.Dispose();
                if (enabled) action.Enable();
                finished?.Invoke(false);
            });
            operation.Start();
            return operation;
        }

        public static void ResetBindings(bool save = true)
        {
            Resolve();
            foreach (Binding binding in Table)
                if (!binding.IsDefault) Override(binding, binding.DefaultPath);
            Commit(save);
        }

        private static void Commit(bool save)
        {
            GameSettings.Current.bindingOverrides = SaveOverrides();
            if (save) GameSettings.Save();
            BindingsChanged?.Invoke();
        }

        /// <summary>The remapped keys as JSON (only those that differ from their default).</summary>
        public static string SaveOverrides()
        {
            Resolve();
            var saved = new SavedBindings();
            foreach (Binding binding in Table)
                if (binding.Rebindable && !binding.IsDefault) saved.entries.Add(new SavedBinding { id = binding.Id, path = binding.Path });
            return saved.entries.Count == 0 ? "" : JsonUtility.ToJson(saved);
        }

        /// <summary>Replaces every remapped key with the ones in <paramref name="json"/> (empty restores the defaults).</summary>
        public static void LoadOverrides(string json, bool notify = true)
        {
            Resolve();
            SavedBindings saved = null;
            if (!string.IsNullOrEmpty(json))
            {
                try { saved = JsonUtility.FromJson<SavedBindings>(json); }
                catch (Exception) { saved = null; }
            }
            foreach (Binding binding in Table)
            {
                string path = binding.DefaultPath;
                if (saved != null && binding.Rebindable)
                    foreach (SavedBinding entry in saved.entries)
                        if (entry.id == binding.Id && !string.IsNullOrEmpty(entry.path)) path = entry.path;
                if (!string.Equals(binding.Path, path, StringComparison.OrdinalIgnoreCase)) Override(binding, path);
            }
            if (notify) BindingsChanged?.Invoke();
        }
    }
}
