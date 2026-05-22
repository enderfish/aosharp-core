using AOSharp.Common.GameData;
using AOSharp.Common.SharedEventArgs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace AOSharp.Bootstrap
{
    public class CoreDelegates
    {
        public delegate void InitDelegate();
        public InitDelegate Init;
        public delegate void TeardownDelegate();
        public TeardownDelegate Teardown;
        public delegate void OnPluginLoadedDelegate(Assembly assembly);
        public OnPluginLoadedDelegate OnPluginLoaded;
        public delegate void DynelSpawnedDelegate(IntPtr pDynel);
        public DynelSpawnedDelegate DynelSpawned;
        public delegate void DataBlockToMessageDelegate(byte[] datablock);
        public DataBlockToMessageDelegate DataBlockToMessage;
        public delegate void ChatRecvDelegate(byte[] packet);
        public ChatRecvDelegate ChatRecv;
        public delegate void SentPacketDelegate(byte[] datablock);
        public SentPacketDelegate SentPacket;
        public delegate void UpdateDelegate(float deltaTime);
        public UpdateDelegate Update;
        public delegate void EarlyUpdateDelegate(float deltaTime);
        public EarlyUpdateDelegate EarlyUpdate;
        public delegate void TeleportStartedDelegate();
        public TeleportStartedDelegate TeleportStarted;
        public delegate void TeleportEndedDelegate();
        public TeleportEndedDelegate TeleportEnded;
        public delegate void TeleportFailedDelegate();
        public TeleportFailedDelegate TeleportFailed;
        public delegate void JoinTeamRequestDelegate(Identity pIdentity, IntPtr pName);
        public JoinTeamRequestDelegate JoinTeamRequest;
        public delegate void ClientPerformedSpecialActionDelegate(Identity pIdentity);
        public ClientPerformedSpecialActionDelegate ClientPerformedSpecialAction;
        public delegate void PlayfieldInitDelegate(uint id);
        public PlayfieldInitDelegate PlayfieldInit;
        public delegate void OptionPanelActivatedDelegate(IntPtr pOptionPanelModule, bool unk);
        public OptionPanelActivatedDelegate OptionPanelActivated;
        public delegate void ViewDeletedDelegate(IntPtr pView);
        public ViewDeletedDelegate ViewDeleted;
        public delegate void WindowDeletedDelegate(IntPtr pWindow);
        public WindowDeletedDelegate WindowDeleted;
        public delegate void AttemptingSpellCastDelegate(AttemptingSpellCastEventArgs args);
        public AttemptingSpellCastDelegate AttemptingSpellCast;
        public delegate void UnknownCommandDelegate(IntPtr pWindow, string command);
        public UnknownCommandDelegate UnknownChatCommand;
        public delegate void HandleGroupMessageDelegate(GroupMessageEventArgs args);
        public HandleGroupMessageDelegate HandleGroupMessage;
        public delegate void ContainerOpenedDelegate(Identity identity);
        public ContainerOpenedDelegate ContainerOpened;
        public delegate void ButtonPressedDelegate(IntPtr pButton);
        public ButtonPressedDelegate ButtonPressed;
        public delegate void CheckBoxToggledDelegate(IntPtr pCheckBox, bool enabled);
        public CheckBoxToggledDelegate CheckBoxToggled;
        public delegate void MultiListViewItemSelectionChangedDelegate(IntPtr pItem, bool selected);
        public MultiListViewItemSelectionChangedDelegate MultiListViewItemSelectionChanged;
        public delegate int GetDynamicIDOverrideDelegate(string name);
        public GetDynamicIDOverrideDelegate GetDynamicIDOverride;
    }

    public class PluginProxy : MarshalByRefObject
    {
        private static CoreDelegates _coreDelegates;
        private List<Plugin> _plugins = new List<Plugin>();

        public int GetDynamicIDOverride(string name) => (_coreDelegates?.GetDynamicIDOverride?.Invoke(name)).GetValueOrDefault(0);

        public void UnknownChatCommand(IntPtr pWindow, string command) => _coreDelegates?.UnknownChatCommand?.Invoke(pWindow, command);

        public void DataBlockToMessage(byte[] datablock) => _coreDelegates?.DataBlockToMessage?.Invoke(datablock);

        public void ChatRecv(byte[] packet) => _coreDelegates?.ChatRecv?.Invoke(packet);

        public void SentPacket(byte[] datablock) => _coreDelegates?.SentPacket?.Invoke(datablock);

        public unsafe void JoinTeamRequest(int type, int id, IntPtr pName) => _coreDelegates?.JoinTeamRequest?.Invoke(new Identity((IdentityType)type, id), pName);

        public unsafe void ClientPerformedSpecialAction(int type, int id) => _coreDelegates?.ClientPerformedSpecialAction?.Invoke(new Identity((IdentityType)type, id));

        public void DynelSpawned(IntPtr pDynel) => _coreDelegates?.DynelSpawned?.Invoke(pDynel);

        public void Update(float deltaTime) => _coreDelegates?.Update?.Invoke(deltaTime);

        public void EarlyUpdate(float deltaTime) => _coreDelegates?.EarlyUpdate?.Invoke(deltaTime);

        public void TeleportStarted() => _coreDelegates?.TeleportStarted?.Invoke();

        public void TeleportEnded() => _coreDelegates?.TeleportEnded?.Invoke();

        public void TeleportFailed() => _coreDelegates?.TeleportFailed?.Invoke();

        public void PlayfieldInit(uint id) => _coreDelegates?.PlayfieldInit?.Invoke(id);

        public void OptionPanelActivated(IntPtr pOptionPanelModule, bool unk) => _coreDelegates?.OptionPanelActivated?.Invoke(pOptionPanelModule, unk);

        public void ViewDeleted(IntPtr pView) => _coreDelegates?.ViewDeleted?.Invoke(pView);

        public void WindowDeleted(IntPtr pWindow) => _coreDelegates?.WindowDeleted?.Invoke(pWindow);

        public void ContainerOpened(int type, int id) => _coreDelegates?.ContainerOpened?.Invoke(new Identity((IdentityType)type, id));

        public void ButtonPressed(IntPtr pButton) => _coreDelegates?.ButtonPressed?.Invoke(pButton);

        public void CheckBoxToggled(IntPtr pCheckBox, bool enabled) => _coreDelegates?.CheckBoxToggled?.Invoke(pCheckBox, enabled);

        public void MultiListViewItemSelectionChanged(IntPtr pItem, bool selected) => _coreDelegates?.MultiListViewItemSelectionChanged?.Invoke(pItem, selected);

        public unsafe bool AttemptingSpellCast(int targetType, int targetId, int spellType, int spellId)
        {
            AttemptingSpellCastEventArgs eventArgs = new AttemptingSpellCastEventArgs(new Identity((IdentityType)targetType, targetId), new Identity((IdentityType)spellType, spellId));
            _coreDelegates?.AttemptingSpellCast?.Invoke(eventArgs);
            return eventArgs.Blocked;
        }

        public bool HandleGroupMessage(IntPtr pGroupMessage)
        {
            GroupMessageEventArgs eventArgs = new GroupMessageEventArgs(new GroupMessage(pGroupMessage));
            _coreDelegates?.HandleGroupMessage?.Invoke(eventArgs);
            return eventArgs.Cancel;
        }

        private T CreateDelegate<T>(Assembly assembly, string className, string methodName) where T : class
        {
            Type t = assembly.GetType(className);

            if (t == null)
                return default(T);

            MethodInfo m = t.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);

            if (m == null)
                return default(T);

            return Delegate.CreateDelegate(typeof(T), m) as T;
        }

        public void LoadCore(string assemblyPath)
        {
            //Load main assembly
            var assembly = Assembly.LoadFile(assemblyPath);

            //Load references
            foreach (var reference in assembly.GetReferencedAssemblies())
            {
                Assembly.Load(reference);
            }

            _coreDelegates = new CoreDelegates()
            {
                Init = CreateDelegate<CoreDelegates.InitDelegate>(assembly, "AOSharp.Core.Game", "Init"),
                Teardown = CreateDelegate<CoreDelegates.TeardownDelegate>(assembly, "AOSharp.Core.Game", "Teardown"),
                OnPluginLoaded = CreateDelegate<CoreDelegates.OnPluginLoadedDelegate>(assembly, "AOSharp.Core.Game", "OnPluginLoaded"),
                Update = CreateDelegate<CoreDelegates.UpdateDelegate>(assembly, "AOSharp.Core.Game", "OnUpdateInternal"),
                EarlyUpdate = CreateDelegate<CoreDelegates.EarlyUpdateDelegate>(assembly, "AOSharp.Core.Game", "OnEarlyUpdateInternal"),
                DynelSpawned = CreateDelegate<CoreDelegates.DynelSpawnedDelegate>(assembly, "AOSharp.Core.DynelManager", "OnDynelSpawned"),
                TeleportStarted = CreateDelegate<CoreDelegates.TeleportStartedDelegate>(assembly, "AOSharp.Core.Game", "OnTeleportStarted"),
                TeleportEnded = CreateDelegate<CoreDelegates.TeleportEndedDelegate>(assembly, "AOSharp.Core.Game", "OnTeleportEnded"),
                TeleportFailed = CreateDelegate<CoreDelegates.TeleportFailedDelegate>(assembly, "AOSharp.Core.Game", "OnTeleportFailed"),
                PlayfieldInit = CreateDelegate<CoreDelegates.PlayfieldInitDelegate>(assembly, "AOSharp.Core.Game", "OnPlayfieldInit"),
                OptionPanelActivated = CreateDelegate<CoreDelegates.OptionPanelActivatedDelegate>(assembly, "AOSharp.Core.UI.Options.OptionPanel", "OnOptionPanelActivated"),
                ViewDeleted = CreateDelegate<CoreDelegates.ViewDeletedDelegate>(assembly, "AOSharp.Core.UI.UIController", "OnViewDeleted"),
                WindowDeleted = CreateDelegate<CoreDelegates.WindowDeletedDelegate>(assembly, "AOSharp.Core.UI.UIController", "OnWindowDeleted"),
                DataBlockToMessage = CreateDelegate<CoreDelegates.DataBlockToMessageDelegate>(assembly, "AOSharp.Core.Network", "OnInboundMessage"),
                ChatRecv = CreateDelegate<CoreDelegates.ChatRecvDelegate>(assembly, "AOSharp.Core.Network", "OnChatMessage"),
                SentPacket = CreateDelegate<CoreDelegates.SentPacketDelegate>(assembly, "AOSharp.Core.Network", "OnOutboundMessage"),
                JoinTeamRequest = CreateDelegate<CoreDelegates.JoinTeamRequestDelegate>(assembly, "AOSharp.Core.Team", "OnJoinTeamRequest"),
                ClientPerformedSpecialAction = CreateDelegate<CoreDelegates.ClientPerformedSpecialActionDelegate>(assembly, "AOSharp.Core.PerkAction", "OnClientPerformedSpecialAction"),
                AttemptingSpellCast = CreateDelegate<CoreDelegates.AttemptingSpellCastDelegate>(assembly, "AOSharp.Core.MiscClientEvents", "OnAttemptingSpellCast"),
                UnknownChatCommand = CreateDelegate<CoreDelegates.UnknownCommandDelegate>(assembly, "AOSharp.Core.UI.Chat", "OnUnknownCommand"),
                HandleGroupMessage = CreateDelegate<CoreDelegates.HandleGroupMessageDelegate>(assembly, "AOSharp.Core.UI.Chat", "OnGroupMessage"),
                ContainerOpened = CreateDelegate<CoreDelegates.ContainerOpenedDelegate>(assembly, "AOSharp.Core.Inventory.Inventory", "OnContainerOpened"),
                ButtonPressed = CreateDelegate<CoreDelegates.ButtonPressedDelegate>(assembly, "AOSharp.Core.UI.UIController", "OnButtonPressed"),
                CheckBoxToggled = CreateDelegate<CoreDelegates.CheckBoxToggledDelegate>(assembly, "AOSharp.Core.UI.UIController", "OnCheckBoxToggled"),
                MultiListViewItemSelectionChanged = CreateDelegate<CoreDelegates.MultiListViewItemSelectionChangedDelegate>(assembly, "AOSharp.Core.UI.UIController", "OnMultiListViewItemStateChanged"),
                GetDynamicIDOverride = CreateDelegate<CoreDelegates.GetDynamicIDOverrideDelegate>(assembly, "AOSharp.Core.UI.UIController", "OnDynamicIDResolve")
            };

            _coreDelegates.Init();
        }

        public void LoadPlugin(string assemblyPath)
        {
            try
            {
                //Load main assembly
                Assembly assembly = Assembly.LoadFrom(assemblyPath);

                //Load references
                foreach (AssemblyName reference in assembly.GetReferencedAssemblies())
                {
                    if (reference.Name == "AOSharp.Common" ||
                        reference.Name == "AOSharp.Bootstrap" ||
                        reference.Name == "AOSharp.Core")
                        continue;

                    try
                    {
                        Assembly.Load(reference);
                    }
                    catch (FileNotFoundException)
                    {
                        Assembly.LoadFrom($"{Path.GetDirectoryName(assemblyPath)}\\{reference.Name}.dll");
                    }
                }

                Type[] exportedTypes = assembly.GetExportedTypes();

                foreach (Type type in exportedTypes)
                {
                    if (type.GetInterface("AOSharp.Core.IAOPluginEntry") == null)
                        continue;

                    MethodInfo runMethod = type.GetMethod("Run", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null)
                        ?? type.GetMethod("Run", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(string) }, null);

                    if (runMethod == null)
                        continue;

                    MethodInfo teardownMethod = type.GetMethod("Teardown", BindingFlags.Public | BindingFlags.Instance);

                    if (teardownMethod == null)
                        continue;

                    ConstructorInfo constructor = type.GetConstructor(Type.EmptyTypes);

                    if (constructor == null)
                        continue;

                    _coreDelegates.OnPluginLoaded(assembly);

                    object instance = constructor.Invoke(null);

                    if (instance == null)
                        continue;

                    _plugins.Add(new Plugin(instance, runMethod, teardownMethod, Path.GetDirectoryName(assemblyPath)));
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error("LoadPlugin failed for {path}: {type}: {msg}", assemblyPath, ex.GetType().Name, ex.Message);
            }
        }

        public void RunPluginInitializations()
        {
            foreach (Plugin plugin in _plugins)
            {
                if (plugin.Initialized)
                    continue;

                plugin.Initialize();
            }
        }

        public void Teardown()
        {
            _coreDelegates?.Teardown?.Invoke();

            foreach (Plugin plugin in _plugins)
                plugin.Teardown();
        }
    }

    public class Plugin
    {
        public bool Initialized;

        private object _instance;
        private MethodInfo _runMethod;
        private MethodInfo _teardownMethod;
        private string _assemblyDir;

        public Plugin(object instance, MethodInfo runMethod, MethodInfo teardownMethod, string assemblyDir)
        {
            Initialized = false;
            _instance = instance;
            _runMethod = runMethod;
            _teardownMethod = teardownMethod;
            _assemblyDir = assemblyDir;
        }

        public void Initialize()
        {
            try
            {
                var dirProp = _instance.GetType().GetProperty("PluginDirectory",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                dirProp?.SetValue(_instance, _assemblyDir);

                var args = _runMethod.GetParameters().Length > 0
                    ? new object[] { _assemblyDir }
                    : null;
                _runMethod.Invoke(_instance, args);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error("Plugin.Initialize failed for {type}: {msg}", _instance.GetType().FullName, ex.Message);
            }

            Initialized = true;
        }

        public void Teardown()
        {
            try
            {
                _teardownMethod.Invoke(_instance, null);
            }
            catch { }
        }
    }
}
