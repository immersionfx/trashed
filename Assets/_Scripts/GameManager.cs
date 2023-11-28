
using System;
using UnityEngine;
using UnityEngine.UI;
using Niantic.Lightship.AR.LocationAR;
using Niantic.Lightship.AR.PersistentAnchors;
using FrostweepGames.Plugins.GoogleCloud.SpeechRecognition.Examples;
using TMPro;


public class GameManager : MonoBehaviour
{
    public static GameManager instance = null;    

    [Header("Managers")]
    [Tooltip("The location manager")]
    [SerializeField] private ARLocationManager _arLocationManager;

    [SerializeField] private AudioManager audioManager;
    [SerializeField] private SemanticQuerying semanticQuery;
    [SerializeField] private FallingObjectSpawner spawner;

    [Header("UI Elements")]
    [SerializeField] private GameObject _VPS_Status; // "Tracking/Not Tracking" 
    [SerializeField] private GameObject _Instructions; // "POINT YOUR DEVICE TO THE SKY AND I WILL INTERACT WITH YOU" 
    public Button _RecordButton;



    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            //DontDestroyOnLoad(gameObject);
        }
        // else if (instance != this)
        // {
        //     Destroy(this.gameObject);
        //     return;
        // }
    }

    //We start with VPS Tracking
    void Start()
    {
        _RecordButton.onClick.AddListener(recordButtonOnClickHandler);

        //Events
        SemanticQuerying.enableRecording += showRecordButtonEvent;

        _VPS_Status.SetActive(true);  

        semanticQuery.gameObject.SetActive(false);        
        _RecordButton.gameObject.SetActive(false);
        audioManager.gameObject.SetActive(false);

#if UNITY_EDITOR
        Debug.Log("@@@ ARLocation TRACKING");
        _VPS_Status.SetActive(false);
        startSemanticQuerying();
#else
        Debug.Log("@@@ Attempt to TRACK...");
        _arLocationManager.locationTrackingStateChanged += OnLocationTrackingStateChanged;
#endif        
    }

    public void startSemanticQuerying()
    {
        semanticQuery.gameObject.SetActive(true);
        audioManager.gameObject.SetActive(true);
    }

    // _RecordButton is clicked
    private void recordButtonOnClickHandler()
    {
        semanticQuery._GodMessage.SetActive(false);
    }

    private void OnLocationTrackingStateChanged(ARLocationTrackedEventArgs args)
    {
        if (args.Tracking)
        {
            Debug.Log("@@@ ARLocation TRACKING");
            _VPS_Status.SetActive(false);
            _Instructions.SetActive(true);
            startSemanticQuerying();
        }
        else
        {
            Debug.Log("@@@ ARLocation NOT TRACKING");
            _VPS_Status.SetActive(true);
        }
    }


    void showRecordButtonEvent()
    {
        _RecordButton.gameObject.SetActive(true);
        _Instructions.SetActive(false);
    }


    void OnDisable()
    {
        SemanticQuerying.enableRecording -= showRecordButtonEvent;
    }
}
