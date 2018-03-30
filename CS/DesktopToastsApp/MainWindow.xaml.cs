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
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace DesktopToastsApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // IMPORTANT: Look at App.xaml.cs for required registration and activation steps
        }

        private void ButtonPopToast_Click(object sender, RoutedEventArgs e)
        {
            PopToast(protocol: false);
        }

        private static bool _hasPerformedCleanup;
        private static async Task<string> DownloadImageToDisk(string httpImage)
        {
            // Toasts can live for up to 3 days, so we cache images for up to 3 days.
            // Note that this is a very simple cache that doesn't account for space usage, so
            // this could easily consume a lot of space within the span of 3 days.

            try
            {
                if (DesktopNotificationManagerCompat.CanUseHttpImages)
                {
                    return httpImage;
                }

                var directory = Directory.CreateDirectory(System.IO.Path.GetTempPath() + "WindowsNotifications.DesktopToasts.Images");

                if (!_hasPerformedCleanup)
                {
                    // First time we run, we'll perform cleanup of old images
                    _hasPerformedCleanup = true;

                    foreach (var d in directory.EnumerateDirectories())
                    {
                        if (d.CreationTimeUtc.Date < DateTime.UtcNow.Date.AddDays(-3))
                        {
                            d.Delete(true);
                        }
                    }
                }

                var dayDirectory = directory.CreateSubdirectory(DateTime.UtcNow.Day.ToString());
                string imagePath = dayDirectory.FullName + "\\" + (uint)httpImage.GetHashCode();

                if (File.Exists(imagePath))
                {
                    return imagePath;
                }

                HttpClient c = new HttpClient();
                using (var stream = await c.GetStreamAsync(httpImage))
                {
                    using (var fileStream = File.OpenWrite(imagePath))
                    {
                        stream.CopyTo(fileStream);
                    }
                }

                return imagePath;
            }
            catch { return ""; }
        }

        internal void ShowConversation()
        {
            ShowMessage("COM activated, view conversation");
        }

        internal void ShowImage(string imageUrl)
        {
            ShowMessage("Showing image: " + imageUrl);
        }

        internal void ShowMessage(string msg)
        {
            ContentBody.Children.Insert(0, new TextBlock()
            {
                Text = msg,
                FontWeight = FontWeights.Bold
            });
        }

        private void ButtonClearToasts_Click(object sender, RoutedEventArgs e)
        {
            DesktopNotificationManagerCompat.History.Clear();
        }

        private void ButtonPopProtocol_Click(object sender, RoutedEventArgs e)
        {
            PopToast(protocol: true);
        }

        private async void PopToast(bool protocol)
        {
            string title = protocol ? "ToastGeneric: Protocol+COM" : "ToastGeneric: Foreground+COM";

            bool useEvent = CheckBoxUseEventHandler.IsChecked.GetValueOrDefault();
            if (useEvent)
            {
                title += "+Event";
            }

            string content = "Check this out, The Enchantments!";
            string image = "https://picsum.photos/364/202?image=883";
            int conversationId = 5;

            // Construct the toast content
            ToastContent toastContent = new ToastContent()
            {
                // Arguments when the user taps body of toast
                Launch = protocol ? "desktopToasts:///viewConversation" : new QueryString()
                {
                    { "action", "viewConversation" },
                    { "conversationId", conversationId.ToString() }

                }.ToString(),

                ActivationType = protocol ? ToastActivationType.Protocol : ToastActivationType.Foreground,

                Visual = new ToastVisual()
                {
                    BindingGeneric = new ToastBindingGeneric()
                    {
                        Children =
                        {
                            new AdaptiveText()
                            {
                                Text = title
                            },

                            new AdaptiveText()
                            {
                                Text = content
                            },

                            new AdaptiveImage()
                            {
                                // Non-Desktop Bridge apps cannot use HTTP images, so
                                // we download and reference the image locally
                                Source = await DownloadImageToDisk(image)
                            }
                        },

                        AppLogoOverride = new ToastGenericAppLogo()
                        {
                            Source = await DownloadImageToDisk("https://unsplash.it/64?image=1005"),
                            HintCrop = ToastGenericAppLogoCrop.Circle
                        }
                    }
                },

                Actions = new ToastActionsCustom()
                {
                    Inputs =
                    {
                        new ToastTextBox("tbReply")
                        {
                            PlaceholderContent = "Type a response"
                        }
                    },

                    Buttons =
                    {
                        // Note that there's no reason to specify background activation, since our COM
                        // activator decides whether to process in background or launch foreground window
                        new ToastButton("Reply", new QueryString()
                        {
                            { "action", "reply" },
                            { "conversationId", conversationId.ToString() }

                        }.ToString()),

                        new ToastButton("Like", new QueryString()
                        {
                            { "action", "like" },
                            { "conversationId", conversationId.ToString() }

                        }.ToString()),

                        new ToastButton("View", new QueryString()
                        {
                            { "action", "viewImage" },
                            { "imageUrl", image }

                        }.ToString())
                    }
                }
            };

            // Make sure to use Windows.Data.Xml.Dom
            var doc = new XmlDocument();
            doc.LoadXml(toastContent.GetContent());

            // And create the toast notification
            var toast = new ToastNotification(doc);

            if (useEvent)
            {
                toast.Activated += Toast_Activated;
            }

            // And then show it
            DesktopNotificationManagerCompat.CreateToastNotifier().Show(toast);
        }

        private void Toast_Activated(ToastNotification sender, object args)
        {
            Application.Current.Dispatcher.Invoke(delegate
            {
                MyNotificationActivator.OpenWindowIfNeeded();
                ShowMessage("Activated event fired");
            });
        }
    }
}
