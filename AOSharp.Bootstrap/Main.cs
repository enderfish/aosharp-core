using AOSharp.Bootstrap.IPC;
using AOSharp.Common.GameData;
using AOSharp.Common.Unmanaged.DataTypes;
using AOSharp.Common.Unmanaged.Imports;
using EasyHook;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace AOSharp.Bootstrap
{
    [Serializable]
    public class Main : IEntryPoint
    {
        private IPCServer _ipcPipe;
        private AppDomain _pluginAppDomain;
        private ManualResetEvent _connectEvent;
        private ManualResetEvent _unloadEvent;
        private static List<LocalHook> _hooks = new List<LocalHook>();
        private PluginProxy _pluginProxy;
        private ChatSocketListener _chatSocketListener;
        private bool _exiting = false;
        private DateTime _lastHeartbeat = DateTime.MinValue;

        private string _lastChatInput;
        private IntPtr _lastChatInputWindowPtr;

        public Main(RemoteHooking.IContext inContext, String inChannelName)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.File("AOSharp.Bootstrapper.txt", restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Debug)
                .CreateLogger();

            Log.Information("Bootstrap loaded in: {name} (PID={pid})",
                System.Diagnostics.Process.GetCurrentProcess().ProcessName,
                System.Diagnostics.Process.GetCurrentProcess().Id);

            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                string detail = args.ExceptionObject is Exception ex ? ex.ToString() : args.ExceptionObject?.ToString() ?? "(null)";
                Log.Fatal("Unhandled exception (IsTerminating={term}): {detail}", args.IsTerminating, detail);
                Log.CloseAndFlush();
            };

            _connectEvent = new ManualResetEvent(false);
            _unloadEvent = new ManualResetEvent(false);
            _chatSocketListener = new ChatSocketListener();

            //Setup IPC server that will be used for handling API requests and events.
            _ipcPipe = new IPCServer(inChannelName);
            _ipcPipe.OnConnected = OnIPCClientConnected;
            _ipcPipe.OnDisconnected = OnIPCClientDisconnected;
            _ipcPipe.RegisterCallback((byte)HookOpCode.LoadAssembly, typeof(LoadAssemblyMessage), OnAssembliesChanged);
            _ipcPipe.Start();
        }

        public void Run(RemoteHooking.IContext inContext, String inChannelName)
        {
            //If the GameController doesn't connect within 10 seconds we will unload the dll.
            if (!_connectEvent.WaitOne(10000))
                return;

            //Wait for the signal to unload the dll.
            _unloadEvent.WaitOne();
        }

        private void OnIPCClientConnected(IPCServer pipe)
        {
            //Notify the main thread we recieved a connection from the GameController.
            _connectEvent.Set();

            try
            {
                SetupHooks();
            }
            catch (Exception e)
            {
                Log.Error(e, "SetupHooks failed");
            }
        }

        private void OnIPCClientDisconnected(IPCServer pipe)
        {
            _exiting = true;
            _ipcPipe.Close();
        }

        private void OnAssembliesChanged(object pipe, IPCMessage message)
        {
            try
            {
                LoadAssemblyMessage msg = message as LoadAssemblyMessage;

                Log.Information("OnAssembliesChanged: received {count} assemblies", msg?.Assemblies?.Count() ?? 0);

                if (_pluginAppDomain != null)
                {
                    //Release existing AppDomain
                    AppDomain.Unload(_pluginAppDomain);
                    _pluginAppDomain = null;
                }

                if (!msg.Assemblies.Any())
                    return;

                AppDomainSetup setup = new AppDomainSetup()
                {
                    ApplicationBase = AppDomain.CurrentDomain.BaseDirectory
                };

                _pluginAppDomain = AppDomain.CreateDomain("plugins", null, setup);

                Type type = typeof(PluginProxy);
                _pluginProxy = (PluginProxy)_pluginAppDomain.CreateInstanceAndUnwrap(type.Assembly.FullName, type.FullName);

                _pluginProxy.LoadCore(_pluginAppDomain.BaseDirectory + "\\AOSharp.Core.dll");

                foreach (string assembly in msg.Assemblies)
                {
                    Log.Information("OnAssembliesChanged: loading {asm}", assembly);
                    _pluginProxy.LoadPlugin(assembly);
                    Log.Information("OnAssembliesChanged: loaded {asm}", assembly);
                }
            }
            catch (Exception e)
            {
                //TODO: Send IPC message back to loader on error
                Log.Error(e, "OnAssembliesChanged failed");
            }
        }

        private unsafe void SetupHooks()
        {
            CreateHook("N3.dll",
                        "?AddChildDynel@n3Playfield_t@@QAEXPAVn3Dynel_t@@ABVVector3_t@@ABVQuaternion_t@@@Z",
                        new N3Playfield_t.DAddChildDynel(N3Playfield_t__AddChildDynel_Hook));

            CreateHook("Gamecode.dll",
                        "?RunEngine@n3EngineClientAnarchy_t@@UAEXM@Z",
                        new N3EngineClientAnarchy_t.DRunEngine(N3EngineClientAnarchy_RunEngine_Hook));

            CreateHook("Gamecode.dll",
                        "?N3Msg_SendInPlayMessage@n3EngineClientAnarchy_t@@QBE_NXZ",
                        new N3EngineClientAnarchy_t.DSendInPlayMessage(N3EngineClientAnarchy_SendInPlayMessage_Hook));

            CreateHook("GUI.dll",
                        "?TeleportStartedMessage@FlowControlModule_t@@CAXXZ",
                        new FlowControlModule_t.DTeleportStartedMessage(FlowControlModule_t_TeleportStarted_Hook));

            CreateHook("Gamecode.dll",
                        "?TeleportFailed@TeleportTrier_t@@QAEXXZ",
                        new TeleportTrier_t.DTeleportFailed(TeleportTrier_t_TeleportFailed_Hook));

            CreateHook("GUI.dll",
                        "?ModuleActivated@OptionPanelModule_c@@UAEX_N@Z",
                        new OptionPanelModule_c.DModuleActivated(OptionPanelModule_ModuleActivated_Hook));

            CreateHook("GUI.dll",
                        "?ViewDeleted@WindowController_c@@QAEXPAVView@@@Z",
                        new WindowController_c.DViewDeleted(WindowController_ViewDeleted_Hook));

            CreateHook("GUI.dll",
                        "?RemoveWindow@WindowController_c@@QAEXPAVWindow@@@Z",
                        new WindowController_c.DRemoveWindow(WindowController_RemoveWindow_Hook));

            CreateHook("MessageProtocol.dll",
                        "?DataBlockToMessage@@YAPAVMessage_t@@IPAX@Z",
                        new MessageProtocol.DDataBlockToMessage(DataBlockToMessage_Hook));

            CreateHook("Gamecode.dll",
                        "?PlayfieldInit@n3EngineClientAnarchy_t@@UAEXI@Z",
                        new N3EngineClientAnarchy_t.DPlayfieldInit(N3EngineClientAnarchy_PlayfieldInit_Hook));

            CreateHook("GUI.dll",
                        "?SlotJoinTeamRequest@TeamViewModule_c@@AAEXABVIdentity_t@@ABV?$basic_string@DU?$char_traits@D@std@@V?$allocator@D@2@@std@@@Z",
                        new TeamViewModule_c.DSlotJoinTeamRequest(TeamViewModule_SlotJoinTeamRequest_Hook));

            CreateHook("GUI.dll",
                        "?SlotJoinTeamRequestFailedTooLow@TeamViewModule_c@@AAEXABVIdentity_t@@@Z",
                        new TeamViewModule_c.DSlotJoinTeamRequestFailed(TeamViewModule_SlotJoinTeamRequestFailed_Hook));

            CreateHook("GUI.dll",
                        "?SlotJoinTeamRequestFailedTooHigh@TeamViewModule_c@@AAEXABVIdentity_t@@@Z",
                        new TeamViewModule_c.DSlotJoinTeamRequestFailed(TeamViewModule_SlotJoinTeamRequestFailed_Hook));

            CreateHook("Gamecode.dll",
                        "?N3Msg_PerformSpecialAction@n3EngineClientAnarchy_t@@QAE_NABVIdentity_t@@@Z",
                        new N3EngineClientAnarchy_t.DPerformSpecialAction(N3EngineClientAnarchy_PerformSpecialAction_Hook));

            CreateHook("GUI.dll",
                        "?HandleGroupMessage@ChatGUIModule_c@@AAEXPBUGroupMessage_t@Client_c@ppj@@@Z",
                        new ChatGUIModule_t.DHandleGroupAction(HandleGroupMessage_Hook));

            CreateHook("Gamecode.dll",
                        "?N3Msg_CastNanoSpell@n3EngineClientAnarchy_t@@QAEXABVIdentity_t@@0@Z",
                        new N3EngineClientAnarchy_t.DCastNanoSpell(N3EngineClientAnarchy_CastNanoSpell_Hook));

            CreateHook("GUI.dll",
                        "?SlotContainerOpened@InventoryGUIModule_c@@AAEXABVIdentity_t@@_N1@Z",
                        new InventoryGUIModule_c.DContainerOpened(ContainerOpened_Hook));

            CreateHook("GUI.dll",
                        "?SetValue@ButtonBase_c@@UAEXABVVariant@@_N@Z",
                        new ButtonBase_c.DSetValue(ButtonBase_SetValue_Hook));

            CreateHook("GUI.dll",
                        "?SlotButtonToggled@CheckBox_c@@AAEX_N@Z",
                        new CheckBox_c.DSlotButtonToggled(CheckBox_SlotButtonToggled_Hook));

            //Will revisit this when it's needed.
            CreateHook("AFCM.dll",
                        "?GetID@DynamicID_t@@QAEHPBD_N@Z",
                        new DynamicID_t.DGetID(DynamicID_GetID_Hook));

            CreateHook("GUI.dll",
                        "?Select@MultiListViewItem_c@@QAEX_N0@Z",
                        new MultiListViewItem_c.DSelect(MultiListViewItem_Select_Hook));

            CreateHook("Connection.dll",
                        "?Send@Connection_t@@QAEHIIPBX@Z",
                        new Connection_t.DSend(Send_Hook));

            CreateHook("ws2_32.dll",
                        "recv",
                        new Ws2_32.RecvDelegate(WsRecv_Hook));

            if (ProcessChatInputPatcher.Patch(out IntPtr pProcessCommand, out IntPtr pGetCommand))
            {
                CommandInterpreter_c.ProcessChatInput = Marshal.GetDelegateForFunctionPointer<CommandInterpreter_c.DProcessChatInput>(pProcessCommand);
                CommandInterpreter_c.GetCommand = Marshal.GetDelegateForFunctionPointer<CommandInterpreter_c.DGetCommand>(pGetCommand);
                CreateHook(pProcessCommand, new CommandInterpreter_c.DProcessChatInput(ProcessChatInput_Hook));
                CreateHook(pGetCommand, new CommandInterpreter_c.DGetCommand(GetCommand_Hook));
            }
        }

        private void CreateHook(string module, string funcName, Delegate newFunc)
        {
            try
            {
                CreateHook(LocalHook.GetProcAddress(module, funcName), newFunc);
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to hook {module}::{func}", module, funcName);
            }
        }

        public void CreateHook(IntPtr origFunc, Delegate newFunc)
        {
            LocalHook hook = LocalHook.Create(origFunc, newFunc, this);
            hook.ThreadACL.SetExclusiveACL(new Int32[] { 0 });
            _hooks.Add(hook);
        }

        private void UnhookAll()
        {
            foreach (LocalHook hook in _hooks)
                hook.Dispose();
        }

        public unsafe int WsRecv_Hook(int socket, IntPtr buffer, int len, int flags)
        {
            int bytesRead = Ws2_32.recv(socket, buffer, len, flags);

            if (_pluginProxy != null && socket == ChatSocketListener.Socket)
            {
                try
                {
                    byte[] trimmedBuffer = new byte[bytesRead];
                    Marshal.Copy(buffer, trimmedBuffer, 0, bytesRead);

                    List<byte[]> packets = _chatSocketListener.ProcessBuffer(trimmedBuffer);

                    foreach (byte[] packet in packets)
                        _pluginProxy.ChatRecv(packet);
                }
                catch (Exception e)
                {
                    Log.Error(e, "WsRecv_Hook: plugin chat recv failed");
                }
            }

            return bytesRead;
        }

        public int DynamicID_GetID_Hook(IntPtr pThis, string name, bool unk)
        {
            try
            {
                int customId = (_pluginProxy?.GetDynamicIDOverride(name)).GetValueOrDefault(0);
                return customId > 0 ? customId : DynamicID_t.GetID(pThis, name, unk);
            }
            catch (Exception e)
            {
                Log.Error(e, "DynamicID_GetID_Hook failed for name={name}", name);
                return DynamicID_t.GetID(pThis, name, unk);
            }
        }

        public void MultiListViewItem_Select_Hook(IntPtr pThis, bool selected, bool unk)
        {
            MultiListViewItem_c.Select(pThis, selected, unk);

            try
            {
                _pluginProxy?.MultiListViewItemSelectionChanged(pThis, selected);
            }
            catch (Exception e)
            {
                Log.Error(e, "MultiListViewItem_Select_Hook: plugin callback failed");
            }
        }

        public void CheckBox_SlotButtonToggled_Hook(IntPtr pThis, bool enabled)
        {
            try
            {
                _pluginProxy?.CheckBoxToggled(pThis, enabled);
            }
            catch (Exception e)
            {
                Log.Error(e, "CheckBox_SlotButtonToggled_Hook: plugin callback failed");
            }

            CheckBox_c.SlotButtonToggled(pThis, enabled);
        }

        public void ButtonBase_SetValue_Hook(IntPtr pThis, IntPtr pVariant, bool unk)
        {
            try
            {
                if (!Variant_c.AsBool(pVariant))
                    _pluginProxy?.ButtonPressed(pThis);
            }
            catch (Exception e)
            {
                Log.Error(e, "ButtonBase_SetValue_Hook: plugin callback failed");
            }

            ButtonBase_c.SetValue(pThis, pVariant, unk);
        }

        public void ContainerOpened_Hook(IntPtr pThis, ref Identity identity, bool unk, bool unk2)
        {
            try
            {
                _pluginProxy?.ContainerOpened((int)identity.Type, identity.Instance);
            }
            catch (Exception e)
            {
                Log.Error(e, "ContainerOpened_Hook: plugin callback failed");
            }

            InventoryGUIModule_c.ContainerOpened(pThis, ref identity, unk, unk2);
        }

        public byte ProcessChatInput_Hook(IntPtr pThis, IntPtr pWindow, IntPtr pCmdText)
        {
            try
            {
                StdString tokenized = StdString.Create();
                ChatGUIModule_t.ExpandChatTextArgs(tokenized.Pointer, pCmdText);
                _lastChatInput = tokenized.ToString();
                _lastChatInputWindowPtr = pWindow;
            }
            catch (Exception e)
            {
                Log.Error(e, "ProcessChatInput_Hook: failed to capture input");
            }

            return CommandInterpreter_c.ProcessChatInput(pThis, pWindow, pCmdText);
        }

        public IntPtr GetCommand_Hook(IntPtr pThis, IntPtr pCmdText, bool unk)
        {
            IntPtr result;
            result = CommandInterpreter_c.GetCommand(pThis, pCmdText, unk);
            Log.Information("GetCommand_Hook: unk={unk} result={result} lastInput={input} proxyNull={proxyNull}",
                unk, result, _lastChatInput, _pluginProxy == null);

            try
            {
                if (result == IntPtr.Zero && unk && _pluginProxy != null)
                    _pluginProxy?.UnknownChatCommand(_lastChatInputWindowPtr, _lastChatInput);
            }
            catch (Exception e)
            {
                Log.Error(e, "GetCommand_Hook: plugin callback failed for input={input}", _lastChatInput);
            }

            return result;
        }

        public void HandleGroupMessage_Hook(IntPtr pThis, IntPtr pGroupMessage)
        {
            bool cancel = false;

            try
            {
                if (_pluginProxy != null)
                    cancel = _pluginProxy.HandleGroupMessage(pGroupMessage);
            }
            catch (Exception e)
            {
                Log.Error(e, "HandleGroupMessage_Hook: plugin callback failed");
            }

            if (!cancel)
                ChatGUIModule_t.HandleGroupMessage(pThis, pGroupMessage);
        }

        private int Send_Hook(IntPtr pConnection, uint unk, int len, byte[] buf)
        {
            try
            {
                if (_pluginProxy != null)
                {
                    _pluginProxy.SentPacket(buf);
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Send_Hook: plugin callback failed");
            }

            return Connection_t.Send(pConnection, unk, len, buf);
        }

        private IntPtr DataBlockToMessage_Hook(uint size, byte[] dataBlock)
        {
            //Let the client process the packet before we inspect it.
            IntPtr pMsg = MessageProtocol.DataBlockToMessage(size, dataBlock);

            try
            {
                if (_pluginProxy != null)
                {
                    _pluginProxy.DataBlockToMessage(dataBlock);
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "DataBlockToMessage_Hook: plugin callback failed");
            }

            return pMsg;
        }

        private void WindowController_ViewDeleted_Hook(IntPtr pThis, IntPtr pView)
        {
            try
            {
                if (_pluginProxy != null)
                {
                    _pluginProxy.ViewDeleted(pView);
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "WindowController_ViewDeleted_Hook: plugin callback failed");
            }

            WindowController_c.ViewDeleted(pThis, pView);
        }

        private void WindowController_RemoveWindow_Hook(IntPtr pThis, IntPtr pWindow)
        {
            try
            {
                if (_pluginProxy != null)
                {
                    _pluginProxy.WindowDeleted(pWindow);
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "WindowController_RemoveWindow_Hook: plugin callback failed");
            }

            WindowController_c.RemoveWindow(pThis, pWindow);
        }

        private void TeamViewModule_SlotJoinTeamRequest_Hook(IntPtr pThis, ref Identity identity, IntPtr pName)
        {
            try
            {
                if (_pluginProxy != null)
                {
                    _pluginProxy.JoinTeamRequest((int)identity.Type, identity.Instance, pName);
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "TeamViewModule_SlotJoinTeamRequest_Hook: plugin callback failed");
            }
        }

        private void TeamViewModule_SlotJoinTeamRequestFailed_Hook(IntPtr pThis, ref Identity identity)
        {
            try
            {
                IntPtr pEngine = N3Engine_t.GetInstance();

                if (pEngine == IntPtr.Zero)
                    return;

                N3EngineClientAnarchy_t.TeamJoinRequest(pEngine, ref identity, true);
            }
            catch (Exception e)
            {
                Log.Error(e, "TeamViewModule_SlotJoinTeamRequestFailed_Hook failed");
            }
        }

        private bool N3EngineClientAnarchy_PerformSpecialAction_Hook(IntPtr pThis, ref Identity identity)
        {
            bool specialActionResult = N3EngineClientAnarchy_t.PerformSpecialAction(pThis, ref identity);

            try
            {
                if (_pluginProxy != null)
                {
                    if (specialActionResult)
                        _pluginProxy.ClientPerformedSpecialAction((int)identity.Type, identity.Instance);
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "N3EngineClientAnarchy_PerformSpecialAction_Hook: plugin callback failed");
            }

            return specialActionResult;
        }

        private unsafe bool N3EngineClientAnarchy_CastNanoSpell_Hook(IntPtr pThis, ref Identity target, ref Identity spell)
        {
            try
            {
                if (_pluginProxy != null)
                {
                    if (_pluginProxy.AttemptingSpellCast((int)target.Type, target.Instance, (int)spell.Type, spell.Instance))
                        return false;
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "N3EngineClientAnarchy_CastNanoSpell_Hook: plugin callback failed");
            }

            return N3EngineClientAnarchy_t.CastNanoSpell(pThis, ref target, ref spell);
        }

        public void OptionPanelModule_ModuleActivated_Hook(IntPtr pThis, bool unk)
        {
            OptionPanelModule_c.ModuleActivated(pThis, unk);

            try
            {
                if (_pluginProxy != null)
                {
                    if (unk)
                        _pluginProxy.OptionPanelActivated(pThis, unk);
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "OptionPanelModule_ModuleActivated_Hook: plugin callback failed");
            }
        }

        public unsafe void FlowControlModule_t_TeleportStarted_Hook()
        {
            try
            {
                if (_pluginProxy != null)
                {
                    if (!*FlowControlModule_t.pIsTeleporting)
                        _pluginProxy.TeleportStarted();
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "FlowControlModule_t_TeleportStarted_Hook: plugin callback failed");
            }

            FlowControlModule_t.TeleportStartedMessage();
        }

        public void TeleportTrier_t_TeleportFailed_Hook(IntPtr pThis)
        {
            try
            {
                if (_pluginProxy != null)
                    _pluginProxy.TeleportFailed();
            }
            catch (Exception e)
            {
                Log.Error(e, "TeleportTrier_t_TeleportFailed_Hook: plugin callback failed");
            }

            TeleportTrier_t.TeleportFailed(pThis);
        }

        public bool N3EngineClientAnarchy_SendInPlayMessage_Hook(IntPtr pThis)
        {
            bool result = N3EngineClientAnarchy_t.SendInPlayMessage(pThis);
            try
            {
                if (_pluginProxy != null)
                    _pluginProxy.TeleportEnded();
            }
            catch (Exception e)
            {
                Log.Error(e, "N3EngineClientAnarchy_SendInPlayMessage_Hook: plugin callback failed");
            }

            return result;
        }

        public void N3EngineClientAnarchy_PlayfieldInit_Hook(IntPtr pThis, uint id)
        {
            N3EngineClientAnarchy_t.PlayfieldInit(pThis, id);

            try
            {
                if (_pluginProxy != null)
                    _pluginProxy.PlayfieldInit(id);
            }
            catch (Exception e)
            {
                Log.Error(e, "N3EngineClientAnarchy_PlayfieldInit_Hook: plugin callback failed (playfieldId={id})", id);
            }
        }

        public void N3EngineClientAnarchy_RunEngine_Hook(IntPtr pThis, float deltaTime)
        {
            try
            {
                if ((DateTime.UtcNow - _lastHeartbeat).TotalSeconds >= 10.0)
                {
                    Log.Debug("Heartbeat: pluginProxy={hasProxy} exiting={exiting}",
                        _pluginProxy != null, _exiting);
                    _lastHeartbeat = DateTime.UtcNow;
                }

                if (_exiting)
                {
                    UnhookAll();

                    _pluginProxy?.Teardown();

                    N3EngineClientAnarchy_t.RunEngine(pThis, deltaTime);

                    _pluginProxy?.Update(deltaTime);

                    if (_pluginAppDomain != null)
                    {
                        AppDomain.Unload(_pluginAppDomain);
                        _pluginAppDomain = null;
                    }

                    //Notify the main thread that it is time to unload the dll.
                    _unloadEvent.Set();
                    _exiting = false;
                }
                else if (_pluginProxy != null)
                {
                    _pluginProxy.RunPluginInitializations();

                    _pluginProxy.EarlyUpdate(deltaTime);

                    N3EngineClientAnarchy_t.RunEngine(pThis, deltaTime);

                    _pluginProxy.Update(deltaTime);
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "N3EngineClientAnarchy_RunEngine_Hook failed");
            }
        }

        public void N3Playfield_t__AddChildDynel_Hook(IntPtr pThis, IntPtr pDynel, IntPtr pos, IntPtr rot)
        {
            //Let the client load the dynel before we notify the GameController of it's spawn.
            N3Playfield_t.AddChildDynel(pThis, pDynel, pos, rot);

            try
            {
                if (_pluginProxy != null)
                    _pluginProxy.DynelSpawned(pDynel);
            }
            catch (Exception e)
            {
                Log.Error(e, "N3Playfield_t__AddChildDynel_Hook: plugin callback failed");
            }
        }
    }
}
