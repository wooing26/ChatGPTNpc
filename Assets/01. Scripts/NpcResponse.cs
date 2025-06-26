using Newtonsoft.Json;
using UnityEngine;

public class NpcResponse
{
    [JsonProperty("ReplyMessage")]
    public string ReplyMessage { get; set; }

    [JsonProperty("Appearance")]
    public string Appearance { get; set; }

    [JsonProperty("Emotion")]
    public string Emotion { get; set; }

    [JsonProperty("ExplosionProbability ")]
    public int ExplosionProbability { get; set; } = 0; // 0-100 ¹üÀ§ÀÇ È®·ü

    [JsonProperty("StoryImageDescription")]
    public string StoryImageDescription { get; set; }

    [JsonProperty("GameState")]
    public string GameState { get; set; } // "EXPLOSION", "VICTORY", "NORMAL", "GAME_OVER"
}
