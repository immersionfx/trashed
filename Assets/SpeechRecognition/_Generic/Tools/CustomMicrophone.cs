using System;
using UnityEngine;
#if FG_MPRO
using Microphone = FrostweepGames.MicrophonePro.Microphone;
#endif

namespace FrostweepGames.Plugins.Native
{
	public sealed class CustomMicrophone
	{
		private static float[] _SamplesArrayBuffer = new float[0];

		public static bool GetData(float[] data, int offset, AudioClip clip)
        {
#if FG_MPRO
			return Microphone.GetData(data, offset);
#else
			return clip.GetData(data, offset);
#endif
        }

        public static void RequestMicrophonePermission()
        {
            if (!HasMicrophonePermission())
            {
#if UNITY_ANDROID
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
#elif UNITY_IOS
				Application.RequestUserAuthorization(UserAuthorization.Microphone);
#endif
            }
        }

        public static bool HasMicrophonePermission()
        {
#if UNITY_ANDROID
            return UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone);
#elif UNITY_IOS
			return Application.HasUserAuthorization(UserAuthorization.Microphone);
#else
			return true;
#endif
        }

        /// <summary>
        /// Detect voice based on threshold
        /// </summary>
        /// <param name="data">input bytes data</param>
        /// <param name="averageVoiceLevel">ref value of current voice level</param>
        /// <param name="threshold">threshold filter</param>
        /// <returns></returns>
        public static bool IsVoiceDetected(float[] samples, ref float averageVoiceLevel, double threshold = 0.02d)
		{
			return IsVoiceDetectedProcess(samples, ref averageVoiceLevel, threshold);
		}

		/// <summary>
		/// Detect voice based on threshold
		/// </summary>
		/// <param name="data">input bytes data</param>
		/// <param name="averageVoiceLevel">ref value of current voice level</param>
		/// <param name="threshold">threshold filter</param>
		/// <returns></returns>
		public static bool IsVoiceDetected(string deviceName, AudioClip audioClip, ref float averageVoiceLevel, double threshold = 0.02d)
		{
			if (Microphone.IsRecording(deviceName) && audioClip != null && audioClip)
			{
				if (_SamplesArrayBuffer.Length != audioClip.samples)
				{
					Array.Resize(ref _SamplesArrayBuffer, audioClip.samples);
				}

				if (GetData(_SamplesArrayBuffer, 0, audioClip))
				{
					int position = Microphone.GetPosition(deviceName);

					int amount = audioClip.frequency;

					if (position >= amount)
					{
						int startIndex = position - amount;
						int count = amount;

						if (startIndex + count >= _SamplesArrayBuffer.Length)
						{
							count = _SamplesArrayBuffer.Length - startIndex;
						}

						float[] samplesChunk = new float[count];
						for (int i = 0; i < samplesChunk.Length; i++)
						{
							samplesChunk[i] = _SamplesArrayBuffer[startIndex + i];
						}
						return IsVoiceDetected(samplesChunk, ref averageVoiceLevel, threshold);
					}
				}
			}

			return false;
		}

		/// <summary>
		/// Convert float array of RAW samples into bytes array
		/// </summary>
		/// <param name="samples"></param>
		/// <returns></returns>
		public static byte[] FloatToByte(float[] samples)
		{
			short[] intData = new short[samples.Length];

			byte[] bytesData = new byte[samples.Length * 2];

			for (int i = 0; i < samples.Length; i++)
			{
				intData[i] = (short)(samples[i] * 32767);
				byte[] byteArr = System.BitConverter.GetBytes(intData[i]);
				byteArr.CopyTo(bytesData, i * 2);
			}

			return bytesData;
		}

		/// <summary>
		/// Converts list of bytes to float array by using 32767 rescale factor
		/// </summary>
		/// <param name="bytesData"></param>
		/// <returns></returns>
		public static float[] ByteToFloat(byte[] bytesData)
		{
			int length = bytesData.Length / 2;
			float[] samples = new float[length];

			for (int i = 0; i < length; i++)
				samples[i] = (float)BitConverter.ToInt16(bytesData, i * 2) / 32767;

			return samples;
		}

		public static AudioClip MakeCopy(string name, int recordingTime, int frequency, int channels, AudioClip clip)
		{
			float[] array = new float[recordingTime * frequency * channels];
			if (GetData(array, 0, clip))
			{
				AudioClip audioClip = AudioClip.Create(name, recordingTime * frequency, channels, frequency, false);
				audioClip.SetData(array, 0);

				return audioClip;
			}

			return null;
		}

		/// <summary>
		/// Filters data based on threshold
		/// </summary>
		/// <param name="data">input bytes data</param>
		/// <param name="averageVoiceLevel">ref value of current voice level</param>
		/// <param name="threshold">threshold filter</param>
		/// <returns></returns>
		private static bool IsVoiceDetectedProcess(float[] samples, ref float averageVoiceLevel, double threshold = 0.02d)
		{
			bool detected = false;
			double sumTwo = 0;
			double tempValue;

			for (int index = 0; index < samples.Length; index++)
			{
				tempValue = samples[index];

				sumTwo += tempValue * tempValue;

				if (tempValue > threshold)
					detected = true;
			}

			sumTwo /= samples.Length;

			averageVoiceLevel = (averageVoiceLevel + (float)sumTwo) / 2f;

			if (detected || sumTwo > threshold)
				return true;
			else
				return false;
		}
	}
}