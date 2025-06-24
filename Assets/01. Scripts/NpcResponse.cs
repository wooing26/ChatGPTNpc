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

    [JsonProperty("StoryImageDescription")]
    public string StoryImageDescription { get; set; }
}
