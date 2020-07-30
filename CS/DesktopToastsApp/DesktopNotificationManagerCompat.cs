// ******************************************************************
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the MIT License (MIT).
// THE CODE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
// IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
// DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
// TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH
// THE CODE OR THE USE OR OTHER DEALINGS IN THE CODE.
// ******************************************************************

/*
 * License for the RegisterActivator portion of code from FrecherxDachs

The MIT License (MIT)

Copyright (c) 2020 Michael Dietrich

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
 * */

using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using Windows.UI.Notifications;
using static DesktopNotifications.NotificationActivator;

namespace DesktopNotifications
{
    namespace Internal
    {
        /// <summary>
        /// This is temporary to hide internal code when copy/pasted into a project... when as a NuGet package, internal classes will be hidden automatically.
        /// </summary>
        internal static class InteralSharedLogic
        {
            internal static string _aumid;

            internal static bool _registeredAumidAndComServer;
            internal static bool _registeredActivator;

            internal static void EnsureRegistered()
            {
                // If not registered AUMID yet
                if (!_registeredAumidAndComServer)
                {
                    // Check if Desktop Bridge
                    if (DesktopBridgeHelpers.IsRunningAsUwp())
                    {
                        // Implicitly registered, all good!
                        _registeredAumidAndComServer = true;
                    }

                    else
                    {
                        // Otherwise, incorrect usage
                        throw new Exception("You must call RegisterApplication first.");
                    }
                }
            }
        }

        /// <summary>
        /// Code from https://github.com/qmatteoq/DesktopBridgeHelpers/edit/master/DesktopBridge.Helpers/Helpers.cs
        /// </summary>
        internal class DesktopBridgeHelpers
        {
            const long APPMODEL_ERROR_NO_PACKAGE = 15700L;

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            static extern int GetCurrentPackageFullName(ref int packageFullNameLength, StringBuilder packageFullName);

            private static bool? _isRunningAsUwp;
            public static bool IsRunningAsUwp()
            {
                if (_isRunningAsUwp == null)
                {
                    if (IsWindows7OrLower)
                    {
                        _isRunningAsUwp = false;
                    }
                    else
                    {
                        int length = 0;
                        StringBuilder sb = new StringBuilder(0);
                        int result = GetCurrentPackageFullName(ref length, sb);

                        sb = new StringBuilder(length);
                        result = GetCurrentPackageFullName(ref length, sb);

                        _isRunningAsUwp = result != APPMODEL_ERROR_NO_PACKAGE;
                    }
                }

                return _isRunningAsUwp.Value;
            }

            private static bool IsWindows7OrLower
            {
                get
                {
                    int versionMajor = Environment.OSVersion.Version.Major;
                    int versionMinor = Environment.OSVersion.Version.Minor;
                    double version = versionMajor + (double)versionMinor / 10;
                    return version <= 6.1;
                }
            }
        }
    }

    public class NotificationActivatiedEventArgs
    {
        public string Arguments { get; set; }

        public NotificationUserInput UserInput { get; set; }
    }

    public static class DesktopNotificationManagerCompat
    {
        /// <summary>
        /// Event that is triggered when a notification or notification button is clicked.
        /// </summary>
        public static event EventHandler<NotificationActivatiedEventArgs> Activated;

        public static void OnActivated(string args, NotificationUserInput input, string aumid)
        {
            Activated?.Invoke(null, new NotificationActivatiedEventArgs()
            {
                Arguments = args,
                UserInput = input
            });
        }

        public const string TOAST_ACTIVATED_LAUNCH_ARG = "-ToastActivated";

        /// <summary>
        /// If you're not using UWP, MSIX, or sparse packages, you must call this method to register your AUMID with the Compat library and to
        /// register your COM CLSID and EXE in LocalServer32 registry. Feel free to call this regardless, and we will no-op if running
        /// under Desktop Bridge. Call this upon application startup, before calling any other APIs. Note that the display name and icon will NOT update if changed until either all toasts are cleared, or the system is rebooted.
        /// </summary>
        /// <param name="aumid">An AUMID that uniquely identifies your application.</param>
        /// <param name="displayName">Your app's display name, which will appear on toasts and within Action Center.</param>
        /// <param name="iconPath">Your app's icon, which will appear on toasts and within Action Center.</param>
        public static void RegisterApplication(string aumid, string displayName, string iconPath)
        {
            if (string.IsNullOrWhiteSpace(aumid))
            {
                throw new ArgumentException("You must provide an AUMID.", nameof(aumid));
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("You must provide a display name.", nameof(displayName));
            }

            if (string.IsNullOrWhiteSpace(iconPath))
            {
                throw new ArgumentException("You must provide an icon path.", nameof(iconPath));
            }

            // If running as Desktop Bridge
            if (Internal.DesktopBridgeHelpers.IsRunningAsUwp())
            {
                // Clear the AUMID since Desktop Bridge doesn't use it, and then we're done.
                // Desktop Bridge apps are registered with platform through their manifest.
                // Their LocalServer32 key is also registered through their manifest.
                Internal.InteralSharedLogic._aumid = null;
                Internal.InteralSharedLogic._registeredAumidAndComServer = true;
                return;
            }

            Internal.InteralSharedLogic._aumid = aumid;

            using (var rootKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\AppUserModelId\" + aumid))
            {
                rootKey.SetValue("DisplayName", displayName);
                rootKey.SetValue("IconUri", iconPath);
                rootKey.SetValue("IconBackgroundColor", "FFDDDDDD"); // Only appears in the settings page, always setting to light gray since app icon is known to work well on light gray anyways since that's how it appears in Action Center
            }

            Internal.InteralSharedLogic._registeredAumidAndComServer = true;

            // https://stackoverflow.com/questions/24069352/c-sharp-typebuilder-generate-class-with-function-dynamically
            AssemblyName aName = new AssemblyName("DynamicComActivator");
            AssemblyBuilder aBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(aName, System.Reflection.Emit.AssemblyBuilderAccess.RunAndSave);

            // For a single-module assembly, the module name is usually 
            // the assembly name plus an extension.
            ModuleBuilder mb = aBuilder.DefineDynamicModule(aName.Name, aName.Name + ".dll");

            // Create class which extends NotificationActivator
            TypeBuilder tb = mb.DefineType(
                name: "MyNotificationActivator",
                attr: TypeAttributes.Public,
                parent: typeof(NotificationActivator),
                interfaces: new Type[0]);

            tb.SetCustomAttribute(new CustomAttributeBuilder(
                con: typeof(GuidAttribute).GetConstructor(new Type[] { typeof(string) }),
                constructorArgs: new object[] { "50cfb67f-bc8a-477d-938c-93cf6bfb3321" }));

            tb.SetCustomAttribute(new CustomAttributeBuilder(
                con: typeof(ComVisibleAttribute).GetConstructor(new Type[] { typeof(bool) }),
                constructorArgs: new object[] { true }));

            tb.SetCustomAttribute(new CustomAttributeBuilder(
                con: typeof(ComSourceInterfacesAttribute).GetConstructor(new Type[] { typeof(Type) }),
                constructorArgs: new object[] { typeof(INotificationActivationCallback) }));

            tb.SetCustomAttribute(new CustomAttributeBuilder(
                con: typeof(ClassInterfaceAttribute).GetConstructor(new Type[] { typeof(ClassInterfaceType) }),
                constructorArgs: new object[] { ClassInterfaceType.None }));

            // Create the OnActivated function
            MethodBuilder mbOnActivated = tb.DefineMethod(
                name: nameof(NotificationActivator.OnActivated),
                attributes: MethodAttributes.Public | MethodAttributes.Virtual,
                returnType: typeof(void),
                parameterTypes: new Type[] { typeof(string), typeof(NotificationUserInput), typeof(string) });

            tb.DefineMethodOverride(
                methodInfoBody: mbOnActivated,
                methodInfoDeclaration: typeof(NotificationActivator).GetMethod(nameof(NotificationActivator.OnActivated)));

            // Create the OnActivated function body
            ILGenerator ilGen = mbOnActivated.GetILGenerator();

            ilGen.Emit(OpCodes.Nop);
            ilGen.Emit(OpCodes.Ldarg_1);
            ilGen.Emit(OpCodes.Ldarg_2);
            ilGen.Emit(OpCodes.Ldarg_3);

            Type[] paramListWID = { typeof(string), typeof(NotificationUserInput), typeof(string) };
            ilGen.EmitCall(OpCodes.Call, typeof(DesktopNotificationManagerCompat).GetMethod(nameof(OnActivated)), paramListWID);

            ilGen.Emit(OpCodes.Ret);


            var activatorType = tb.CreateType();

            RegisterActivator(activatorType);
        }

        private static void RegisterComServer(Type activatorType, String exePath)
        {
            // We register the EXE to start up when the notification is activated
            string regString = String.Format("SOFTWARE\\Classes\\CLSID\\{{{0}}}\\LocalServer32", activatorType.GUID);
            var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(regString);

            // Include a flag so we know this was a toast activation and should wait for COM to process
            // We also wrap EXE path in quotes for extra security
            key.SetValue(null, '"' + exePath + '"' + " " + TOAST_ACTIVATED_LAUNCH_ARG);
        }

        /// <summary>
        /// Registers the activator type as a COM server client so that Windows can launch your activator. If not using UWP/MSIX/sparse, you must call <see cref="RegisterApplication(string, string, string)"/> first.
        /// </summary>
        /// <typeparam name="T">Your implementation of <see cref="NotificationActivator"/>. Must have GUID and ComVisible attributes on class.</typeparam>
        public static void RegisterActivator(Type activatorType)
        {
            if (!Internal.DesktopBridgeHelpers.IsRunningAsUwp())
            {
                if (Internal.InteralSharedLogic._aumid == null)
                {
                    throw new InvalidOperationException("You must call RegisterApplication first.");
                }

                String exePath = Process.GetCurrentProcess().MainModule.FileName;
                RegisterComServer(activatorType, exePath);

                using (var rootKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\AppUserModelId\" + Internal.InteralSharedLogic._aumid))
                {
                    rootKey.SetValue("CustomActivator", string.Format("{{{0}}}", activatorType.GUID));
                }
            }

            // Big thanks to FrecherxDachs for figuring out the following code which works in .NET Core 3: https://github.com/FrecherxDachs/UwpNotificationNetCoreTest
            var uuid = activatorType.GUID;
            uint _cookie;
            CoRegisterClassObject(uuid, new NotificationActivatorClassFactory(activatorType), CLSCTX_LOCAL_SERVER,
                REGCLS_MULTIPLEUSE, out _cookie);

            Internal.InteralSharedLogic._registeredActivator = true;
        }

        [ComImport]
        [Guid("00000001-0000-0000-C000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IClassFactory
        {
            [PreserveSig]
            int CreateInstance(IntPtr pUnkOuter, ref Guid riid, out IntPtr ppvObject);

            [PreserveSig]
            int LockServer(bool fLock);
        }

        private const int CLASS_E_NOAGGREGATION = -2147221232;
        private const int E_NOINTERFACE = -2147467262;
        private const int CLSCTX_LOCAL_SERVER = 4;
        private const int REGCLS_MULTIPLEUSE = 1;
        private const int S_OK = 0;
        private static readonly Guid IUnknownGuid = new Guid("00000000-0000-0000-C000-000000000046");

        private class NotificationActivatorClassFactory : IClassFactory
        {
            private Type _activatorType;

            public NotificationActivatorClassFactory(Type activatorType)
            {
                _activatorType = activatorType;
            }

            public int CreateInstance(IntPtr pUnkOuter, ref Guid riid, out IntPtr ppvObject)
            {
                ppvObject = IntPtr.Zero;

                if (pUnkOuter != IntPtr.Zero)
                    Marshal.ThrowExceptionForHR(CLASS_E_NOAGGREGATION);

                if (riid == _activatorType.GUID || riid == IUnknownGuid)
                    // Create the instance of the .NET object
                    ppvObject = Marshal.GetComInterfaceForObject(Activator.CreateInstance(_activatorType),
                        typeof(INotificationActivationCallback));
                else
                    // The object that ppvObject points to does not support the
                    // interface identified by riid.
                    Marshal.ThrowExceptionForHR(E_NOINTERFACE);
                return S_OK;
            }

            public int LockServer(bool fLock)
            {
                return S_OK;
            }
        }

        [DllImport("ole32.dll")]
        private static extern int CoRegisterClassObject(
            [MarshalAs(UnmanagedType.LPStruct)] Guid rclsid,
            [MarshalAs(UnmanagedType.IUnknown)] object pUnk,
            uint dwClsContext,
            uint flags,
            out uint lpdwRegister);
    }

    public static class ToastNotificationManagerCompat
    {
        /// <summary>
        /// Creates a toast notifier. If you're not using UWP/MSIX/sparse, you must have called <see cref="DesktopNotificationManagerCompat.RegisterApplication(string, string, string)"/> first, or this will throw an exception.
        /// </summary>
        /// <returns></returns>
        public static ToastNotifier CreateToastNotifier()
        {
            Internal.InteralSharedLogic.EnsureRegistered();

            if (Internal.InteralSharedLogic._aumid != null)
            {
                // Non-Desktop Bridge
                return ToastNotificationManager.CreateToastNotifier(Internal.InteralSharedLogic._aumid);
            }
            else
            {
                // Desktop Bridge
                return ToastNotificationManager.CreateToastNotifier();
            }
        }

        /// <summary>
        /// Gets the <see cref="ToastNotificationHistoryCompat"/> object. You must have called <see cref="RegisterActivator{T}"/> first (and also <see cref="RegisterAumidAndComServer(string)"/> if you're a classic Win32 app), or this will throw an exception.
        /// </summary>
        public static ToastNotificationHistoryCompat History
        {
            get
            {
                Internal.InteralSharedLogic.EnsureRegistered();

                return new ToastNotificationHistoryCompat(Internal.InteralSharedLogic._aumid);
            }
        }

        /// <summary>
        /// Gets a boolean representing whether http images can be used within toasts. This is true if running with package identity (MSIX or sparse package).
        /// </summary>
        public static bool CanUseHttpImages => Internal.DesktopBridgeHelpers.IsRunningAsUwp();
    }

    /// <summary>
    /// Manages the toast notifications for an app including the ability the clear all toast history and removing individual toasts.
    /// </summary>
    public sealed class ToastNotificationHistoryCompat
    {
        private string _aumid;
        private ToastNotificationHistory _history;

        /// <summary>
        /// Do not call this. Instead, call <see cref="DesktopNotificationManagerCompat.History"/> to obtain an instance.
        /// </summary>
        /// <param name="aumid"></param>
        internal ToastNotificationHistoryCompat(string aumid)
        {
            _aumid = aumid;
            _history = ToastNotificationManager.History;
        }

        /// <summary>
        /// Removes all notifications sent by this app from action center.
        /// </summary>
        public void Clear()
        {
            if (_aumid != null)
            {
                _history.Clear(_aumid);
            }
            else
            {
                _history.Clear();
            }
        }

        /// <summary>
        /// Gets all notifications sent by this app that are currently still in Action Center.
        /// </summary>
        /// <returns>A collection of toasts.</returns>
        public IReadOnlyList<ToastNotification> GetHistory()
        {
            return _aumid != null ? _history.GetHistory(_aumid) : _history.GetHistory();
        }

        /// <summary>
        /// Removes an individual toast, with the specified tag label, from action center.
        /// </summary>
        /// <param name="tag">The tag label of the toast notification to be removed.</param>
        public void Remove(string tag)
        {
            if (_aumid != null)
            {
                _history.Remove(tag, string.Empty, _aumid);
            }
            else
            {
                _history.Remove(tag);
            }
        }

        /// <summary>
        /// Removes a toast notification from the action using the notification's tag and group labels.
        /// </summary>
        /// <param name="tag">The tag label of the toast notification to be removed.</param>
        /// <param name="group">The group label of the toast notification to be removed.</param>
        public void Remove(string tag, string group)
        {
            if (_aumid != null)
            {
                _history.Remove(tag, group, _aumid);
            }
            else
            {
                _history.Remove(tag, group);
            }
        }

        /// <summary>
        /// Removes a group of toast notifications, identified by the specified group label, from action center.
        /// </summary>
        /// <param name="group">The group label of the toast notifications to be removed.</param>
        public void RemoveGroup(string group)
        {
            if (_aumid != null)
            {
                _history.RemoveGroup(group, _aumid);
            }
            else
            {
                _history.RemoveGroup(group);
            }
        }
    }

    /// <summary>
    /// Apps must implement this activator to handle notification activation.
    /// </summary>
    public abstract class NotificationActivator : NotificationActivator.INotificationActivationCallback
    {
        public void Activate(string appUserModelId, string invokedArgs, NOTIFICATION_USER_INPUT_DATA[] data, uint dataCount)
        {
            OnActivated(invokedArgs, new NotificationUserInput(data), appUserModelId);
        }

        /// <summary>
        /// This method will be called when the user clicks on a foreground or background activation on a toast. Parent app must implement this method.
        /// </summary>
        /// <param name="arguments">The arguments from the original notification. This is either the launch argument if the user clicked the body of your toast, or the arguments from a button on your toast.</param>
        /// <param name="userInput">Text and selection values that the user entered in your toast.</param>
        /// <param name="appUserModelId">Your AUMID.</param>
        public abstract void OnActivated(string arguments, NotificationUserInput userInput, string appUserModelId);

        // These are the new APIs for Windows 10
        #region NewAPIs
        [StructLayout(LayoutKind.Sequential), Serializable]
        public struct NOTIFICATION_USER_INPUT_DATA
        {
            [MarshalAs(UnmanagedType.LPWStr)]
            public string Key;

            [MarshalAs(UnmanagedType.LPWStr)]
            public string Value;
        }

        [ComImport,
        Guid("53E31837-6600-4A81-9395-75CFFE746F94"), ComVisible(true),
        InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface INotificationActivationCallback
        {
            void Activate(
                [In, MarshalAs(UnmanagedType.LPWStr)]
            string appUserModelId,
                [In, MarshalAs(UnmanagedType.LPWStr)]
            string invokedArgs,
                [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
            NOTIFICATION_USER_INPUT_DATA[] data,
                [In, MarshalAs(UnmanagedType.U4)]
            uint dataCount);
        }
        #endregion
    }

    /// <summary>
    /// Text and selection values that the user entered on your notification. The Key is the ID of the input, and the Value is what the user entered.
    /// </summary>
    public class NotificationUserInput : IReadOnlyDictionary<string, string>
    {
        private NotificationActivator.NOTIFICATION_USER_INPUT_DATA[] _data;

        internal NotificationUserInput(NotificationActivator.NOTIFICATION_USER_INPUT_DATA[] data)
        {
            _data = data;
        }

        public string this[string key] => _data.First(i => i.Key == key).Value;

        public IEnumerable<string> Keys => _data.Select(i => i.Key);

        public IEnumerable<string> Values => _data.Select(i => i.Value);

        public int Count => _data.Length;

        public bool ContainsKey(string key)
        {
            return _data.Any(i => i.Key == key);
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            return _data.Select(i => new KeyValuePair<string, string>(i.Key, i.Value)).GetEnumerator();
        }

        public bool TryGetValue(string key, out string value)
        {
            foreach (var item in _data)
            {
                if (item.Key == key)
                {
                    value = item.Value;
                    return true;
                }
            }

            value = null;
            return false;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
