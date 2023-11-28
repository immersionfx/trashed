using System;
using Niantic.Lightship.AR.Semantics;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.EventSystems;


public class SemanticQuerying : MonoBehaviour
{
    public ARCameraManager _cameraMan;
    public ARSemanticSegmentationManager _semanticMan;
    public GameObject _GodMessage; // "How can I help you today?"

    public static event Action enableRecording;
    
    private string _channel = "ground";
    private Vector2 mousePos;
    private float _timer = 0.0f;
    private bool isMessageShowing = false;



    void OnEnable()
    {
        isMessageShowing = false;
        _GodMessage.SetActive(false);       
    }


    void Update()
    {        
        if (!_semanticMan.enabled) return;
        if (!_semanticMan.subsystem.running) return;

#if UNITY_EDITOR
        if (Input.GetMouseButtonDown(0))
        {
            mousePos = Input.mousePosition;
        }
#else
        if (Input.touches.Length > 0)
        {
            if (EventSystem.current.IsPointerOverGameObject())
            {
                Debug.Log("@@@ Touched the UI");
                return;
            }
            mousePos = Input.touches[0].position;
        }
#endif

        if (mousePos.x > 0 && mousePos.x < Screen.width)
        {
            if (mousePos.y > 0 && mousePos.y < Screen.height)
            {
                _timer += Time.deltaTime;
                if (_timer > 0.05f)
                {
                    var list = _semanticMan.GetChannelNamesAt((int)mousePos.x, (int)mousePos.y);
                    if (list.Count > 0)
                    {
                        _channel = list[0];                                                   
                        //_text.text = _channel;
                    }
                    // else
                    // {
                    //     //_text.text = "?";
                    // }
                    _timer = 0.0f;
                }                 
            }
        }

        if (_channel == "sky")
        {
            if (!isMessageShowing) {
                _GodMessage.SetActive(true);
                enableRecording?.Invoke(); //@GameManager
                isMessageShowing = true;
                return;
            }            
        }
        else {
            if (isMessageShowing) {
                _GodMessage.SetActive(false);
                isMessageShowing = false;
            }
        }
    }
 
}