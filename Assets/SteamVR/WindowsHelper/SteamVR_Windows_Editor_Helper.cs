using UnityEngine;
using System;
using System.Reflection;

namespace Valve.VR
{
    public class SteamVR_Windows_Editor_Helper
    {
        public enum BrowserApplication
        {
            Unknown,
            InternetExplorer,
            Firefox,
            Chrome,
            Opera,
            Safari,
            Edge,
        }

        public static BrowserApplication GetDefaultBrowser()
        {
#if UNITY_EDITOR
    #if UNITY_STANDALONE_WIN
            try
            {
                const string userChoice = @"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\http\UserChoice";

                // 通过反射访问注册表，避免编译期依赖 Microsoft.Win32.Registry 程序集
                Type registryType = Type.GetType("Microsoft.Win32.Registry, Microsoft.Win32.Registry, Version=4.1.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
                if (registryType == null)
                    registryType = Type.GetType("Microsoft.Win32.Registry");
                if (registryType == null)
                    return BrowserApplication.Unknown;

                PropertyInfo currentUserProp = registryType.GetProperty("CurrentUser", BindingFlags.Public | BindingFlags.Static);
                if (currentUserProp == null)
                    return BrowserApplication.Unknown;

                object currentUser = currentUserProp.GetValue(null, null);
                MethodInfo openSubKey = currentUser.GetType().GetMethod("OpenSubKey", new[] { typeof(string) });
                object userChoiceKey = openSubKey.Invoke(currentUser, new object[] { userChoice });

                if (userChoiceKey == null)
                    return BrowserApplication.Unknown;

                MethodInfo getValue = userChoiceKey.GetType().GetMethod("GetValue", new[] { typeof(string) });
                object progIdValue = getValue.Invoke(userChoiceKey, new object[] { "Progid" });

                IDisposable disposable = userChoiceKey as IDisposable;
                if (disposable != null)
                    disposable.Dispose();

                if (progIdValue == null)
                    return BrowserApplication.Unknown;

                string browserId = progIdValue.ToString().ToLower();

                if (browserId.Contains("ie.http"))
                    return BrowserApplication.InternetExplorer;
                else if (browserId.Contains("firefox"))
                    return BrowserApplication.Firefox;
                else if (browserId.Contains("chrome"))
                    return BrowserApplication.Chrome;
                else if (browserId.Contains("opera"))
                    return BrowserApplication.Opera;
                else if (browserId.Contains("safari"))
                    return BrowserApplication.Safari;
                else if (browserId.Contains("appcq0fevzme2pys62n3e0fbqa7peapykr8v")) //AppXq0fevzme2pys62n3e0fbqa7peapykr8v
                    return BrowserApplication.Edge;
                else
                    return BrowserApplication.Unknown;
            }
            catch
            {
                return BrowserApplication.Unknown;
            }
    #else
            return BrowserApplication.Firefox;
    #endif
#else
            return BrowserApplication.Firefox;
#endif
        }
    }
}
