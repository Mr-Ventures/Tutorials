// NS_AdManager.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class NS_AdManager : MonoBehaviour {

    [SerializeField] private GameObject debugAd;
    bool adsEnabled = true;
    System.Action<bool> adCallback = null;
    public enum AdType { interstitial, rewardedVideo };

    // Found under App Management (https://console.fyber.com/inventory/publisher)
    private const string androidAppID = "112775";
    private const string iosAppID = "112776";

    private const string androidInterstitialID = "217858";
    private const string androidRewardedID = "217841";

    private const string iosInterstitialID = "TODO";
    private const string iosRewardedID = "TODO";

    public static bool IsAndroid() {
#if UNITY_IOS
        return false;
#endif
        return true;
    }

    public void Load() {
        UnityEngine.Assertions.Assert.IsNotNull( debugAd );
        DisableDebugAd();
        if ( IsMobile() ) {
            Fyber.FairBid.Start( IsAndroid() ? androidAppID : iosAppID );
            Debug.Log( "Fyber ads setup" );
        }
        else {
            Debug.Log( "Fyber ads NOT setup (because we are not on mobile)" );
        }

        // If player has purchased something at least once then we set adsEnabled to false
        if ( AreAdsDisabled() ) {
            adsEnabled = false;
            Debug.Log( "Ads have been disabled for a returning payer." );
        }
        PrepAds();
    }

    public void SetAdsDisabled() {
        PlayerPrefs.SetInt( "removeAds", 1 );
        adsEnabled = false;
        Debug.Log( "Ads have just been disabled." );
    }
    public bool AreAdsDisabled() {
        // If player has purchased something at least once then we set adsEnabled to false
        return ( PlayerPrefs.GetInt( "removeAds", 0 ) == 1 );
    }

    public static bool IsMobile() {
#if UNITY_EDITOR
        return false;
#elif UNITY_STANDALONE_OSX
            return false;
#elif UNITY_STANDALONE_WIN
            return false;
#else
            return true;
#endif
    }

    private static bool IsInterstitialAvailable() {
        return Fyber.Interstitial.IsAvailable( IsAndroid() ? androidInterstitialID : iosInterstitialID );
    }

    private static void FetchInterstitial() {
        Fyber.Interstitial.Request( IsAndroid() ? androidInterstitialID : iosInterstitialID );
    }

    private static bool IsRewardedAvailable() {
        return Fyber.Rewarded.IsAvailable( IsAndroid() ? androidRewardedID : iosRewardedID );
    }

    private static void FetchRewarded() {
        Fyber.Rewarded.Request( IsAndroid() ? androidRewardedID : iosRewardedID );
    }

    public void AdFetchIfNeeded() {
        if ( !IsMobile() )
            return;

        Debug.Log( "Pre-fetch, Interstitial ready: " + IsInterstitialAvailable() );
        Debug.Log( "Pre-fetch, Incentivized ready: " + IsRewardedAvailable() );
        if ( !IsInterstitialAvailable() )
            FetchInterstitial();
        if ( !IsRewardedAvailable() )
            FetchRewarded();
        Debug.Log( "Post-fetch, Interstitial ready: " + IsInterstitialAvailable() );
        Debug.Log( "Post-fetch, Incentivized ready: " + IsRewardedAvailable() );
    }

    private void PrepAds() {
        Debug.Log( "Preping Ads" );
        if ( !IsMobile() ) {
            return;
        }

        AdFetchIfNeeded();
        Debug.Log( "Setting up ad listeners." );

        var rewardedListener = new MyRewardedListener(CallbackAfterAdd);
        Fyber.Rewarded.SetRewardedListener( rewardedListener );

        var interstitialListener = new MyInterstitialListener(CallbackAfterAdd);
        Fyber.Interstitial.SetInterstitialListener( interstitialListener );
    }

    private void CallbackAfterAdd( bool adCompleted ) {
        if ( adCallback != null )
            adCallback( adCompleted );
        adCallback = null;
    }

    // The callback is called after the video ad, it needs to be reset!
    public void SetCallback( System.Action<bool> callback ) {
        adCallback = callback;
    }

    // Returns if ad was played
    public bool PlayAdIfEnabled( AdType t ) {
        Debug.Log( "Ads controller recieved request to play ad of type: " + t.ToString() );

        AdFetchIfNeeded();

        // If it's not a rewarded video then we do the callback even if ad failed
        if ( t != AdType.rewardedVideo ) {
            CallbackAfterAdd( true );
        }

        if ( ( t == AdType.rewardedVideo ) || adsEnabled ) {
            if ( t == AdType.rewardedVideo && adCallback == null ) {
                Debug.LogError( "Rewarded video request has no callback." );
            }
            if ( !IsMobile() ) {
                PlayAdDebug();
                if ( t == AdType.rewardedVideo )
                    CallbackAfterAdd( true );
            }
            else {
                if ( t == AdType.rewardedVideo && IsRewardedAvailable() ) {
                    Debug.Log( "PlayAdIfEnabled - rewarded" );
                    return PlayRewardedVideoMobile();
                }
                else if ( t == AdType.interstitial && IsInterstitialAvailable() ) {
                    PlayAdInterstitialMobile();
                    return true;
                }
                else {
                    Debug.LogError( "Ad type not handled: " + t );
                    return false;
                }
            }

            return true;
        }
        // Rewarded video failed
        else {
            return false;
        }
    }

    private bool PlayRewardedVideoMobile( int numtries = 0 ) {
        if ( IsRewardedAvailable() ) {
            Debug.Log( "Incentivized available and playing" );
            try {
                Fyber.Rewarded.Show( IsAndroid() ? androidRewardedID : iosRewardedID );
            }
            catch ( Exception e ) {
                Debug.LogError( "'Fyber.Rewarded.Show' yielded error..." + e.Message + e.StackTrace );
            }

            Debug.Log( "After show" );
            return true;
        }
        else {
            Debug.Log( "Incentivized not available" );
            if ( numtries < 3 ) {
                Debug.Log( "Retrying..." );
                AdFetchIfNeeded();
                return PlayRewardedVideoMobile( numtries + 1 );
            }
            else {
                Debug.Log( "Giving up..." );
                AdFetchIfNeeded();
                return false;
            }
        }
    }
    private void PlayAdInterstitialMobile( int numtries = 0 ) {
        if ( IsInterstitialAvailable() ) {
            Debug.Log( "Interstitial available and playing" );
            Fyber.Rewarded.Show( IsAndroid() ? androidInterstitialID : iosInterstitialID );
            AdFetchIfNeeded();
            return;
        }
        else {
            Debug.Log( "Interstitial not available" );
            if ( numtries < 3 ) {
                Debug.Log( "Retrying..." );
                AdFetchIfNeeded();
                PlayAdInterstitialMobile( numtries + 1 );
                return;
            }
            else {
                Debug.Log( "Giving up..." );
                AdFetchIfNeeded();
                return;
            }
        }
    }

    private void PlayAdDebug() {
        debugAd.gameObject.SetActive( true );
        Invoke( "DisableDebugAd", 1.2f );
    }
    private void DisableDebugAd() {
        debugAd.gameObject.SetActive( false );
        CallbackAfterAdd( true );
    }
}
