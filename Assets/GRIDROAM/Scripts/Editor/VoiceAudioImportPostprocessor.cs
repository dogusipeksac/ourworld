using UnityEditor;
using UnityEngine;

namespace OurWorld.Editor
{
    /// <summary>
    /// Automatically applies mobile-optimized mono Vorbis compression to offline Piper voiceover clips
    /// under Assets/GRIDROAM/Resources/Audio/Voice/ so iOS/Android builds stay lean (~3-4 MB for 200+ clips).
    /// </summary>
    public sealed class VoiceAudioImportPostprocessor : AssetPostprocessor
    {
        private const string VoiceFolderPrefix = "Assets/GRIDROAM/Resources/Audio/Voice/";

        private void OnPreprocessAudio()
        {
            if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith(VoiceFolderPrefix))
            {
                return;
            }

            AudioImporter importer = assetImporter as AudioImporter;
            if (importer == null) return;

            importer.forceToMono = true;
            importer.loadInBackground = false;
            importer.ambisonic = false;

            AudioImporterSampleSettings settings = new AudioImporterSampleSettings
            {
                loadType = AudioClipLoadType.CompressedInMemory,
                compressionFormat = AudioCompressionFormat.Vorbis,
                quality = 0.36f,
                sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate,
                preloadAudioData = false
            };

            importer.defaultSampleSettings = settings;
            importer.SetOverrideSampleSettings("iOS", settings);
            importer.SetOverrideSampleSettings("Android", settings);
        }

        [MenuItem("OurWorld/Audio/Reimport All Voice Clips (Mobile Vorbis)")]
        public static void ReimportAllVoiceClips()
        {
            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/GRIDROAM/Resources/Audio/Voice" });
            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            Debug.Log($"[VoiceAudioImportPostprocessor] Reimported {guids.Length} mobile-compressed voice clips.");
        }
    }
}
