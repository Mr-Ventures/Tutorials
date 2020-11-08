using System.Collections;
using System.Collections.Generic;
using UnityEngine;
// CSVDownloader.cs
using UnityEngine.Networking;

public static class CSVDownloader
{
    private const string k_googleSheetDocID = "13zXZxMWmS5ShIIxXd8OIOIf6JCBYmwziav9OsLdPH1U";

    // docs.google.com/spreadsheets/d/13zXZxMWmS5ShIIxXd8OIOIf6JCBYmwziav9OsLdPH1U/edit#gid=0
    private const string url = "https://docs.google.com/spreadsheets/d/" + k_googleSheetDocID + "/export?format=csv";

    internal static IEnumerator DownloadData(System.Action<string> onCompleted)
    {
        yield return new WaitForEndOfFrame();

        string downloadData = null;
        using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
        {
            Debug.Log("Starting Download...");
            yield return webRequest.SendWebRequest();
            int equalsIndex = ExtractEqualsIndex(webRequest.downloadHandler);
            if (webRequest.isNetworkError || (-1 == equalsIndex))
            {
                Debug.Log("...Download Error: " + webRequest.error);
                downloadData = PlayerPrefs.GetString("LastDataDownloaded", null);
                string versionText = PlayerPrefs.GetString("LastDataDownloadedVersion", null);
                Debug.Log("Using stale data version: " + versionText);
            }
            else
            {
                string versionText = webRequest.downloadHandler.text.Substring(0, equalsIndex);
                downloadData = webRequest.downloadHandler.text.Substring(equalsIndex + 1);
                PlayerPrefs.SetString("LastDataDownloadedVersion", versionText);
                PlayerPrefs.SetString("LastDataDownloaded", downloadData);
                Debug.Log("...Downloaded version: " + versionText);

            }
        }

        onCompleted(downloadData);
    }

    private static int ExtractEqualsIndex(DownloadHandler d)
    {
        if (d.text == null || d.text.Length < 10)
        {
            return -1;
        }
        // First term will be preceeded by version number, e.g. "100=English"
        string versionSection = d.text.Substring(0, 5);
        int equalsIndex = versionSection.IndexOf('=');
        if (equalsIndex == -1)
            Debug.Log("Could not find a '=' at the start of the CVS");
        return equalsIndex;
    }
}