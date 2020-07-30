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
using Microsoft.QueryStringDotNET;
using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace DesktopToastsApp
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Register AUMID and display details
            DesktopNotificationManagerCompat.RegisterApplication(
                aumid: "WindowsNotifications.DesktopToasts",
                displayName: "Desktop toasts app",
                iconPath: "C:\\icon.png");

            // Listen to notification activation
            //DesktopNotificationManagerCompat.OnActivated += Notification_OnActivated;

            //DesktopNotificationManagerCompat.RegisterActivator<MyNotificationActivator>();

            // If launched from a toast
            // This launch arg was specified in our WiX installer where we register the LocalServer32 exe path.
            if (e.Args.Contains(DesktopNotificationManagerCompat.TOAST_ACTIVATED_LAUNCH_ARG))
            {
                // Our NotificationActivator code will run after this completes,
                // and will show a window if necessary.
            }

            else
            {
                // Show the window
                // In App.xaml, be sure to remove the StartupUri so that a window doesn't
                // get created by default, since we're creating windows ourselves (and sometimes we
                // don't want to create a window if handling a background activation).
                new MainWindow().Show();
            }

            base.OnStartup(e);
        }

        private void Notification_OnActivated(NotificationActivatiedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(delegate
            {
                if (e.Arguments.Length == 0)
                {
                    OpenWindowIfNeeded();
                    return;
                }

                // Parse the query string (using NuGet package QueryString.NET)
                QueryString args = QueryString.Parse(e.Arguments);

                // See what action is being requested 
                switch (args["action"])
                {
                    // Open the image
                    case "viewImage":

                        // The URL retrieved from the toast args
                        string imageUrl = args["imageUrl"];

                        // Make sure we have a window open and in foreground
                        OpenWindowIfNeeded();

                        // And then show the image
                        (App.Current.Windows[0] as MainWindow).ShowImage(imageUrl);

                        break;

                    // Open the conversation
                    case "viewConversation":

                        // The conversation ID retrieved from the toast args
                        int conversationId = int.Parse(args["conversationId"]);

                        // Make sure we have a window open and in foreground
                        OpenWindowIfNeeded();

                        // And then show the conversation
                        (App.Current.Windows[0] as MainWindow).ShowConversation();

                        break;

                    // Background: Quick reply to the conversation
                    case "reply":

                        // Get the response the user typed
                        string msg = e.UserInput["tbReply"];

                        // And send this message
                        ShowToast("Sending message: " + msg);

                        // If there's no windows open, exit the app
                        if (App.Current.Windows.Count == 0)
                        {
                            Application.Current.Shutdown();
                        }

                        break;

                    // Background: Send a like
                    case "like":

                        ShowToast("Sending like");

                        // If there's no windows open, exit the app
                        if (App.Current.Windows.Count == 0)
                        {
                            Application.Current.Shutdown();
                        }

                        break;

                    default:

                        OpenWindowIfNeeded();

                        break;
                }
            });
        }
        private void OpenWindowIfNeeded()
        {
            // Make sure we have a window open (in case user clicked toast while app closed)
            if (App.Current.Windows.Count == 0)
            {
                new MainWindow().Show();
            }

            // Activate the window, bringing it to focus
            App.Current.Windows[0].Activate();

            // And make sure to maximize the window too, in case it was currently minimized
            App.Current.Windows[0].WindowState = WindowState.Normal;
        }

        private void ShowToast(string msg)
        {
            // Construct the visuals of the toast
            ToastContent toastContent = new ToastContent()
            {
                // Arguments when the user taps body of toast
                Launch = "action=ok",

                Visual = new ToastVisual()
                {
                    BindingGeneric = new ToastBindingGeneric()
                    {
                        Children =
                        {
                            new AdaptiveText()
                            {
                                Text = msg
                            }
                        }
                    }
                }
            };

            var doc = new XmlDocument();
            doc.LoadXml(toastContent.GetContent());

            // And create the toast notification
            var toast = new ToastNotification(doc);

            // And then show it
            ToastNotificationManagerCompat.CreateToastNotifier().Show(toast);
        }
    }
}
