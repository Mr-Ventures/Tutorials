using Photon.Pun;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public class _BBEditor : MonoBehaviour
{

#if UNITY_EDITOR
    private static UnityEditor.EditorWindow _mouseOverWindow;
    private static DateTime timeOfLastDebugWipe = DateTime.Now;

    private void Awake()
    {
        UnityEditor.EditorApplication.playModeStateChanged += HandleOnPlayModeChanged;
    }

    void HandleOnPlayModeChanged( PlayModeStateChange change )
    {
        Debug.Log( "Playstate change" );
    }

    public static void OnPlayModeChanged( FFE.PlayModeState changedState )
    {
        OpenJournalIfNecessary();
        UpdateLastJournalEndTimeWithNow();

        if ( changedState == FFE.PlayModeState.AboutToPlay )
        {
            //ReimportScripts();
        }
        else if ( changedState == FFE.PlayModeState.AboutToStop )
        {
            SettingsSO.isShuttingDown = true;
        }
    }

    static int scriptRefreshesSinceBuild = 0;

    [UnityEditor.MenuItem( "BB_Tools/Game/Reimport Scripts (Alt+N) &n" )]
    internal static void ReimportScripts()
    {
        SettingsSO.isShuttingDown = false;

        TimeSpan timeSinceLastWipe = DateTime.Now - timeOfLastDebugWipe;
        if ( timeSinceLastWipe.TotalSeconds > 2 )
        {
            timeOfLastDebugWipe = DateTime.Now;
            ClearLog();
        }

        scriptRefreshesSinceBuild++;
        Debug.Log( "Refresh #" + scriptRefreshesSinceBuild );

        UnityEditor.AssetDatabase.ImportAsset( "Assets/Scripts", UnityEditor.ImportAssetOptions.ImportRecursive );
    }

    [UnityEditor.MenuItem( "BB_Tools/Recursive Rename" )]
    internal static void RenameSelectedTransform()
    {
        Transform selected = UnityEditor.Selection.activeGameObject.transform;
        RenameChildTransforms.RecursiveRename( selected );
    }

    internal static void ClearLogs()
    {
        var assembly = Assembly.GetAssembly( typeof( UnityEditor.Editor ) );
        var type = assembly.GetType( "UnityEditor.LogEntries" );
        var method = type.GetMethod( "Clear" );
        method.Invoke( new object(), null );
    }

    static void UpdateLastJournalEndTimeWithNow()
    {
        string path = Utils.GetLogPath();
        if ( !File.Exists( path ) )
            return;

        // Only update the entry's endtime every x minutes
        DateTime currentDate = System.DateTime.Now;
        long lastJournalEntryEndBinary = Convert.ToInt64( PlayerPrefs.GetString( "lastLogEntryEndTime" ) );
        DateTime lastJournalEntryEndDate = DateTime.FromBinary( lastJournalEntryEndBinary );
        TimeSpan difference = currentDate.Subtract( lastJournalEntryEndDate );
        int kMinutesBetweenUpdates = 35;
        if ( difference.TotalMinutes < kMinutesBetweenUpdates )
            return;

        string pastEntries = File.ReadAllText( path );
        int indexOfLastEndTime = pastEntries.IndexOf( " - " ) + 3;

        DateTime newEndTimeEstimate = DateTime.Now + new TimeSpan( 0, 30, 0 );
        string newEndTimeText = RoundTimeString( newEndTimeEstimate );

        string updatedEntries = pastEntries.Substring( 0, indexOfLastEndTime )
            + newEndTimeText
            + pastEntries.Substring( indexOfLastEndTime + 5 );

        PlayerPrefs.SetString( "lastLogEntryEndTime", newEndTimeEstimate.ToBinary().ToString() );
        File.WriteAllText( path, updatedEntries );

        string newTimeSpan = pastEntries.Substring( indexOfLastEndTime - 8, +16 );
        Debug.Log( "Updated recent log entry to: " + newTimeSpan );
    }

    static string RoundTimeString( DateTime time )
    {
        int kNumMinutesToRoundTo = 30;
        TimeSpan roundingInterval = TimeSpan.FromMinutes( kNumMinutesToRoundTo );

        var delta = time.Ticks % roundingInterval.Ticks;
        bool roundUp = delta > roundingInterval.Ticks / 2;
        var offset = roundUp ? roundingInterval.Ticks : 0;

        DateTime roundedTime = new DateTime( time.Ticks + offset - delta, time.Kind );
        string timeText = roundedTime.ToString( "HH:mm" );

        return timeText;
    }

    static string GetLastJournalEntryTag()
    {
        string path = Utils.GetLogPath();
        if ( !File.Exists( path ) )
            return "";

        FileInfo theSourceFile = new FileInfo( path );
        using ( StreamReader reader = theSourceFile.OpenText() )
        {
            string curLine;
            do
            {
                curLine = reader.ReadLine();

                int tagStart = curLine.IndexOf( "[" );
                if ( tagStart != -1 )
                {
                    int tagEnd = curLine.IndexOf( "]", tagStart + 1 );
                    string tag = curLine.Substring( tagStart + 1, tagEnd - tagStart - 1 );
                    return tag;
                }

            } while ( curLine != null );
        }

        return "";
    }

    [UnityEditor.MenuItem( "BB_Tools/Log/Open log with new entry" )]
    static void OpenLogWithNewEntry()
    {
        string path = Utils.GetLogPath();
        if ( !File.Exists( path ) )
            File.Create( path );

        DateTime startTime = DateTime.Now;
        DateTime estimatedEndTime = DateTime.Now + new TimeSpan( 0, 30, 0 );
        string dateText = DateTime.Now.ToShortDateString().Replace( "/", "." ).Replace( "2021", "21" ).Replace( "2022", "22" );
        string startTimeText = RoundTimeString( startTime );
        string endTimeText = RoundTimeString( estimatedEndTime );
        string newEntry = "";
        newEntry += "\n" + dateText;
        newEntry += "\n" + "[" + GetLastJournalEntryTag() + "]";
        newEntry += "\n" + startTimeText + " - " + endTimeText;
        newEntry += "\n" + "-------------";
        newEntry += "\n" + "- ";
        newEntry += "\n";
        newEntry += "\n";
        newEntry += "--------------------------------------------------------------";
        newEntry += "\n";

        string pastEntries = File.ReadAllText( path );
        File.WriteAllText( path, newEntry + pastEntries );
        PlayerPrefs.SetString( "lastLogEntryStartTime", System.DateTime.Now.ToBinary().ToString() );
        PlayerPrefs.SetString( "lastLogEntryEndTime", estimatedEndTime.ToBinary().ToString() );

        OpenLog();
    }

    static bool IsJournalOpeningNecessary()
    {
        DateTime currentDate = System.DateTime.Now;
        string lastLogEntryString = PlayerPrefs.GetString( "lastLogEntryStartTime" );
        if ( string.IsNullOrEmpty( lastLogEntryString ) )
            return true;

        long lastJournalEntryBinary = DateTime.Now.ToBinary();
        try { lastJournalEntryBinary = Convert.ToInt64( lastLogEntryString ); }
        catch { return true; }

        DateTime lastJournalEntryDate = DateTime.FromBinary( lastJournalEntryBinary );
        TimeSpan difference = currentDate.Subtract( lastJournalEntryDate );
        int kHoursBetweenDistinctEntries = 4;
        return difference.TotalHours > kHoursBetweenDistinctEntries;
    }

    static void OpenJournalIfNecessary()
    {
        if ( IsJournalOpeningNecessary() )
            OpenLogWithNewEntry();
    }

    [UnityEditor.MenuItem( "BB_Tools/Log/Open Log" )]
    static void OpenLog()
    {
        string path = Utils.GetLogPath();
        if ( !File.Exists( path ) )
            File.Create( path );
        Application.OpenURL( path );
    }

    [UnityEditor.MenuItem( "BB_Tools/Log/Make log require update" )]
    static void SetLogRequireUpdate()
    {
        DateTime tenHoursAgo = System.DateTime.Now - new TimeSpan( 10, 0, 0 );
        PlayerPrefs.SetString( "lastLogEntryStartTime", tenHoursAgo.ToBinary().ToString() );
    }

    [UnityEditor.MenuItem( "BB_Tools/Open/Scene/House" )]
    static public void OpenHouse()
    {
        string prefix = "Assets/Scenes/";
        string suffix = ".unity";
        string scenePath = prefix + SettingsSO.levels_ingame_house_static + suffix;
        EditorSceneManager.OpenScene( scenePath );
        //SelectSettingsGO();
    }

    static public void OpenWIP()
    {
        string prefix = "Assets/Scenes/";
        string suffix = ".unity";
        string scenePath = prefix + "WIP" + suffix;
        EditorSceneManager.OpenScene( scenePath );
        //SelectSettingsGO();
    }

    [UnityEditor.MenuItem( "BB_Tools/Open/Scene/Menu" )]
    static public void OpenMenu()
    {
        string prefix = "Assets/Scenes/";
        string suffix = ".unity";
        string scenePath = prefix + SettingsSO.levels_menu_mainLobby_static + suffix;
        EditorSceneManager.OpenScene( scenePath );
        SelectSettingsGO();
    }


    [UnityEditor.MenuItem( "BB_Tools/Building/PrepareToBuild" )]
    static public void PrepareToBuild()
    {
        SetDebugMode( false );

    }

    [UnityEditor.MenuItem( "BB_Tools/Building/PrepareToDebug" )]
    static public void PrepareToDebug()
    {
        SetDebugMode( true );
    }

    static private void SetDebugMode( bool useDebug )
    {
        // TCP vs UDP :
        // If you need to guarantee sequence and delivery then just use TCP, it's slower but order is exact.
        // If you need to guarantee sequence and delivery plus get the most recent packets as soon as they arrive, then use RUDP.
        PhotonNetwork.PhotonServerSettings.AppSettings.Protocol = useDebug ? ExitGames.Client.Photon.ConnectionProtocol.Tcp : ExitGames.Client.Photon.ConnectionProtocol.Udp;
        PhotonNetwork.NetworkingClient.LoadBalancingPeer.DisconnectTimeout = useDebug ? 10 * 1000 : 120 * 1000; // in milliseconds
    }

    [UnityEditor.MenuItem( "BB_Tools/Building/OpenBuildSteps" )]
    static public void OpenBuildSteps()
    {
        Application.OpenURL( "https://docs.google.com/document/d/1YwVAI2bxgKS2BP4Zfq3Yv7YxugH3m4xS945TvlZ3o8Q/edit?usp=sharing" );
    }

    [UnityEditor.MenuItem( "BB_Tools/Select/Pawns" )]
    static public void SelectPawns()
    {
        var go = GameObject.Find( "PawnManager" );
        if ( go == null )
        {
            Debug.LogWarning( "Aborting Select-Pawns, no Pawns object found." );
            return;
        }

        UnityEditor.Selection.activeGameObject = go;

        Transform child = null;
        if ( go.transform.childCount > 0 )
        {
            child = go.transform.GetChild( 0 );
            UnityEditor.EditorGUIUtility.PingObject( child.gameObject );
            UnityEditor.Selection.activeGameObject = child.gameObject;
        }
    }

    [UnityEditor.MenuItem( "BB_Tools/Select/PawnsIcons" )]
    static public void SelectPawnIcons()
    {
        var go = GameObject.Find( "attackerIcons" );
        if ( go == null )
        {
            Debug.LogWarning( "Aborting Select-Pawns, no Pawns object found." );
            return;
        }

        if ( go.transform.childCount > 0 )
            go = go.transform.GetChild( 0 ).gameObject;

        UnityEditor.Selection.activeGameObject = go;

        Transform child = null;
        if ( go.transform.childCount > 0 )
        {
            child = go.transform.GetChild( 0 );
            UnityEditor.EditorGUIUtility.PingObject( child.gameObject );
            UnityEditor.Selection.activeGameObject = child.gameObject;
        }
    }

    static public void SelectObject( string name )
    {
        var go = GameObject.Find( name );
        if ( go == null )
        {
            Debug.LogWarning( "Aborting selection, no '" + name + "' object found." );
            return;
        }

        UnityEditor.Selection.activeGameObject = go;

        Transform child = null;
        if ( go.transform.childCount > 0 )
        {
            child = go.transform.GetChild( 0 );
            UnityEditor.EditorGUIUtility.PingObject( child.gameObject );
            UnityEditor.Selection.activeGameObject = child.gameObject;
        }
    }

    [UnityEditor.MenuItem( "BB_Tools/Open/Photon Operation Codes" )]
    static public void OpenOpCodes()
    {
        Application.OpenURL( "https://doc-api.photonengine.com/en/pun/v1/class_operation_code.html" );
    }

    [UnityEditor.MenuItem( "BB_Tools/Game/Open Game" )]
    static public void OpenGame()
    {
        Application.OpenURL( GetBuildFileExePath() );
    }

    static public IEnumerator DelayedOpenGames( int numGamesToOpen = 1 )
    {
        float delayInSeconds = 0.3f; // 0.2 too low
        for ( int i = 0; i < numGamesToOpen; i++ )
        {
            yield return new WaitForSecondsRealtime( i * delayInSeconds );
            Application.OpenURL( GetBuildFileExePath() );
        }
    }

    [UnityEditor.MenuItem( "BB_Tools/Game/Run Games (Alt+R) &r" )]
    static public void RunGames()
    {
        OpenGame();
        EditorApplication.ExecuteMenuItem( "Edit/Play" );
    }

    static string GetBuildFileExePath()
    {
        return GetBuildFolderPath() + "Breach Buddies.exe";
    }

    static string GetGeneralBuildsFolderPath()
    {
        return "C:/Users/matth/Documents/Perforce/Ventures-DAD-Workspace/Builds/BB/";
    }

    static string GetBuildFolderPath()
    {
        return GetGeneralBuildsFolderPath() + "v0." + SettingsSO.Instance.buildVersion + "b" + "/";
    }

    [UnityEditor.MenuItem( "BB_Tools/Open/Open Build Folder (Alt+o) &o" )]
    static public void OpenBuildFolder()
    {
        Application.OpenURL( GetBuildFolderPath() );
    }

    [UnityEditor.MenuItem( "BB_Tools/Open/ Shared Game Settings" )]
    static public void GetSharedGameSettingsPath()
    {
        if ( File.Exists( SaveGameManager.GetSharedGameSettingsPath() ) )
        {
            Debug.Log( "Opening: " + SaveGameManager.GetSharedGameSettingsPath() );
            Application.OpenURL( SaveGameManager.GetSharedGameSettingsPath() );
        }
        else
        {
            Debug.Log( "Could not find: " + SaveGameManager.GetSharedGameSettingsPath() );
        }
    }

    [UnityEditor.MenuItem( "BB_Tools/Building/Delete Builds" )]
    static public void DeleteOldBuilds()
    {
        string path = GetGeneralBuildsFolderPath();

        Debug.unityLogger.logEnabled = false;

        if ( Directory.Exists( path ) )
            Directory.Delete( path, true );

        Debug.unityLogger.logEnabled = true;

        Directory.CreateDirectory( path );
    }

    [UnityEditor.MenuItem( "BB_Tools/Game/Begin Build (Alt+B) &b" )]
    static public void CloseThenBuildThenOpenGame()
    {
        CloseGame();
        scriptRefreshesSinceBuild = 0;

        DeleteOldBuilds();
        SettingsSO.Instance.buildVersion++;
        SettingsSO.isShuttingDown = false;

        // Override settings to fit playtest requirements
        bool previous_tileClientWindows = SettingsSO.Instance.tileClientWindows;
        bool previous_appendNumbersToDisplayNames = SettingsSO.Instance.appendNumbersToDisplayNames;
        bool previous_forgetPlayerIcon = SettingsSO.Instance.forgetPlayerIcon;
        bool previous_auto_isActive = SettingsSO.Instance.auto_isActive;
        bool previous_auto_auto_login = SettingsSO.Instance.auto_login;
        bool previous_auto_startGame = SettingsSO.Instance.auto_startGame;
        float previous_secondsInPrematch = SettingsSO.Instance.secondsInPrematch;
        bool previous_skipToGame = SettingsSO.Instance.skipToGame;

        if ( SettingsSO.Instance.isPlaytestBuild )
        {
            SettingsSO.Instance.tileClientWindows = false;
            SettingsSO.Instance.appendNumbersToDisplayNames = false;
            SettingsSO.Instance.auto_isActive = true;
            SettingsSO.Instance.auto_login = false;
            SettingsSO.Instance.auto_startGame = false;
            SettingsSO.Instance.secondsInPrematch = 10;
            SettingsSO.Instance.skipToGame = false;
        }

        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.locationPathName = GetBuildFileExePath();
        buildPlayerOptions.scenes = new [] { "Assets/Scenes/Lobby.unity", "Assets/Scenes/House.unity" };
        buildPlayerOptions.target = BuildTarget.StandaloneWindows;
        buildPlayerOptions.options = BuildOptions.Development | BuildOptions.AllowDebugging | BuildOptions.EnableDeepProfilingSupport;

        BuildReport report = BuildPipeline.BuildPlayer( buildPlayerOptions );
        BuildSummary summary = report.summary;

        if ( summary.result == BuildResult.Succeeded )
        {
            Debug.Log( "Build succeeded: " + summary.totalSize + " bytes" );
            RunGames();
        }

        if ( summary.result == BuildResult.Failed )
        {
            Debug.Log( "Build failed" );
        }

        // Restore settings prior to playtest overrides
        if ( SettingsSO.Instance.isPlaytestBuild )
        {
            SettingsSO.Instance.tileClientWindows = previous_tileClientWindows;
            SettingsSO.Instance.appendNumbersToDisplayNames = previous_appendNumbersToDisplayNames;
            SettingsSO.Instance.forgetPlayerIcon = previous_forgetPlayerIcon;
            SettingsSO.Instance.auto_isActive = previous_auto_isActive;
            SettingsSO.Instance.auto_login = previous_auto_auto_login;
            SettingsSO.Instance.auto_startGame = previous_auto_startGame;
            SettingsSO.Instance.secondsInPrematch = previous_secondsInPrematch;
            SettingsSO.Instance.skipToGame = previous_skipToGame;
        }
    }

    public static void ClearLog()
    {
        var assembly = Assembly.GetAssembly( typeof( UnityEditor.Editor ) );
        var type = assembly.GetType( "UnityEditor.LogEntries" );
        var method = type.GetMethod( "Clear" );
        method.Invoke( new object(), null );
    }

    [UnityEditor.MenuItem( "BB_Tools/Game/Close Game ( alt-X ) &x" )]
    static public void CloseGame()
    {
        foreach ( System.Diagnostics.Process p in System.Diagnostics.Process.GetProcessesByName( "Breach Buddies" ) )
        {
            p.CloseMainWindow();
        }
    }

    [UnityEditor.MenuItem( "BB_Tools/Open Credits" )]
    static public void OpenCredits()
    {
        string file = "C:/Users/matth/Documents/Perforce/Ventures-DAD-Workspace/BreachBuddies/Credits.txt";
        Application.OpenURL( file );
    }

    [UnityEditor.MenuItem( "BB_Tools/Print Global Position" )]
    public static void PrintGlobalPosition()
    {
        if ( Selection.activeGameObject != null )
        {
            Debug.Log( Selection.activeGameObject.name + " is at " + Selection.activeGameObject.transform.position );
        }
    }

    static public void OpenSettingsMenuScript()
    {
        string suffix = ".cs";
        string path = Application.dataPath;
        path = path.Replace( @"\", "/" );                      // '...\BreachBuddies\Unity\SWAT3D\Assets\'
        path += "/Scripts/Editor/";                             // '...\BreachBuddies\Unity\SWAT3D\Assets\Scripts\Editor'
        path += "_BBEditor" + suffix;
        Application.OpenURL( path );

    }

    static public void OpenScriptableSOEditor()
    {
        string path = "C:/Users/matth/Documents/Perforce/Ventures-DAD-Workspace/BreachBuddies/Unity/SWAT3D/Assets/Scripts/Editor/ScriptableSOEditor.cs";
        Application.OpenURL( path );
    }

    [UnityEditor.MenuItem( "BB_Tools/Open/Scene/Asteroids" )]
    static public void OpenAsteroids()
    {
        string prefix = "Assets/Photon/PhotonUnityNetworking/Demos/DemoAsteroids/Scenes/";
        string suffix = ".unity";
        string path = prefix + SettingsSO.Instance.levels_inGame_asteroidsDemo + suffix;
        EditorSceneManager.OpenScene( path );
    }

    [UnityEditor.MenuItem( "BB_Tools/Open/Scene/Opsive Demo" )]
    static public void OpenOpsiveDemo()
    {
        string prefix = "Assets/Opsive/UltimateCharacterController/Demo/";
        string suffix = ".unity";
        string path = prefix + "ThirdPersonControllerDemo" + suffix;
        EditorSceneManager.OpenScene( path );
    }


    [UnityEditor.MenuItem( "BB_Tools/Open Project Directory" )]
    static void OpenProjectDirectory()
    {
        string path = Application.dataPath;
        path = path.Replace( @"\", "/" );                      // '...\BreachBuddies\Unity\SWAT3D\Assets\'
        path = path.Substring( 0, path.LastIndexOf( "/" ) );   // '...\BreachBuddies\Unity\SWAT3D\'
        Application.OpenURL( path );
    }

    [UnityEditor.MenuItem( "BB_Tools/Open VS Solution" )]
    static void OpenVSSolution()
    {
        string path = Application.dataPath;
        path = path.Replace( @"\", "/" );                      // '...\BreachBuddies\Unity\SWAT3D\Assets\'
        path = path.Substring( 0, path.LastIndexOf( "/" ) );   // '...\BreachBuddies\Unity\SWAT3D\'
        string fullpath = path + "/SWAT3D" + ".sln";
        Debug.Log( "Opening: " + fullpath );
        Application.OpenURL( fullpath );
    }

    [UnityEditor.MenuItem( "BB_Tools/Select Settings ( alt-L ) &l" )]
    static void SelectSettingsGO()
    {
        UnityEditor.Selection.activeObject = UnityEditor.AssetDatabase.LoadMainAssetAtPath( "Assets/Resources/" + "Settings" + ".asset" );

        // Lock settings window
        {
            Type type = Assembly.GetAssembly( typeof( UnityEditor.Editor ) ).GetType( "UnityEditor.InspectorWindow" );
            UnityEngine.Object [] findObjectsOfTypeAll = Resources.FindObjectsOfTypeAll( type );
            if ( findObjectsOfTypeAll.Length == 0 )
                return;

            foreach ( var window in findObjectsOfTypeAll )
            {
                UnityEditor.EditorWindow settingsWindow = ( UnityEditor.EditorWindow ) window;

                // Lock the taller settings window
                if ( settingsWindow.position.height > 600 )
                {
                    PropertyInfo propertyInfo = type.GetProperty( "isLocked" );
                    propertyInfo.SetValue( settingsWindow, true, null );
                    settingsWindow.Repaint();
                }
            }
        }
    }
#endif
}