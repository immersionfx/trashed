using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace FrostweepGames.Plugins.GoogleCloud.SpeechRecognition.Examples
{
    public class AudioManager : MonoBehaviour
    {
        //Events
        public static event Action<int> startSpawner;
        public static event Action stopSpawner;
        public static event Action restartSpawner;

        public GCSpeechRecognition _speechRecognition;

        public GameObject _recordButton;
        public Sprite recordOff, recordOn;

        [SerializeField] private GameObject _SpeechBubble;
        [SerializeField] private TMP_Text _SpeechText;

        public bool isRecording = false;


        private void OnEnable()
        {
            _speechRecognition = GCSpeechRecognition.Instance;
            _speechRecognition.RecognizeSuccessEvent += RecognizeSuccessEventHandler;
            _speechRecognition.RecognizeFailedEvent += RecognizeFailedEventHandler;

            _speechRecognition.FinishedRecordEvent += FinishedRecordEventHandler;
            _speechRecognition.RecordFailedEvent += RecordFailedEventHandler;

            _speechRecognition.BeginTalkigEvent += BeginTalkigEventHandler;
            _speechRecognition.EndTalkigEvent += EndTalkigEventHandler;

            FallingObjectSpawner.hideRecorder += HideRecorder;

            _SpeechBubble.SetActive(false);
            GetMicrophoneDevice(0);
        }

        public void ClickRecordButton()
        {
            if (!isRecording) {
                _recordButton.GetComponent<Image>().sprite = recordOn;
                _SpeechText.text = string.Empty;
                _speechRecognition.StartRecord(false);
                isRecording = true;
            }
            else {
                _recordButton.GetComponent<Image>().sprite = recordOff;
                _speechRecognition.StopRecord();
                isRecording = false;
            }
        }

        //Get the first available Microphone device
        private void GetMicrophoneDevice(int value)
        {
            if (!_speechRecognition.HasConnectedMicrophoneDevices()) return;
            _speechRecognition.SetMicrophoneDevice(_speechRecognition.GetMicrophoneDevices()[value]);
        }

        private void RecordFailedEventHandler()
        {
            Debug.LogError("@@@ Start record Failed. Please check microphone device and try again.");
        }

        private void BeginTalkigEventHandler()
        {
            Debug.Log("Talk Began.");
        }

        private void EndTalkigEventHandler(AudioClip clip, float[] raw)
        {
            Debug.Log("Talk Ended.");

            FinishedRecordEventHandler(clip, raw);
        }

        private void FinishedRecordEventHandler(AudioClip clip, float[] raw)
        {
            string phrasesText = "";

            if (clip == null)
                return;

            RecognitionConfig config = RecognitionConfig.GetDefault();
            config.languageCode = "en_GB";
            config.speechContexts = new SpeechContext[]
            {
                new SpeechContext()
                {
                    phrases = phrasesText.Split(',')
                }
            };
            config.audioChannelCount = clip.channels;

            GeneralRecognitionRequest recognitionRequest;

            recognitionRequest = new GeneralRecognitionRequest();

            recognitionRequest.audio = new RecognitionAudioContent() // for base64 data
            {
                content = raw.ToBase64(channels: clip.channels)
            };

            recognitionRequest.config = config;
            _speechRecognition.Recognize(recognitionRequest);
        }

        private void RecognizeFailedEventHandler(string error)
        {
            Debug.LogError("@@@ Recognize Failed: " + error);
        }

        private void RecognizeSuccessEventHandler(RecognitionResponse recognitionResponse)
        {
            Debug.Log("@@@ Recognize Success.");
            InsertRecognitionResponseInfo(recognitionResponse);
        }

        private void InsertRecognitionResponseInfo(RecognitionResponse recognitionResponse)
        {
            if (recognitionResponse == null || recognitionResponse.results.Length == 0)
            {
                StartCoroutine(keepSpeechBubbleFor("Words not detected.", 3));
                return;
            }
            var words = recognitionResponse.results[0].alternatives[0].words;

            if (words != null)
            {
                StartCoroutine(keepSpeechBubbleFor(recognitionResponse.results[0].alternatives[0].transcript, 6));
                detectWords(recognitionResponse.results[0].alternatives[0].words);
            }
        }


        private void detectWords(WordInfo[] words)
        {
            int itemID = -1;

            foreach (var item in words)
            {
                if (item.word == "glass") {
                    itemID = 0;
                    break;
                }
                if (item.word == "plastic")
                {
                    itemID = 1;
                    break;
                }
                if (item.word == "food")
                {
                    itemID = 2;
                    break;
                }
                if (item.word == "aluminium" || item.word == "cans")
                {
                    itemID = 3;
                    break;
                }
                if (item.word == "stop")
                {
                    itemID = 4;
                    break;
                }
                if (item.word == "restart" || item.word == "menu")
                {
                    itemID = 5;
                    break;
                }
            }

            Debug.LogFormat("@@@ Item found: {0}", itemID);

            if (itemID == 4)
                stopSpawner?.Invoke(); //@FallingObjectSpawner
            else if (itemID == 5) {  
                restartSpawner?.Invoke(); //@FallingObjectSpawner
                _SpeechBubble.SetActive(false);
            }
            else if (itemID > -1) {
                startSpawner?.Invoke(itemID); //@FallingObjectSpawner
            }
        }

        
        IEnumerator keepSpeechBubbleFor(string txt, float secs)
        {
            _SpeechBubble.SetActive(true);
            _SpeechText.text += txt;
            yield return new WaitForSeconds(secs);
            _SpeechBubble.SetActive(false);
            _SpeechText.text = string.Empty;
        }

        void HideRecorder()
        {
            _recordButton.SetActive(false);
        }

        void OnDisable()
        {
            FallingObjectSpawner.hideRecorder -= HideRecorder;
            StopAllCoroutines();
        }

        private void OnDestroy()
        {
            _speechRecognition.RecognizeSuccessEvent -= RecognizeSuccessEventHandler;
            _speechRecognition.RecognizeFailedEvent -= RecognizeFailedEventHandler;
            _speechRecognition.FinishedRecordEvent -= FinishedRecordEventHandler;
            _speechRecognition.RecordFailedEvent -= RecordFailedEventHandler;
            _speechRecognition.EndTalkigEvent -= EndTalkigEventHandler;
            StopAllCoroutines();
        }
    }
}