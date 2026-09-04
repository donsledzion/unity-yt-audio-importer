using UnityEditor;

namespace SoftAware.YouTubeAudioImporter.Editor.UI.Components
{
    public static class LegalDisclaimerModal
    {
        private const string DisclaimerAcceptedKey = "SoftAware_YtAudioImporter_LegalAccepted";

        public const string DisclaimerText = 
            "You may only import content for which you own the rights or that is not protected " +
            "by copyright. The plugin author assumes no liability for any use of this tool in " +
            "violation of applicable law or the YouTube Terms of Service.";

        public static bool HasAcceptedDisclaimer()
        {
            return EditorPrefs.GetBool(DisclaimerAcceptedKey, false);
        }

        public static void SetAccepted(bool accepted)
        {
            EditorPrefs.SetBool(DisclaimerAcceptedKey, accepted);
        }

        public static bool PromptUserIfNecessary()
        {
            if (HasAcceptedDisclaimer())
            {
                return true;
            }

            var accepted = EditorUtility.DisplayDialog(
                "YouTube Audio Importer — Legal Disclaimer",
                $"{DisclaimerText}\n\nDo you accept these terms and confirm that you will use this tool lawfully?",
                "I Accept",
                "Cancel"
            );

            if (accepted)
            {
                SetAccepted(true);
            }

            return accepted;
        }
    }
}
