using UnityEngine;

namespace Sekai.MusicScoreMaker.Ingame.AudioAssist
{
    public static class AudioAssistStemColors
    {
        public static Color For(string key) => key switch
        {
            "original" => new Color(.39f,.85f,.95f),
            "vocals" => new Color(.78f,.61f,.98f),
            "instrumental" => new Color(.40f,.88f,.72f),
            "drums" => new Color(1f,.64f,.44f),
            "bass" => new Color(.95f,.80f,.38f),
            "other" => new Color(.97f,.57f,.76f),
            _ => Color.white
        };
    }
}
