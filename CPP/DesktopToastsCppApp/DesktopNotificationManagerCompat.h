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

#pragma once
#include <rpc.h>
#include <Windows.h>
#include <windows.ui.notifications.h>
#include <wrl.h>
#define TOAST_ACTIVATED_LAUNCH_ARG "-ToastActivated"

using namespace ABI::Windows::UI::Notifications;

class DesktopNotificationHistoryCompat;

class DesktopNotificationManagerCompat
{
public:
    static HRESULT RegisterAumidAndComServer(const wchar_t *aumid, GUID clsid);
    static HRESULT RegisterActivator();
    static HRESULT CreateToastNotifier(IToastNotifier **notifier);
    static HRESULT CreateToastNotification(ABI::Windows::Data::Xml::Dom::IXmlDocument* content, IToastNotification** notification);
    static HRESULT get_History(DesktopNotificationHistoryCompat *history);
    static bool CanUseHttpImages();

private:
    static bool s_registeredAumidAndComServer;
    static const wchar_t *s_aumid;
    static bool s_registeredActivator;
    static bool s_hasCheckedIsRunningAsUwp;
    static bool s_isRunningAsUwp;

    static HRESULT RegisterComServer(GUID clsid, wchar_t exePath[]);
    static void EnsureRegistered();
    static bool IsRunningAsUwp();
};

class DesktopNotificationHistoryCompat
{
public:
    HRESULT Clear();
    HRESULT GetHistory(__FIVectorView_1_Windows__CUI__CNotifications__CToastNotification **toasts);
    HRESULT Remove(const wchar_t *tag);
    HRESULT Remove(const wchar_t *tag, const wchar_t *group);
    HRESULT RemoveGroup(const wchar_t *group);
    DesktopNotificationHistoryCompat(const wchar_t *aumid, Microsoft::WRL::ComPtr<IToastNotificationHistory> history);

    // TODO: This constructor should be removed? Can we somehow make this support ComPtr and stuff?
    DesktopNotificationHistoryCompat();

private:
    const wchar_t *m_aumid = nullptr;
    Microsoft::WRL::ComPtr<IToastNotificationHistory> m_history = nullptr;
};