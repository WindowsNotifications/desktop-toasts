#pragma comment(linker,"\"/manifestdependency:type='win32' name='Microsoft.Windows.Common-Controls' version='6.0.0.0' processorArchitecture='*' publicKeyToken='6595b64144ccf1df' language='*'\"")

#include <atlstr.h>
#include <SDKDDKVer.h>
#include <Windows.h>
#include <Psapi.h>
#include <string>
#include <strsafe.h>
#include <ShObjIdl.h>
#include <Shlobj.h>
#include <Pathcch.h>
#include <propvarutil.h>
#include <propkey.h>
#include <wrl.h>
#include <wrl\wrappers\corewrappers.h>
#include <windows.ui.notifications.h>
#include "NotificationActivationCallback.h"
#include "DesktopNotificationManagerCompat.h"

#define THROW_IF_FAILED(hr) if (FAILED(hr)) { throw hr; }

using namespace ABI::Windows::Data::Xml::Dom;
using namespace ABI::Windows::UI::Notifications;
using namespace Microsoft::WRL;
using namespace Microsoft::WRL::Wrappers;

class DesktopToastsApp
{
public:
    static const UINT WM_USER_OPENWINDOWIFNEEDED = WM_USER;

    static DesktopToastsApp* GetInstance()
    {
        return s_currentInstance;
    }

    static void DesktopToastsApp::SendBasicToast(
        _In_ PCWSTR message
    );

    DesktopToastsApp();
    ~DesktopToastsApp();
    void Initialize(_In_ HINSTANCE hInstance);
    void OpenWindowIfNeeded();
    bool HasWindow();
    void RunMessageLoop();
    void SetMessage(PCWSTR message);

    void SetHInstance(_In_ HINSTANCE hInstance)
    {
        m_hInstance = hInstance;
    }


private:
    static LRESULT CALLBACK WndProc(
        _In_ HWND hWnd,
        _In_ UINT message,
        _In_ WPARAM wParam,
        _In_ LPARAM lParam
        );

    void DisplayToast();
    void ClearToasts();

    Microsoft::WRL::ComPtr<ABI::Windows::Data::Xml::Dom::IXmlDocument> CreateToastXml();

    static void ShowToast(
        _In_ ABI::Windows::Data::Xml::Dom::IXmlDocument* xml
        );
    static void DesktopToastsApp::SetImageSrc(
        _In_ PCWSTR imagePath,
        _In_ ABI::Windows::Data::Xml::Dom::IXmlDocument* toastXml
        );
    static void DesktopToastsApp::SetTextValues(
        _In_reads_(textValuesCount) const PCWSTR* textValues,
        _In_ UINT32 textValuesCount,
        _Inout_ ABI::Windows::Data::Xml::Dom::IXmlDocument* toastXml
        );
    static void DesktopToastsApp::SetNodeValueString(
        _In_ HSTRING onputString,
        _Inout_ ABI::Windows::Data::Xml::Dom::IXmlNode* node,
        _In_ ABI::Windows::Data::Xml::Dom::IXmlDocument* xml
        );

    HINSTANCE m_hInstance;
    HWND m_hwnd = nullptr;
    HWND m_hEdit = nullptr;
    DWORD m_threadId;

    static const WORD HM_POPTOASTBUTTON = 1;
    static const WORD HM_CLEARTOASTSBUTTON = 2;

    static DesktopToastsApp* s_currentInstance;
};

DesktopToastsApp* DesktopToastsApp::s_currentInstance = nullptr;

// For the app to be activated from Action Center, it needs to provide a COM server to be called
// when the notification is activated.  The CLSID of the object needs to be registered with the
// OS via its shortcut so that it knows who to call later. The WiX installer adds that to the shortcut.
// Be sure to install the app via the WiX installer once before debugging!
class DECLSPEC_UUID("23A5B06E-20BB-4E7E-A0AC-6982ED6A6041") NotificationActivator WrlSealed
    : public RuntimeClass<RuntimeClassFlags<ClassicCom>, INotificationActivationCallback> WrlFinal
{
public: 
    virtual HRESULT STDMETHODCALLTYPE Activate(
        _In_ LPCWSTR appUserModelId,
        _In_ LPCWSTR invokedArgs,
        _In_reads_(dataCount) const NOTIFICATION_USER_INPUT_DATA* data,
        ULONG dataCount) override
    {
        try
        {
            std::string arguments = CW2A(invokedArgs);

            // Background: Quick reply to the conversation
            if (arguments.find("action=reply") == 0)
            {
                // Get the response user typed (we know this is first and only user input since our toasts only have one input)
                LPCWSTR response = data[0].Value;

                DesktopToastsApp::SendBasicToast(response);
            }

            // Background: Send a like
            else if (arguments.find("action=like") == 0)
            {
                DesktopToastsApp::SendBasicToast(L"Sending like...");
            }

            else
            {
                // The remaining scenarios are foreground activations,
                // so we first make sure we have a window open and in foreground
                DesktopToastsApp::GetInstance()->OpenWindowIfNeeded();

                // Open the image
                if (arguments.find("action=viewImage") == 0)
                {
                    DesktopToastsApp::GetInstance()->SetMessage(L"NotificationActivator - The user wants to view the image.");
                }

                // Open the conversation
                else if (arguments.find("action=viewConversation") == 0)
                {
                    DesktopToastsApp::GetInstance()->SetMessage(L"NotificationActivator - The user wants to view the conversation.");
                }

                // Open the app itself (user might have clicked on app title in Action Center which launches with empty args)
                else
                {
                    DesktopToastsApp::GetInstance()->SetMessage(L"NotificationActivator - The user clicked on a toast or the app title.");
                }
            }
        }
        catch (...)
        {
        }

        return S_OK;
    }

    ~NotificationActivator()
    {
        // If we don't have window open
        if (!DesktopToastsApp::GetInstance()->HasWindow())
        {
            // Exit
            exit(0);
        }
    }
};
CoCreatableClass(NotificationActivator);


// Main function
int WINAPI wWinMain(_In_ HINSTANCE hInstance, _In_opt_ HINSTANCE, _In_ LPWSTR cmdLineArgs, _In_ int)
{
    try
    {
        RoInitializeWrapper winRtInitializer(RO_INIT_MULTITHREADED);

        THROW_IF_FAILED(winRtInitializer);

        // Register AUMID and COM server (for Desktop Bridge apps, this no-ops)
        DesktopNotificationManagerCompat::RegisterAumidAndComServer(L"WindowsNotifications.DesktopToastsCpp", __uuidof(NotificationActivator));

        // Register activator type
        DesktopNotificationManagerCompat::RegisterActivator();

        // Create our desktop app
        DesktopToastsApp app;
        app.SetHInstance(hInstance);

        std::string cmdLineArgsStr = CW2A(cmdLineArgs);

        // If launched from toast
        if (cmdLineArgsStr.find(TOAST_ACTIVATED_LAUNCH_ARG) != std::string::npos)
        {
            // Let our NotificationActivator handle activation
        }

        else
        {
            // Otherwise launch like normal
            app.Initialize(hInstance);
        }

        app.RunMessageLoop();

        return 0;
    }
    catch (...)
    {
        return E_FAIL;
    }
}

void DesktopToastsApp::OpenWindowIfNeeded()
{
    // If no window exists
    if (m_hwnd == nullptr)
    {
        // If not on main UI thread
        if (m_threadId != GetCurrentThreadId())
        {
            // We have to initialize on UI thread so that the message loop is handled correctly
            HANDLE h = CreateEvent(NULL, 0, 0, NULL);
            PostThreadMessage(m_threadId, DesktopToastsApp::WM_USER_OPENWINDOWIFNEEDED, NULL, (LPARAM)h);
            WaitForSingleObject(h, INFINITE);
        }
        else
        {
            // Otherwise, create the window
            Initialize(m_hInstance);
        }
    }
    else
    {
        // Otherwise, ensure window is unminimized and in the foreground
        ::ShowWindow(m_hwnd, SW_RESTORE);
        ::SetForegroundWindow(m_hwnd);
    }
}

bool DesktopToastsApp::HasWindow()
{
    return m_hwnd != nullptr;
}

DesktopToastsApp::DesktopToastsApp()
{
    s_currentInstance = this;
    m_threadId = GetCurrentThreadId();
}

DesktopToastsApp::~DesktopToastsApp()
{
    s_currentInstance = nullptr;
}

// Prepare the main window
_Use_decl_annotations_
void DesktopToastsApp::Initialize(HINSTANCE hInstance)
{
    WNDCLASSEX wcex = { sizeof(wcex) };
    // Register window class
    wcex.style = CS_HREDRAW | CS_VREDRAW;
    wcex.lpfnWndProc = DesktopToastsApp::WndProc;
    wcex.cbWndExtra = sizeof(LONG_PTR);
    wcex.hInstance = hInstance;
    wcex.hIcon = LoadIcon(nullptr, IDI_APPLICATION);
    wcex.hCursor = LoadCursor(nullptr, IDC_ARROW);
    wcex.lpszClassName = L"DesktopToastsApp";
    wcex.hIconSm = LoadIcon(nullptr, IDI_APPLICATION);
    ::RegisterClassEx(&wcex);

    // Create window
    m_hwnd = CreateWindow(
        L"DesktopToastsApp",
        L"Desktop Toasts Demo App",
        WS_OVERLAPPEDWINDOW,
        CW_USEDEFAULT, CW_USEDEFAULT,
        350, 200,
        nullptr, nullptr,
        hInstance, this);

    if (!m_hwnd)
    {
        throw E_FAIL;
    }

    ::CreateWindow(
        L"BUTTON",
        L"View Text Toast",
        BS_PUSHBUTTON | WS_CHILD | WS_VISIBLE,
        12, 12, 150, 25,
        m_hwnd, reinterpret_cast<HMENU>(HM_POPTOASTBUTTON),
        hInstance, nullptr);
    ::CreateWindow(
        L"BUTTON",
        L"Clear toasts",
        BS_PUSHBUTTON | WS_CHILD | WS_VISIBLE,
        174, 12, 150, 25,
        m_hwnd, reinterpret_cast<HMENU>(HM_CLEARTOASTSBUTTON),
        hInstance, nullptr);
    m_hEdit = ::CreateWindow(
        L"EDIT",
        L"Whatever action you take on the displayed toast will be shown here.",
        ES_LEFT | ES_MULTILINE | ES_READONLY | WS_CHILD | WS_VISIBLE | WS_BORDER,
        12, 49, 300, 50,
        m_hwnd, nullptr,
        hInstance, nullptr);

    ::ShowWindow(m_hwnd, SW_SHOWNORMAL);
    ::UpdateWindow(m_hwnd);
    ::SetForegroundWindow(m_hwnd);
}

// Standard message loop
void DesktopToastsApp::RunMessageLoop()
{
    MSG msg;
    while (GetMessage(&msg, NULL, 0, 0) != 0)
    {
        if (msg.message == DesktopToastsApp::WM_USER_OPENWINDOWIFNEEDED)
        {
            OpenWindowIfNeeded();
            SetEvent((HANDLE)msg.lParam);
        }
        else
        {
            TranslateMessage(&msg);
            DispatchMessage(&msg);
        }
    }
}

void DesktopToastsApp::SetMessage(PCWSTR message)
{
    ::SendMessage(m_hEdit, WM_SETTEXT, reinterpret_cast<WPARAM>(nullptr), reinterpret_cast<LPARAM>(message));
}

// Display the toast using classic COM. Note that is also possible to create and
// display the toast using the new C++ /ZW options (using handles, COM wrappers, etc.)
void DesktopToastsApp::DisplayToast()
{
    try
    {
        // Create the toast content
        ComPtr<IXmlDocument> toastXml = CreateToastXml();

        // And show it
        ShowToast(toastXml.Get());
    }
    catch (...)
    {
        DesktopToastsApp::GetInstance()->SetMessage(L"Exception occurred");
    }
}

// Create the toast XML from a template
_Use_decl_annotations_
ComPtr<IXmlDocument> DesktopToastsApp::CreateToastXml()
{
    ComPtr<IXmlDocument> doc = DesktopNotificationManagerCompat::CreateXmlDocumentFromString(LR"(<toast launch="action=viewConversation&amp;conversationId=5">
    <visual>
        <binding template="ToastGeneric">
            <text></text>
            <text></text>
        </binding>
    </visual>
    <actions>
        <input id="tbReply" type="text" placeHolderContent="Type a response"/>
        <action content="Reply" arguments="action=reply&amp;conversationId=5"/>
        <action content="Like" arguments="action=like&amp;conversationId=5"/>
        <action content="View" arguments="action=viewImage&amp;imageUrl=https://picsum.photos/364/202?image=883"/>
    </actions>
</toast>)");

    PCWSTR textValues[] = {
        L"Andrew sent you a picture",
        L"Check this out, The Enchantments!"
    };
    SetTextValues(textValues, ARRAYSIZE(textValues), doc.Get());

    return doc;
}

// Set the value of the "src" attribute of the "image" node
_Use_decl_annotations_
void DesktopToastsApp::SetImageSrc(PCWSTR imagePath, IXmlDocument* toastXml)
{
    wchar_t imageSrcUri[MAX_PATH];
    DWORD size = ARRAYSIZE(imageSrcUri);

    THROW_IF_FAILED(::UrlCreateFromPath(imagePath, imageSrcUri, &size, 0));

    ComPtr<IXmlNodeList> nodeList;
    THROW_IF_FAILED(toastXml->GetElementsByTagName(HStringReference(L"image").Get(), &nodeList));

    ComPtr<IXmlNode> imageNode;
    THROW_IF_FAILED(nodeList->Item(0, &imageNode));

    ComPtr<IXmlNamedNodeMap> attributes;

    THROW_IF_FAILED(imageNode->get_Attributes(&attributes));

    ComPtr<IXmlNode> srcAttribute;
    THROW_IF_FAILED(attributes->GetNamedItem(HStringReference(L"src").Get(), &srcAttribute));

    SetNodeValueString(HStringReference(imageSrcUri).Get(), srcAttribute.Get(), toastXml);
}

// Set the values of each of the text nodes
_Use_decl_annotations_
void DesktopToastsApp::SetTextValues(const PCWSTR* textValues, UINT32 textValuesCount, IXmlDocument* toastXml)
{
    ComPtr<IXmlNodeList> nodeList;
    THROW_IF_FAILED(toastXml->GetElementsByTagName(HStringReference(L"text").Get(), &nodeList));

    UINT32 nodeListLength;
    THROW_IF_FAILED(nodeList->get_Length(&nodeListLength));

    // If a template was chosen with fewer text elements, also change the amount of strings
    // passed to this method.
    THROW_IF_FAILED(textValuesCount <= nodeListLength ? S_OK : E_INVALIDARG);

    for (UINT32 i = 0; i < textValuesCount; i++)
    {
        ComPtr<IXmlNode> textNode;
        THROW_IF_FAILED(nodeList->Item(i, &textNode));

        SetNodeValueString(HStringReference(textValues[i]).Get(), textNode.Get(), toastXml);
    }
}

_Use_decl_annotations_
void DesktopToastsApp::SetNodeValueString(HSTRING inputString, IXmlNode* node, IXmlDocument* xml)
{
    ComPtr<IXmlText> inputText;
    THROW_IF_FAILED(xml->CreateTextNode(inputString, &inputText));

    ComPtr<IXmlNode> inputTextNode;
    THROW_IF_FAILED(inputText.As(&inputTextNode));

    ComPtr<IXmlNode> appendedChild;
    THROW_IF_FAILED(node->AppendChild(inputTextNode.Get(), &appendedChild));
}

// Create and display the toast
_Use_decl_annotations_
void DesktopToastsApp::ShowToast(IXmlDocument* xml)
{
    // Create the notifier
    // Classic Win32 apps MUST use the compat method to create the notifier
    ComPtr<IToastNotifier> notifier = DesktopNotificationManagerCompat::CreateToastNotifier();

    // And create the notification itself
    ComPtr<IToastNotification> toast = DesktopNotificationManagerCompat::CreateToastNotification(xml);

    // And show it!
    THROW_IF_FAILED(notifier->Show(toast.Get()));
}

// Clear all toasts
_Use_decl_annotations_
void DesktopToastsApp::ClearToasts()
{
    try
    {
        // Clear all toasts
        // Classic Win32 apps MUST use the compat method to obtain history
        DesktopNotificationManagerCompat::get_History()->Clear();
    }
    catch (...)
    {
        DesktopToastsApp::GetInstance()->SetMessage(L"Exception occurred");
    }
}

// Standard window procedure
_Use_decl_annotations_
LRESULT CALLBACK DesktopToastsApp::WndProc(HWND hwnd, UINT32 message, WPARAM wParam, LPARAM lParam)
{
    if (message == WM_CREATE)
    {
        auto pcs = reinterpret_cast<LPCREATESTRUCT>(lParam);
        auto app = reinterpret_cast<DesktopToastsApp *>(pcs->lpCreateParams);

        SetWindowLongPtr(hwnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(app));

        return 1;
    }

    auto app = reinterpret_cast<DesktopToastsApp *>(GetWindowLongPtr(hwnd, GWLP_USERDATA));
    if (app)
    {
        switch (message)
        {
        case WM_COMMAND:
        {
            int wmId = LOWORD(wParam);
            switch (wmId)
            {
            case DesktopToastsApp::HM_POPTOASTBUTTON:
                app->DisplayToast();
                break;
            case DesktopToastsApp::HM_CLEARTOASTSBUTTON:
                app->ClearToasts();
                break;
            default:
                return DefWindowProc(hwnd, message, wParam, lParam);
            }
        }
            break;
        case WM_PAINT:
        {
            PAINTSTRUCT ps;
            BeginPaint(hwnd, &ps);
            EndPaint(hwnd, &ps);
        }
            return 0;

        case WM_DESTROY:
        {
            PostQuitMessage(0);
        }
            return 1;
        }
    }
    return DefWindowProc(hwnd, message, wParam, lParam);
}

void DesktopToastsApp::SendBasicToast(PCWSTR message)
{
    ComPtr<IXmlDocument> doc = DesktopNotificationManagerCompat::CreateXmlDocumentFromString(LR"(<toast>
    <visual>
        <binding template="ToastGeneric">
            <text></text>
        </binding>
    </visual>
</toast>)");

    PCWSTR textValues[] = {
        message
    };
    SetTextValues(textValues, ARRAYSIZE(textValues), doc.Get());

    ShowToast(doc.Get());
}