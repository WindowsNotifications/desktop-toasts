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

using DesktopNotifications;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace DesktopToastsApp
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            SingleApplication.Run(delegate
            {
                // Register AUMID, COM server, and activator
                DesktopNotificationManagerCompat.RegisterAumidAndComServer<MyNotificationActivator>("WindowsNotifications.DesktopToasts");
                DesktopNotificationManagerCompat.RegisterActivator<MyNotificationActivator>();
            }, SingleApplication_Activated, string.Join(" ", e.Args));

            base.OnStartup(e);
        }

        private void SingleApplication_Activated(object sender, string args)
        {
            // If launched from a toast
            // This launch arg was specified in our WiX installer where we register the LocalServer32 exe path.
            if (args.Contains(DesktopNotificationManagerCompat.TOAST_ACTIVATED_LAUNCH_ARG))
            {
                // Our NotificationActivator code will run after this completes,
                // and will show a window if necessary.
            }

            else if (args.StartsWith("desktopToasts:", StringComparison.CurrentCultureIgnoreCase))
            {
                MyNotificationActivator.CreateWindowIfNeeded();

                (App.Current.Windows[0] as MainWindow).ShowMessage("Protocol activated: " + args);
            }

            else
            {
                // Show the window
                // In App.xaml, be sure to remove the StartupUri so that a window doesn't
                // get created by default, since we're creating windows ourselves (and sometimes we
                // don't want to create a window if handling a background activation).
                MyNotificationActivator.CreateWindowIfNeeded();
                (App.Current.Windows[0] as MainWindow).ShowMessage("EXE launched");
            }
        }
    }
}
