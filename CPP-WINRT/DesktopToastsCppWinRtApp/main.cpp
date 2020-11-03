
#include "pch.h"
#include <iostream>
#include "DesktopNotificationManagerCompat.h";
#include <functional>
#include <winrt/Windows.Data.Xml.Dom.h>
#include <winrt/Windows.UI.Notifications.h>
#include <conio.h>

using namespace winrt;
using namespace Windows::Data::Xml::Dom;
using namespace Windows::UI::Notifications;

bool _hasStarted;

void start();
void sendToast();
void showWindow();
void sendBasicToast(std::wstring message);

int main(int argc, char* argv[])
{
    DesktopNotificationManagerCompat::Register(L"Microsoft.SampleCppWinRtApp", L"Sample C++ WinRT App", L"C:\\MyIcon.png");

    DesktopNotificationManagerCompat::OnActivated([](DesktopNotificationActivatedEventArgsCompat e)
        {
            if (e.Argument()._Starts_with(L"action=like"))
            {
                sendBasicToast(L"Sent like!");

                if (!_hasStarted)
                {
                    exit(0);
                }
            }

            else if (e.Argument()._Starts_with(L"action=reply"))    
            {
                std::wstring msg = e.UserInput().Lookup(L"tbReply").c_str();

                sendBasicToast(L"Sent reply! Reply: " + msg);

                if (!_hasStarted)
                {
                    exit(0);
                }
            }

            else
            {
                if (!_hasStarted)
                {
                    if (e.Argument()._Starts_with(L"action=viewConversation"))
                    {
                        std::cout << "Launched from toast, opening the conversation!\n\n";
                    }

                    start();
                }
                else
                {
                    showWindow();

                    if (e.Argument()._Starts_with(L"action=viewConversation"))
                    {
                        std::cout << "\n\nOpening the conversation!\n\nEnter a number to continue: ";
                    }
                    else
                    {
                        std::wcout << L"\n\nToast activated!\n - Argument: " + e.Argument() + L"\n\nEnter a number to continue: ";
                    }
                }
            }
        });

    if (argc >= 2 && strcmp(argv[1], TOAST_ACTIVATED_LAUNCH_ARG) == 0)
    {
        // Was launched from a toast, OnActivated will be called and we'll decide whether to start the app or exit
        std::cin.ignore();   
    }

    else
    {
        start();
    }
}

void start()
{
    _hasStarted = true;

    std::cout << "Welcome!";

    while (true)
    {
        std::cout << "\n\nHere are your options...\n\n - 1. Send a toast\n - 2. Clear all toasts\n - 3. Uninstall and quit\n - 4. Exit\n\nEnter a number to continue: ";

        bool exit = false;

        switch (_getch())
        {
        case '1':
            sendToast();
            break;
        case '2':
            DesktopNotificationManagerCompat::History().Clear();
            break;
        case '3':
            DesktopNotificationManagerCompat::Uninstall();
            exit = true;
            break;
        case '4':
            exit = true;
            break;
        default:
            std::cout << "\nInvalid entry. Please try again: ";
            break;
        }

        if (exit)
        {
            break;
        }
    }
}

void sendToast()
{
    std::cout << "\n\nSending a toast... ";

    // Construct the toast template
    XmlDocument doc;
    doc.LoadXml(L"<toast>\
    <visual>\
        <binding template=\"ToastGeneric\">\
            <text></text>\
            <text></text>\
            <image placement=\"appLogoOverride\" hint-crop=\"circle\"/>\
            <image/>\
        </binding>\
    </visual>\
    <actions>\
        <input\
            id=\"tbReply\"\
            type=\"text\"\
            placeHolderContent=\"Type a reply\"/>\
        <action\
            content=\"Reply\"\
            activationType=\"background\"/>\
        <action\
            content=\"Like\"\
            activationType=\"background\"/>\
        <action\
            content=\"View\"\
            activationType=\"background\"/>\
    </actions>\
</toast>");

    // Populate with text and values
    doc.DocumentElement().SetAttribute(L"launch", L"action=viewConversation&conversationId=9813");
    doc.SelectSingleNode(L"//text[1]").InnerText(L"Andrew sent you a picture");
    doc.SelectSingleNode(L"//text[2]").InnerText(L"Check this out, Happy Canyon in Utah!");
    doc.SelectSingleNode(L"//image[1]").as<XmlElement>().SetAttribute(L"src", L"https://unsplash.it/64?image=1005");
    doc.SelectSingleNode(L"//image[2]").as<XmlElement>().SetAttribute(L"src", L"https://picsum.photos/364/202?image=883");
    doc.SelectSingleNode(L"//action[1]").as<XmlElement>().SetAttribute(L"arguments", L"action=reply&conversationId=9813");
    doc.SelectSingleNode(L"//action[2]").as<XmlElement>().SetAttribute(L"arguments", L"action=like&conversationId=9813");
    doc.SelectSingleNode(L"//action[3]").as<XmlElement>().SetAttribute(L"arguments", L"action=viewImage&imageUrl=https://picsum.photos/364/202?image=883");

    // Construct the notification
    ToastNotification notif{ doc };

    // And send it!
    DesktopNotificationManagerCompat::CreateToastNotifier().Show(notif);

    std::cout << "Sent!\n";
}

void showWindow()
{
    HWND hwnd = GetConsoleWindow();
    WINDOWPLACEMENT place = { sizeof(WINDOWPLACEMENT) };
    GetWindowPlacement(hwnd, &place);
    switch (place.showCmd)
    {
    case SW_SHOWMAXIMIZED:
        ShowWindow(hwnd, SW_SHOWMAXIMIZED);
        break;
    case SW_SHOWMINIMIZED:
        ShowWindow(hwnd, SW_RESTORE);
        break;
    default:
        ShowWindow(hwnd, SW_NORMAL);
        break;
    }
    SetWindowPos(0, HWND_TOP, 0, 0, 0, 0, SWP_SHOWWINDOW | SWP_NOSIZE | SWP_NOMOVE);
    SetForegroundWindow(hwnd);
}

void sendBasicToast(std::wstring message)
{
    // Construct the toast template
    XmlDocument doc;
    doc.LoadXml(L"<toast>\
    <visual>\
        <binding template=\"ToastGeneric\">\
            <text></text>\
        </binding>\
    </visual>\
</toast>");

    // Populate with text and values
    doc.SelectSingleNode(L"//text[1]").InnerText(message);

    // Construct the notification
    ToastNotification notif{ doc };

    // And send it!
    DesktopNotificationManagerCompat::CreateToastNotifier().Show(notif);
}

// Run program: Ctrl + F5 or Debug > Start Without Debugging menu
// Debug program: F5 or Debug > Start Debugging menu

// Tips for Getting Started: 
//   1. Use the Solution Explorer window to add/manage files
//   2. Use the Team Explorer window to connect to source control
//   3. Use the Output window to see build output and other messages
//   4. Use the Error List window to view errors
//   5. Go to Project > Add New Item to create new code files, or Project > Add Existing Item to add existing code files to the project
//   6. In the future, to open this project again, go to File > Open > Project and select the .sln file
