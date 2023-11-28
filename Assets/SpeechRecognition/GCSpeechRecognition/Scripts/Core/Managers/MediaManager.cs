using System;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using FrostweepGames.Plugins.Core;
using System.Collections;
using FrostweepGames.Plugins.Native;
#if FG_MPRO
using Microphone = FrostweepGames.MicrophonePro.Microphone;
#endif
namespace FrostweepGames.Plugins.GoogleCloud.SpeechRecognition
{
    public class MediaManager : IService, IMediaManager
    {
		private const int _Frequency = 16000;

		public event Action MicrophoneDeviceSelectedEvent;

		public event Action RecordStartedEvent;
        public event Action RecordFailedEvent;
		public event Action<AudioClip, float[]> RecordEndedEvent;

		public event Action TalkBeganEvent;
        public event Action<AudioClip, float[]> TalkEndedEvent;

		private IVoiceDetectionManager _voiceDetectionManager;

		private ISpeechRecognitionManager _speechRecognitionManager;

		private AudioClip _microphoneWorkingAudioClip;

        private int _currentSamplePosition;

        private int _previousSamplePosition;

        private float[] _currentAudioSamples;

        private bool _isTalking;

        private List<float> _currentRecordingVoice;

		private float _maxVoiceFrame;

		private float _endTalkingDelay;

		private bool _permissionGranted;

		private int _channels;

        public bool IsRecording { get; private set; }
		public string MicrophoneDevice { get; private set; }
		public AudioClip LastRecordedClip { get; private set; }
		public float[] LastRecordedRaw { get; private set; }
		public bool DetectVoice { get; private set; }

		public void Init()
        {
			_voiceDetectionManager = ServiceLocator.Get<IVoiceDetectionManager>();
			_speechRecognitionManager = ServiceLocator.Get<ISpeechRecognitionManager>();

#if UNITY_EDITOR || UNITY_STANDALONE
            _permissionGranted = true;
#endif
#if FG_MPRO
            Microphone.PermissionChangedEvent += PermissionChangedEventHandler;
			Microphone.RecordStreamDataEvent += RecordStreamDataEventHandler;
#endif
        }

        public void Update()
        {
#if !FG_MPRO || UNITY_EDITOR
            if (IsRecording)
            {
                _currentSamplePosition = Microphone.GetPosition(MicrophoneDevice);
                CustomMicrophone.GetData(_currentAudioSamples, 0, _microphoneWorkingAudioClip);

				if (DetectVoice)
                {
                    bool isTalking = _voiceDetectionManager.HasDetectedVoice(_currentAudioSamples);

					if (isTalking)
					{
						_endTalkingDelay = 0f;	
					}
					else
					{
						_endTalkingDelay += Time.deltaTime;
					}

                    if (!_isTalking && isTalking)
                    {
						AddStartAudioSamplesIntoBuffer();

						_isTalking = true;

						TalkBeganEvent?.Invoke();
					}
					else if (_isTalking && !isTalking && _endTalkingDelay >= _speechRecognitionManager.CurrentConfig.voiceDetectionEndTalkingDelay)
                    {
						_isTalking = false;

						LastRecordedRaw = _currentRecordingVoice.ToArray();
						LastRecordedClip = AudioConvert.Convert(LastRecordedRaw, _microphoneWorkingAudioClip.channels);

						_currentRecordingVoice.Clear();

						TalkEndedEvent?.Invoke(LastRecordedClip, LastRecordedRaw);
					}
                    else if (_isTalking && isTalking)
                    {
                        AddAudioSamplesIntoBuffer();
                    }
                }
                else
                {
                    AddAudioSamplesIntoBuffer();
                }

                _previousSamplePosition = _currentSamplePosition;
            }
#endif
        }

        public void Dispose()
        {
#if FG_MPRO
            Microphone.PermissionChangedEvent -= PermissionChangedEventHandler;
            Microphone.RecordStreamDataEvent -= RecordStreamDataEventHandler;
#endif
            if (_microphoneWorkingAudioClip != null)
			{
				MonoBehaviour.Destroy(_microphoneWorkingAudioClip);
				_microphoneWorkingAudioClip = null;
			}

			if (LastRecordedClip != null)
			{
				MonoBehaviour.Destroy(LastRecordedClip);
				LastRecordedClip = null;
			}
        }

		public float GetLastFrame()
		{
			int minValue = _Frequency / 8;

			if (_currentRecordingVoice == null || _currentRecordingVoice.Count < minValue)
				return 0;

			int position = Mathf.Clamp(_currentRecordingVoice.Count - (minValue + 1), 0, _currentRecordingVoice.Count-1);

			float sum = 0f;
			for(int i = position; i < _currentRecordingVoice.Count; i++)
			{
				sum += Mathf.Abs(_currentRecordingVoice[i]);
			}

			sum /= minValue;

			return sum;
		}

		public float GetMaxFrame()
		{
			return _maxVoiceFrame;
		}

		public void StartRecord(bool withVoiceDetection = false)
		{
			if (IsRecording)
				return;

			if(!ReadyToRecord())
			{
				RecordFailedEvent?.Invoke();
				return;
			}

			DetectVoice = withVoiceDetection;

			_maxVoiceFrame = 0;

			_currentRecordingVoice = new List<float>();

			if (_microphoneWorkingAudioClip != null)
			{
				MonoBehaviour.Destroy(_microphoneWorkingAudioClip);
			}

			if (LastRecordedClip != null)
			{
				MonoBehaviour.Destroy(LastRecordedClip);
			}

			_microphoneWorkingAudioClip = Microphone.Start(MicrophoneDevice, true, 1, _Frequency);

			_channels = _microphoneWorkingAudioClip.channels;

#if FG_MPRO && !UNITY_EDITOR
			_channels = 1;
#endif
            _currentAudioSamples = new float[_Frequency * _channels];

			IsRecording = true;

			RecordStartedEvent?.Invoke();
		}

		public void StopRecord()
		{
			if (!IsRecording || !ReadyToRecord())
				return;

			IsRecording = false;

			Microphone.End(MicrophoneDevice);

			if (!DetectVoice)
			{
				LastRecordedRaw = _currentRecordingVoice.ToArray();
				LastRecordedClip = AudioConvert.Convert(LastRecordedRaw, _channels);			
			} 
			else
			{
				LastRecordedRaw = null;
				LastRecordedClip = null;
			}

			if (_currentRecordingVoice != null)
			{
				_currentRecordingVoice.Clear();
			}

			_currentAudioSamples = null;
			_currentRecordingVoice = null;

			RecordEndedEvent?.Invoke(LastRecordedClip, LastRecordedRaw);
		}

		public bool ReadyToRecord()
		{
			return HasConnectedMicrophoneDevices() && !string.IsNullOrEmpty(MicrophoneDevice);
		}

		public bool HasConnectedMicrophoneDevices()
		{
			return Microphone.devices.Length > 0;
		}

		public void SetMicrophoneDevice(string device)
		{
			if(MicrophoneDevice == device)
			{
				Debug.LogWarning("you are trying to select microphone device that already selected");
				return;
			}

			MicrophoneDevice = device;

			MicrophoneDeviceSelectedEvent?.Invoke();
		}

		public string[] GetMicrophoneDevices()
		{
			return Microphone.devices;
		}

		public void SaveLastRecordedAudioClip(string path)
		{
			if (LastRecordedClip != null)
			{
				try
				{
#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_ANDROID || UNITY_IOS
					File.WriteAllBytes(path, AudioClip2PCMConverter.AudioClip2PCM(LastRecordedClip));
#endif
				}
				catch (Exception e)
				{
					Debug.LogException(e);
				}
			}
		}


		public IEnumerator OneTimeRecord(int durationSec, Action<float[]> callback, int sampleRate = 16000)
		{
			if (HasMicrophonePermission() && Microphone.devices.Length > 0)
			{
				AudioClip clip = Microphone.Start(Microphone.devices[0], false, durationSec, sampleRate);

				yield return new WaitForSeconds(durationSec);

				Microphone.End(Microphone.devices[0]);

				float[] array = new float[sampleRate * durationSec * clip.channels];

				CustomMicrophone.GetData(array, 0, clip);

				callback?.Invoke(array);
			}
            else
            {
                callback?.Invoke(null);
            }
		}

		public bool HasMicrophonePermission()
		{
#if FG_MPRO
			return _permissionGranted;
#else
			return CustomMicrophone.HasMicrophonePermission();
#endif
        }

		/// <summary>
		/// Currently works as synchronous function with callback when app unpauses
		/// could not work properly if has enabled checkbox regarding additional frame in pause
		/// </summary>
		/// <param name="callback"></param>
		public void RequestMicrophonePermission(Action<bool> callback)
		{
#if FG_MPRO
			Microphone.RequestPermission();
#else
			CustomMicrophone.RequestMicrophonePermission();
#endif
            callback?.Invoke(HasMicrophonePermission());
		}

		private void AddAudioSamplesIntoBuffer()
		{
			if (_previousSamplePosition > _currentSamplePosition)
			{
				for (int i = _previousSamplePosition; i < _currentAudioSamples.Length; i++)
				{
					_currentRecordingVoice.Add(_currentAudioSamples[i]);

					if (_currentAudioSamples[i] > _maxVoiceFrame)
						_maxVoiceFrame = _currentAudioSamples[i];
				}

				_previousSamplePosition = 0;
			}

			for (int i = _previousSamplePosition; i < _currentSamplePosition; i++)
			{
				_currentRecordingVoice.Add(_currentAudioSamples[i]);

				if (_currentAudioSamples[i] > _maxVoiceFrame)
					_maxVoiceFrame = _currentAudioSamples[i];
			}

			_previousSamplePosition = _currentSamplePosition;
		}

		private void AddStartAudioSamplesIntoBuffer()
		{
			int count = _currentSamplePosition - 2000;

			if (count >= 0)
			{
				for (int i = count; i < _currentSamplePosition; i++)
				{
					_currentRecordingVoice.Add(_currentAudioSamples[i]);

					if (_currentAudioSamples[i] > _maxVoiceFrame)
						_maxVoiceFrame = _currentAudioSamples[i];
				}
			}
			else
			{
				for (int i = _currentAudioSamples.Length - Mathf.Abs(count); i < _currentAudioSamples.Length; i++)
				{
					_currentRecordingVoice.Add(_currentAudioSamples[i]);

					if (_currentAudioSamples[i] > _maxVoiceFrame)
						_maxVoiceFrame = _currentAudioSamples[i];
				}

				for (int i = 0; i < _currentSamplePosition; i++)
				{
					_currentRecordingVoice.Add(_currentAudioSamples[i]);

					if (_currentAudioSamples[i] > _maxVoiceFrame)
						_maxVoiceFrame = _currentAudioSamples[i];
				}
			}

			_previousSamplePosition = _currentSamplePosition;
		}
#if FG_MPRO
        private void PermissionChangedEventHandler(bool granted)
        {
            _permissionGranted = granted;

			if (granted && Microphone.devices.Length > 0)
            {
				SetMicrophoneDevice(Microphone.devices[0]);
			}
			else
            {
				SetMicrophoneDevice(null);
            }
        }

		private void RecordStreamDataEventHandler(Microphone.StreamData streamData)
        {
            if (DetectVoice)
            {
                bool isTalking = _voiceDetectionManager.HasDetectedVoice(streamData.ChannelsData[0]);

                if (isTalking)
                {
                    _endTalkingDelay = 0f;
                }
                else
                {
                    _endTalkingDelay += Time.deltaTime;
                }

                if (!_isTalking && isTalking)
                {
                    _currentRecordingVoice.AddRange(streamData.ChannelsData[0]);

                    _isTalking = true;

                    TalkBeganEvent?.Invoke();
                }
                else if (_isTalking && !isTalking && _endTalkingDelay >= _speechRecognitionManager.CurrentConfig.voiceDetectionEndTalkingDelay)
                {
                    _isTalking = false;

                    LastRecordedRaw = _currentRecordingVoice.ToArray();
                    LastRecordedClip = AudioConvert.Convert(LastRecordedRaw, _channels);

                    _currentRecordingVoice.Clear();

                    TalkEndedEvent?.Invoke(LastRecordedClip, LastRecordedRaw);
                }
                else if (_isTalking && isTalking)
                {
                    _currentRecordingVoice.AddRange(streamData.ChannelsData[0]);
                }
            }
            else
            {
                _currentRecordingVoice.AddRange(streamData.ChannelsData[0]);
            }
        }
#endif
    }
}